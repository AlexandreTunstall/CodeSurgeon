using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSurgeon.Module
{
    internal class ModuleImporter
    {
        private static readonly IResolutionScope Scope = new DummyResolutionScope();

        private Dictionary<UTF8String, ModuleModification> modules;
        private ITokenTransformer transformer;
        
        public StandardPatch CreatePatch(ModuleDef module, string patchName)
        {
            StandardPatch patch = new StandardPatch(patchName ?? module.Name);
            modules = new Dictionary<UTF8String, ModuleModification>();
            transformer = new ModuleTokenTransformer(this, module);
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
            if (!(Import(def) is TypeModification mod)) return;
            mod.Attributes = def.Attributes;
            mod.BaseType = Import(def.BaseType.ResolveTypeDef());
            foreach (FieldDef field in def.Fields) Populate(field);
            foreach (MethodDef method in def.Methods) Populate(method);
            foreach (PropertyDef property in def.Properties) Populate(property);
            foreach (EventDef @event in def.Events) Populate(@event);
        }

        private void Populate(FieldDef def)
        {
            if (!(Import(def) is FieldModification mod)) return;
            mod.Attributes = def.Attributes;
        }

        private void Populate(MethodDef def)
        {
            if (!(Import(def) is MethodModification mod)) return;
            mod.Attributes = def.Attributes;
            mod.Body = def.Body;
            mod.TokenTransformer = transformer;
        }

        private void Populate(PropertyDef def)
        {
            if (!(Import(def) is PropertyModification mod)) return;
            mod.Attributes = def.Attributes;
            foreach (MethodDef accessor in def.GetMethods) mod.Get(Import(accessor));
            foreach (MethodDef accessor in def.SetMethods) mod.Set(Import(accessor));
            foreach (MethodDef accessor in def.OtherMethods) mod.Other(Import(accessor));
        }

        private void Populate(EventDef def)
        {
            if (!(Import(def) is EventModification mod)) return;
            mod.Attributes = def.Attributes;
            mod.AddMethod = Import(def.AddMethod);
            mod.RemoveMethod = Import(def.RemoveMethod);
            mod.InvokeMethod = Import(def.InvokeMethod);
            foreach (MethodDef accessor in def.OtherMethods) mod.Other(Import(accessor));
        }

        internal TypeModification Import(TypeDef def) => def?.GetModificationKind() is ModificationKind kind ? def.IsNested ? Import(def.DeclaringType).NestedType(def.GetTargetName(), kind) : modules[def.GetTargetModule()].Type(def.GetTargetName().ExtractNamespace(out UTF8String name), name, kind) : null;
        internal FieldModification Import(FieldDef def) => def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Field(def.Name, Import(def.FieldSig), kind) : null;
        internal MethodModification Import(MethodDef def) => def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Method(def.Name, Import(def.MethodSig), kind) : null;
        internal PropertyModification Import(PropertyDef def) => def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Property(def.Name, Import(def.PropertySig), kind) : null;
        internal EventModification Import(EventDef def) => def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Event(def.Name, Import(def.EventType.ResolveTypeDefThrow()), kind) : null;

        internal ITypeDefOrRef Import(ITypeDefOrRef type) => Import(type.ResolveTypeDef()) is TypeModification mod ? new TypeRefUser(new ModuleDefUser(mod.Module.Name), mod.Namespace, mod.Name, Scope) : type;

        internal TypeSig Import(TypeSig sig) => sig.ApplyToLeaf(Import);
        internal LeafSig Import(LeafSig sig)
        {
            switch (sig)
            {
                case TypeDefOrRefSig type:
                    return Import(type);
                case GenericSig generic:
                    return Import(generic);
                case SentinelSig sentinel:
                    return Import(sentinel);
                case FnPtrSig fn:
                    return Import(fn);
                case GenericInstSig inst:
                    return Import(inst);
            }
            throw new NotImplementedException("unknown LeafSig type " + sig.GetType().Name);
        }
        internal TypeDefOrRefSig Import(TypeDefOrRefSig sig)
        {
            switch (sig)
            {
                case CorLibTypeSig cor:
                    return Import(cor);
                case ClassOrValueTypeSig type:
                    return Import(type);
            }
            throw new NotImplementedException("unknown TypeDefOrRefSig type " + sig.GetType().Name);
        }
        internal CorLibTypeSig Import(CorLibTypeSig sig) => new CorLibTypeSig(Import(sig.TypeDefOrRef), sig.ElementType);
        internal ClassOrValueTypeSig Import(ClassOrValueTypeSig sig)
        {
            switch (sig)
            {
                case ValueTypeSig value:
                    return Import(value);
                case ClassSig @class:
                    return Import(@class);
            }
            throw new NotImplementedException("unknown ClassOrValueTypeSig type " + sig.GetType().Name);
        }
        internal ValueTypeSig Import(ValueTypeSig sig) => new ValueTypeSig(Import(sig.TypeDefOrRef));
        internal ClassSig Import(ClassSig sig) => new ClassSig(Import(sig.TypeDefOrRef));
        internal GenericSig Import(GenericSig sig)
        {
            switch (sig)
            {
                case GenericVar type:
                    return Import(type);
                case GenericMVar method:
                    return Import(method);
            }
            throw new NotImplementedException("unknown GenericSig type " + sig.GetType().Name);
        }
        internal GenericVar Import(GenericVar sig) => sig;
        internal GenericMVar Import(GenericMVar sig) => sig;
        internal SentinelSig Import(SentinelSig sig) => sig;
        internal FnPtrSig Import(FnPtrSig sig) => new FnPtrSig(Import(sig.Signature));
        internal GenericInstSig Import(GenericInstSig sig) => new GenericInstSig(Import(sig.GenericType), sig.GenericArguments.Select(Import).ToList());

        internal CallingConventionSig Import(CallingConventionSig sig)
        {
            switch (sig)
            {
                case FieldSig field:
                    return Import(field);
                case MethodSig method:
                    return Import(method);
                case PropertySig property:
                    return Import(property);
                case LocalSig local:
                    return Import(local);
                case GenericInstMethodSig inst:
                    return Import(inst);
            }
            throw new NotImplementedException("unknown CallingConventionSig type " + sig.GetType().Name);
        }
        internal FieldSig Import(FieldSig sig) => new FieldSig(Import(sig.Type));
        internal MethodSig Import(MethodSig sig) => new MethodSig(sig.CallingConvention, sig.GenParamCount, Import(sig.RetType), sig.Params.Select(Import).ToList(), sig.ParamsAfterSentinel?.Select(Import).ToList());
        internal PropertySig Import(PropertySig sig) => new PropertySig(sig.HasThis, Import(sig.RetType), sig.Params.Select(Import).ToArray());
        internal LocalSig Import(LocalSig sig) => new LocalSig(sig.Locals.Select(Import).ToList());
        internal GenericInstMethodSig Import(GenericInstMethodSig sig) => new GenericInstMethodSig(sig.GenericArguments.Select(Import).ToList());
    }
}
