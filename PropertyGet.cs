using Harmony;
using System;
using System.Reflection;

namespace AccessExtension
{
    public class PropertyGet : MethodCall
    {
        public PropertyGet(PropertyInfo i) : base(i.GetMethod)
        {
        }

        public PropertyGet(Type t, string m) : this(AccessTools.Property(t, m))
        {
        }

        public PropertyGet(string dll, string type, string property) : base(new AEPData<MethodInfo>(() => AccessTools.Property(AccessExtensionPatcher.GetLoadedAssemblyByName(dll).GetType(type), property).GetMethod))
        {
        }
    }
}
