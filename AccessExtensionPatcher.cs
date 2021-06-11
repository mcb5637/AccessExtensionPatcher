using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace AccessExtension
{
    public static class AccessExtensionPatcher
    {
        private static readonly Dictionary<MethodBase, FieldInfo> PatchFieldInfo = new Dictionary<MethodBase, FieldInfo>();
        private static readonly Dictionary<MethodBase, MethodCall> PatchMethodInfo = new Dictionary<MethodBase, MethodCall>();
        private static readonly Dictionary<MethodBase, ConstructorInfo> PatchConstructorInfo = new Dictionary<MethodBase, ConstructorInfo>();

        public static void PatchAll(HarmonyInstance h, Assembly a)
        {
            foreach (Type t in a.GetTypes())
            {
                foreach (MethodBase m in t.GetMethods())
                {
                    if (m.GetCustomAttribute<FieldGet>() != null)
                    {
                        PatchFieldGet(h, m);
                    }
                    else if (m.GetCustomAttribute<FieldSet>() != null)
                    {
                        PatchFieldSet(h, m);
                    }
                    else if (m.GetCustomAttribute<MethodCall>() != null)
                    {
                        PatchMethodCall(h, m);
                    }
                    else if (m.GetCustomAttribute<ConstructorCall>() != null)
                    {
                        PatchConstructorCall(h, m);
                    }
                }
            }
        }

        public static void PatchFieldGet(HarmonyInstance h, MethodBase m, FieldInfo f = null)
        {
            if (f != null)
                PatchFieldInfo.Add(m, f);
            h.Patch(m, null, null, new HarmonyMethod(AccessTools.Method(typeof(AccessExtensionPatcher), nameof(TransFieldGet))));
        }
        public static void PatchFieldSet(HarmonyInstance h, MethodBase m, FieldInfo f = null)
        {
            if (f != null)
                PatchFieldInfo.Add(m, f);
            h.Patch(m, null, null, new HarmonyMethod(AccessTools.Method(typeof(AccessExtensionPatcher), nameof(TransFieldSet))));
        }

        public static void PatchMethodCall(HarmonyInstance h, MethodBase m, MethodInfo target = null)
        {
            if (target != null)
                PatchMethodInfo.Add(m, new MethodCall(target, target.ReturnType));
            h.Patch(m, null, null, new HarmonyMethod(AccessTools.Method(typeof(AccessExtensionPatcher), nameof(TransMethodCall))));
        }

        public static void PatchPropertyGet(HarmonyInstance h, MethodBase m, PropertyInfo p)
        {
            PatchMethodCall(h, m, p.GetMethod);
        }

        public static void PatchPropertySet(HarmonyInstance h, MethodBase m, PropertyInfo p)
        {
            PatchMethodCall(h, m, p.SetMethod);
        }

        public static void PatchConstructorCall(HarmonyInstance h, MethodBase m, ConstructorInfo c = null)
        {
            if (c != null)
                PatchConstructorInfo.Add(m, c);
            h.Patch(m, null, null, new HarmonyMethod(AccessTools.Method(typeof(AccessExtensionPatcher), nameof(TransConstructorCall))));
        }

        private static IEnumerable<CodeInstruction> TransFieldGet(IEnumerable<CodeInstruction> c, MethodBase method)
        {
            
            FieldInfo field = PatchFieldInfo.TryGetValue(method, out FieldInfo o) ? o : method.GetCustomAttribute<FieldGet>().i;
            if (field.IsStatic)
            {
                CheckParamTypes(method, field.FieldType);
                yield return new CodeInstruction(OpCodes.Ldsfld, field);
                yield return new CodeInstruction(OpCodes.Ret);
            }
            else
            {
                CheckParamTypes(method, field.FieldType, field.DeclaringType);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, field);
                yield return new CodeInstruction(OpCodes.Ret);
            }
        }
        private static IEnumerable<CodeInstruction> TransFieldSet(IEnumerable<CodeInstruction> c, MethodBase method)
        {
            FieldInfo field = PatchFieldInfo.TryGetValue(method, out FieldInfo o) ? o : method.GetCustomAttribute<FieldSet>().i;
            if (field.IsStatic)
            {
                CheckParamTypes(method, typeof(void), field.FieldType);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Stsfld, field);
                yield return new CodeInstruction(OpCodes.Ret);
            }
            else
            {
                CheckParamTypes(method, typeof(void), field.DeclaringType, field.FieldType);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Stfld, field);
                yield return new CodeInstruction(OpCodes.Ret);
            }
        }
        private static IEnumerable<CodeInstruction> TransMethodCall(IEnumerable<CodeInstruction> c, MethodBase method)
        {
            MethodCall mcall = PatchMethodInfo.TryGetValue(method, out MethodCall o) ? o : method.GetCustomAttribute<MethodCall>();
            MethodBase i = mcall.i;
            Type r = mcall.ret;
            if (i.IsStatic)
            {
                CheckParamTypes(method, r, i.GetParameters().Length, i.GetParameters().Select((p) => p.ParameterType));
                for (int j = 0; j < i.GetParameters().Length; j++)
                    yield return new CodeInstruction(OpCodes.Ldarg, j);
                yield return new CodeInstruction(OpCodes.Call, i);
                yield return new CodeInstruction(OpCodes.Ret);
            }
            else
            {
                CheckParamTypes(method, r, i.GetParameters().Length+1, i.GetParameters().Select((p) => p.ParameterType).Prepend(i.DeclaringType));
                for (int j = 0; j < i.GetParameters().Length + 1; j++)
                    yield return new CodeInstruction(OpCodes.Ldarg, j);
                yield return new CodeInstruction(OpCodes.Callvirt, i);
                yield return new CodeInstruction(OpCodes.Ret);
            }
        }
        private static IEnumerable<CodeInstruction> TransConstructorCall(IEnumerable<CodeInstruction> c, MethodBase method)
        {
            ConstructorInfo i = PatchConstructorInfo.TryGetValue(method, out ConstructorInfo o) ? o : method.GetCustomAttribute<ConstructorCall>().i;
            CheckParamTypes(method, i.DeclaringType, i.GetParameters().Length, i.GetParameters().Select((p) => p.ParameterType));
            for (int j = 0; j < i.GetParameters().Length; j++)
                yield return new CodeInstruction(OpCodes.Ldarg, j);
            yield return new CodeInstruction(OpCodes.Newobj, i);
            yield return new CodeInstruction(OpCodes.Ret);
        }

        private static void CheckParamTypes(MethodBase m, Type ret, params Type[] t)
        {
            CheckParamTypes(m, ret, t.Length, t);
        }

        private static void CheckParamTypes(MethodBase m, Type ret, int numParam, IEnumerable<Type> t)
        {
            if (!m.IsStatic)
                throw new ArgumentException("non static method");
            if (!(m is MethodInfo i && i.ReturnType.IsEquivalentTo(ret)))
                throw new ArgumentException("return type different");
            ParameterInfo[] p = m.GetParameters();
            if (p.Length != numParam)
                throw new ArgumentException("parameter number different");
            if (p.Zip(t, (pi, ti) => pi.ParameterType.Equals(ti)).Contains(false))
                throw new ArgumentException("parameter type mismatch");
        }

        public static Assembly GetLoadedAssemblybyName(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where((a) => name.Equals(a.GetName().Name)).FirstOrDefault();
        }

        /// <summary>
        /// searches all loades assemblies for a matching static method and generates an delegate to it.
        /// </summary>
        /// <typeparam name="T">type of delegate</typeparam>
        /// <param name="assembly">assembly (dll) filename</param>
        /// <param name="type">fully qualified type name (namespace.Typename)</param>
        /// <param name="method">method name</param>
        /// <param name="del">reference to delegate variable. only written if something found</param>
        /// <param name="pred">optional predicate to filter methods</param>
        /// <param name="op">optional transform the selected method (example: instanciate a generic with MakeGenericMethod</param>
        /// <returns>true, if delegate was created successfully</returns>
        public static bool GetDelegateFromAssembly<T>(string assembly, string type, string method, ref T del, Func<MethodInfo, bool> pred = null, Func<MethodInfo, Assembly, MethodInfo> op = null) where T : Delegate
        {
            return GetDelegateFromAssembly(GetLoadedAssemblybyName(assembly), type, method, ref del, pred, op);
        }
        public static bool GetDelegateFromAssembly<T>(Assembly a, string type, string method, ref T del, Func<MethodInfo, bool> pred = null, Func<MethodInfo, Assembly, MethodInfo> op = null) where T : Delegate
        {
            if (a == null)
                return false;
            if (pred == null)
                pred = (_) => true;
            if (op == null)
                op = (i, _) => i;
            Type t = a.GetType(type);
            if (t != null)
            {
                MethodInfo m = op(t.GetMethods().Where((i) => i.Name.Equals(method)).Single(pred), a); // throws if more than one found
                del = (T)Delegate.CreateDelegate(typeof(T), m);
                return true;
            }
            return false;
        }
    }

    public class FieldGet : Attribute
    {
        internal FieldInfo i;

        public FieldGet(FieldInfo i)
        {
            this.i = i;
        }

        public FieldGet(Type t, string f)
        {
            i = AccessTools.Field(t, f);
        }
    }
    public class FieldSet : Attribute
    {
        internal FieldInfo i;

        public FieldSet(FieldInfo i)
        {
            this.i = i;
        }

        public FieldSet(Type t, string f)
        {
            i = AccessTools.Field(t, f);
        }
    }
    public class MethodCall : Attribute
    {
        internal MethodBase i;
        internal Type ret;

        public MethodCall(MethodBase i, Type ret)
        {
            this.i = i;
            this.ret = ret;
        }

        internal MethodCall()
        {
        }

        public MethodCall(Type t, string m, Type[] p = null)
        {
            MethodInfo inf = AccessTools.Method(t, m, p);
            i = inf;
            ret = inf.ReturnType;
        }
    }
    public class PropertyGet : MethodCall
    {
        public PropertyGet(Type t, string m)
        {
            PropertyInfo p = AccessTools.Property(t, m);
            i = p.GetMethod;
            ret = p.PropertyType;
        }
    }
    public class PropertySet : MethodCall
    {
        public PropertySet(Type t, string m)
        {
            PropertyInfo p = AccessTools.Property(t, m);
            i = p.SetMethod;
            ret = typeof(void);
        }
    }
    public class ConstructorCall : Attribute
    {
        internal ConstructorInfo i;

        public ConstructorCall(ConstructorInfo i)
        {
            this.i = i;
        }

        public ConstructorCall(Type t, Type[] p = null)
        {
            i = AccessTools.Constructor(t, p);
        }
    }
}
