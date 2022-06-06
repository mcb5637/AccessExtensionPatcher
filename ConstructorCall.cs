using Harmony;
using System;
using System.Reflection;

namespace AccessExtension
{
    public class ConstructorCall : Attribute
    {
        private AEPData<ConstructorInfo> d;

        internal ConstructorInfo Constructor
        {
            get => d.Data;
        }

        public ConstructorCall(ConstructorInfo i)
        {
            d = new AEPData<ConstructorInfo>(i);
        }

        public ConstructorCall(Type t, Type[] p = null) : this(AccessTools.Constructor(t, p))
        {
        }

        public ConstructorCall(string dll, string type, Type[] p = null)
        {
            d = new AEPData<ConstructorInfo>(() => AccessTools.Constructor(AccessExtensionPatcher.GetLoadedAssemblyByName(dll).GetType(type), p));
        }
    }
}
