using HarmonyLib;
using System;
using System.Reflection;

namespace AccessExtension
{
    public class MethodCall : Attribute
    {
        private AEPData<MethodInfo> d;

        internal MethodInfo Method
        {
            get => d.Data;
        }

        public MethodCall(MethodInfo i)
        {
            d = new AEPData<MethodInfo>(i);
        }

        public MethodCall(Type t, string m, Type[] p = null, Type[] generics = null) : this(AccessTools.Method(t, m, p))
        {
        }

        internal MethodCall(AEPData<MethodInfo> a)
        {
            d = a;
        }

        public MethodCall(string dll, string type, string method, Type[] p = null, Type[] generics = null)
        {
            d = new AEPData<MethodInfo>(() => AccessTools.Method(AccessExtensionPatcher.GetLoadedAssemblyByName(dll).GetType(type), method, p, generics));
        }
    }
}
