using HarmonyLib;
using System;
using System.Reflection;

namespace AccessExtension
{
    public class PropertySet : MethodCall
    {
        public PropertySet(PropertyInfo i) : base(i.SetMethod)
        {
        }

        public PropertySet(Type t, string m) : this(AccessTools.Property(t, m))
        {
        }

        public PropertySet(string dll, string type, string property) : base(new AEPData<MethodInfo>(() => AccessTools.Property(AccessExtensionPatcher.GetLoadedAssemblyByName(dll).GetType(type), property).SetMethod))
        {
        }
    }
}
