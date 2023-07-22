using HarmonyLib;
using System;
using System.Reflection;

namespace AccessExtension
{
    public class FieldGet : Attribute
    {
        private AEPData<FieldInfo> d;

        internal FieldInfo Field
        {
            get => d.Data;
        }

        public FieldGet(FieldInfo i)
        {
            d = new AEPData<FieldInfo>(i);
        }

        public FieldGet(Type t, string f)
        {
            d = new AEPData<FieldInfo>(AccessTools.Field(t, f));
        }

        public FieldGet(string dll, string type, string field)
        {
            d = new AEPData<FieldInfo>(() => AccessTools.Field(AccessExtensionPatcher.GetLoadedAssemblyByName(dll).GetType(type), field));
        }

    }
}
