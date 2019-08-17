using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSurgeon.Module
{
    internal class ModuleImporter
    {
        private readonly Dictionary<UTF8String, ModuleModification> modules;

        public StandardPatch CreatePatch(ModuleDef module, string patchName)
        {
            StandardPatch patch = new StandardPatch(patchName ?? module.Name);
            Dictionary<UTF8String, ModuleModification> modules = new Dictionary<UTF8String, ModuleModification>();
            foreach (CustomAttribute attribute in module.CustomAttributes.FindAll(ModuleExtensions.Required))
            {
                UTF8String name = attribute.ConstructorArguments[0].ExtractName();
                modules.Add(name, patch.Module(name, ModificationKind.FailIfMissing, (bool)attribute.ConstructorArguments[1].Value));
            }
            for (Stack<TypeDef> types = new Stack<TypeDef>(module.Types); types.Count > 0;)
            {
                TypeDef type = types.Pop();
                Populate(type);
                foreach (TypeDef nested in type.NestedTypes) types.Push(nested);
            }
            return patch;
        }

        private void Populate(TypeDef def)
        {
            if (!(Import(def) is var mod)) return;
            mod.Attributes = def.Attributes;
            foreach (FieldDef field in def.Fields) Populate(field);
            foreach (MethodDef method in def.Methods) Populate(method);
            foreach (PropertyDef property in def.Properties) Populate(property);
            foreach (EventDef @event in def.Events) Populate(@event);
        }

        private void Populate(FieldDef def)
        {
            if (!(Import(def) is var mod)) return;
            mod.Attributes = def.Attributes;
        }

        private void Populate(MethodDef def)
        {
            if (!(Import(def) is var mod)) return;
            mod.Attributes = def.Attributes;
        }

        private void Populate(PropertyDef def)
        {
            if (!(Import(def) is var mod)) return;
            mod.Attributes = def.Attributes;
            foreach (MethodDef accessor in def.GetMethods) mod.Get(Import(accessor));
            foreach (MethodDef accessor in def.SetMethods) mod.Set(Import(accessor));
            foreach (MethodDef accessor in def.OtherMethods) mod.Other(Import(accessor));
        }

        private void Populate(EventDef def)
        {
            if (!(Import(def) is var mod)) return;
            mod.Attributes = def.Attributes;
            mod.AddMethod = Import(def.AddMethod);
            mod.RemoveMethod = Import(def.RemoveMethod);
            mod.InvokeMethod = Import(def.InvokeMethod);
            foreach (MethodDef accessor in def.OtherMethods) mod.Other(Import(accessor));
        }

        private TypeModification Import(TypeDef def) => def?.GetModificationKind() is ModificationKind kind ? modules[def.GetTargetModule()].Type(def.GetTargetName().ExtractNamespace(out UTF8String name), name, kind) : null;
        private FieldModification Import(FieldDef def) => def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Field(def.Name, def.FieldSig, kind) : null;
        private MethodModification Import(MethodDef def) => def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Method(def.Name, def.MethodSig, kind) : null;
        private PropertyModification Import(PropertyDef def) => def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Property(def.Name, def.PropertySig, kind) : null;
        private EventModification Import(EventDef def) => def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Event(def.Name, Import(def.EventType.ResolveTypeDefThrow()), kind) : null;
    }
}
