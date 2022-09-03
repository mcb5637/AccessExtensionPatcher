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
                PatchMethodInfo.Add(m, new MethodCall(target));
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
            
            FieldInfo field = PatchFieldInfo.TryGetValue(method, out FieldInfo o) ? o : method.GetCustomAttribute<FieldGet>().Field;
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
            FieldInfo field = PatchFieldInfo.TryGetValue(method, out FieldInfo o) ? o : method.GetCustomAttribute<FieldSet>().Field;
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
            MethodInfo i = mcall.Method;
            Type r = i.ReturnType;
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
            ConstructorInfo i = PatchConstructorInfo.TryGetValue(method, out ConstructorInfo o) ? o : method.GetCustomAttribute<ConstructorCall>().Constructor;
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

        public static Assembly GetLoadedAssemblyByName(string name)
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
        /// <param name="log">optional gets called with a logging string</param>
        /// <returns>true, if delegate was created successfully</returns>
        public static bool GetDelegateFromAssembly<T>(string assembly, string type, string method, ref T del, Func<MethodInfo, bool> pred = null, Func<MethodInfo, Assembly, MethodInfo> op = null, Action<string> log = null) where T : Delegate
        {
            return GetDelegateFromAssembly(GetLoadedAssemblyByName(assembly), type, method, ref del, pred, op, log);
        }
        public static bool GetDelegateFromAssembly<T>(Assembly a, string type, string method, ref T del, Func<MethodInfo, bool> pred = null, Func<MethodInfo, Assembly, MethodInfo> op = null, Action<string> log = null) where T : Delegate
        {
            if (a == null)
            {
                log?.Invoke("AEP: Assembly null");
                return false;
            }
            if (pred == null)
                pred = (_) => true;
            if (op == null)
                op = (i, _) => i;
            Type t = a.GetType(type);
            if (t != null)
            {
                MethodInfo m = op(t.GetMethods().Where((i) => i.Name.Equals(method)).SingleOrDefault(pred), a); // throws if more than one found
                if (m == null)
                {
                    log?.Invoke($"AEP: no method {method} found in {type} that satisfies all conditions");
                    return false;
                }
                del = (T)Delegate.CreateDelegate(typeof(T), m);
                log?.Invoke("AEP: delegate bound to " + m.FullName());
                return true;
            }
            log?.Invoke($"AEP: Type {type} not found in Assembly");
            return false;
        }

        public static string FullName(this MethodBase m)
        {
            string r;
            if (m is MethodInfo mi)
                r = mi.ReturnType.FullName;
            else
                r = "UnknownReturn";
            r += " " + m.DeclaringType.FullName;
            r += "." + m.Name;
            Type[] gens;
            try
            {
                gens = m.GetGenericArguments();
            }
            catch (NotSupportedException)
            {
                gens = null;
            }
            if (gens != null && gens.Length > 0)
            {
                r += "<" + string.Join(", ", gens.Select((t) => t.FullName)) + ">";
            }
            r += "(" + string.Join(", ", m.GetParameters().Select(o => $"{o.ParameterType} {o.Name}")) + ")";
            return r;
        }

        public static Type GenerateType(string name, Type baseClass, Type[] interfaces, IEnumerable<CustomAttributeBuilder> attributes)
        {
            AssemblyName aname = new AssemblyName(name);
            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(aname, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
            TypeBuilder typeBuilder = moduleBuilder.DefineType(aname.FullName, TypeAttributes.Public | TypeAttributes.Class, baseClass, interfaces);
            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

            foreach (Type t in interfaces)
            {
                foreach (PropertyInfo p in t.GetProperties())
                {
                    TypeGenerateProperty(typeBuilder, p.Name, p.PropertyType, p);
                }
            }

            foreach (CustomAttributeBuilder at in attributes)
                typeBuilder.SetCustomAttribute(at);

            return typeBuilder.CreateType();
        }

        private static void TypeGenerateProperty(TypeBuilder typeBuilder, string name, Type ty, PropertyInfo over=null)
        {
            FieldBuilder field = typeBuilder.DefineField("_" + name, ty, FieldAttributes.Private);
            PropertyBuilder prop = typeBuilder.DefineProperty(name, PropertyAttributes.HasDefault, ty, null);
            MethodAttributes att = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
            if (over != null)
                att |= MethodAttributes.Virtual;
            MethodBuilder getbuild = typeBuilder.DefineMethod("get_"+name, att, ty, Type.EmptyTypes);
            ILGenerator getil = getbuild.GetILGenerator();
            getil.Emit(OpCodes.Ldarg_0);
            getil.Emit(OpCodes.Ldfld, field);
            getil.Emit(OpCodes.Ret);
            MethodBuilder setbuild = typeBuilder.DefineMethod("set_"+name, att, null, new Type[] { ty });
            ILGenerator setil = setbuild.GetILGenerator();
            setil.Emit(OpCodes.Ldarg_0);
            setil.Emit(OpCodes.Ldarg_1);
            setil.Emit(OpCodes.Stfld, field);
            setil.Emit(OpCodes.Ret);
            prop.SetGetMethod(getbuild);
            prop.SetSetMethod(setbuild);
            if (over != null)
            {
                typeBuilder.DefineMethodOverride(getbuild, over.GetGetMethod());
                typeBuilder.DefineMethodOverride(setbuild, over.GetSetMethod());
            }
        }

        public static T GenerateCastAndCall<T>(MethodInfo i) where T : Delegate
        {
            MethodInfo tm = typeof(T).GetMethod("Invoke");
            Type[] iparams;
            if (i.IsStatic)
                iparams = i.GetParameters().Select((p) => p.ParameterType).ToArray();
            else
                iparams = i.GetParameters().Select((p) => p.ParameterType).ToArray().Prepend(i.DeclaringType).ToArray();
           Type[] tmparams = tm.GetParameters().Select((p) => p.ParameterType).ToArray();
            int count = iparams.Count();
            if (count != tmparams.Count())
                throw new ArgumentException("parameter count mismatch");
            Type obj = typeof(object);
            for (int j=0; j < count; j++)
            {
                if (tmparams[j] != obj && tmparams[j] != iparams[j])
                    throw new ArgumentException("parameter type mismatch " + i);
            }
            if (!(tm.ReturnType == obj && i.ReturnType != typeof(void)) && i.ReturnType != tm.ReturnType)
                throw new ArgumentException("return type mismatch");

            DynamicMethod dm = new DynamicMethod(i.Name+"_CastAndCall", tm.ReturnType, tmparams);
            ILGenerator g = dm.GetILGenerator();
            for (int j = 0; j < count; j++)
            {
                g.Emit(OpCodes.Ldarg, j);
                if (tmparams[j] != iparams[j])
                    g.Emit(OpCodes.Castclass, iparams[j]);
            }
            g.Emit(OpCodes.Call, i);
            g.Emit(OpCodes.Ret);
            return (T)dm.CreateDelegate(typeof(T));
        }

        private class Comparator : IEqualityComparer<CodeInstruction>
        {
            public bool Equals(CodeInstruction x, CodeInstruction y)
            {
                return x.opcode == y.opcode && x.operand.Equals(y.operand);
            }

            public int GetHashCode(CodeInstruction obj)
            {
                return obj.opcode.GetHashCode() ^ obj.operand.GetHashCode();
            }
        }
        /// <summary>
        /// tool to insert code for a harmony transpiler.
        /// compares the last compare.Length instructions with compare, if equals, calls onfound and inserts its return.
        /// then continues searching.
        /// you may modify the pref LinkedList that onfound gets as parameter.
        /// if you do not want the matched code to be emitted again after the insertion, clear the prev LinkedList.
        /// if you want to modify the matched code, return the modified code and clear the prev LinkedList.
        /// if you want to search your emitted code again, only modify the prev LinkedList and return an empty enumeration.
        /// you may not increase the size of the prev LinkedList to > compare.Length.
        /// </summary>
        /// <param name="code">input code</param>
        /// <param name="compare">code to search for</param>
        /// <param name="onfound">called when compare found</param>
        /// <param name="dump">print all instructions to FileLog.Log</param>
        /// <returns></returns>
        public static IEnumerable<CodeInstruction> TranspilerHelper(IEnumerable<CodeInstruction> code, CodeInstruction[] compare, Func<LinkedList<CodeInstruction>, IEnumerable<CodeInstruction>> onfound, bool dump = false)
        {
            if (dump)
                FileLog.Log("TranspilerHelper starting");
            LinkedList<CodeInstruction> prev = new LinkedList<CodeInstruction>();
            Comparator cmptor = new Comparator();
            foreach (CodeInstruction c in code)
            {
                prev.AddLast(c);
                if (prev.Count == compare.Length)
                {
                    if (prev.SequenceEqual(compare, cmptor))
                    {
                        if (dump)
                        {
                            FileLog.Log("found");
                            foreach (CodeInstruction p in prev)
                                Dump(p);
                            FileLog.Log("replacement");
                        }
                        foreach (CodeInstruction t in onfound(prev))
                        {
                            Dump(t);
                            yield return t;
                        }
                        if (dump)
                            FileLog.Log("end found");
                    }
                }

                if (prev.Count == compare.Length)
                {
                    Dump(prev.First.Value);
                    yield return prev.First.Value;
                    prev.RemoveFirst();
                }
            }
            while (prev.Count > 0)
            {
                Dump(prev.First.Value);
                yield return prev.First.Value;
                prev.RemoveFirst();
            }
            if (dump)
                FileLog.Log("TranspilerHelper done");

            void Dump(CodeInstruction c)
            {
                if (!dump)
                    return;
                string op;
                if (c.operand == null)
                    op = "null";
                else if (c.operand is MethodBase minf)
                    op = minf.FullName();
                else if (c.operand is FieldInfo finf)
                    op = finf.FieldType.FullName + " " + finf.DeclaringType.FullName + "." + finf.Name;
                else
                    op = c.operand.ToString();
                FileLog.Log($"{c.opcode}: [{(c.operand != null ? c.operand.GetType().FullName : "null")}] {op}");
            }
        }
    }
}
