using HarmonyLib;
using System;
using System.Reflection;

namespace AccessExtension
{
    public class FieldSet : Attribute
    {
        private AEPData<FieldInfo> d;

        internal FieldInfo Field
        {
            get => d.Data;
        }

        public FieldSet(FieldInfo i)
        {
            d = new AEPData<FieldInfo>(i);
        }

        public FieldSet(Type t, string f)
        {
            d = new AEPData<FieldInfo>(AccessTools.Field(t, f));
        }

        public FieldSet(string dll, string type, string field)
        {
            d = new AEPData<FieldInfo>(() => AccessTools.Field(AccessExtensionPatcher.GetLoadedAssemblyByName(dll).GetType(type), field));
        }
    }
}
