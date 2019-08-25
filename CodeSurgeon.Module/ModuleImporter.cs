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
        private Dictionary<UTF8String, ModuleModification> modules;
        private ITokenTransformer transformer;

        private readonly Dictionary<MethodDef, Action<MethodModification>> listeners = new Dictionary<MethodDef, Action<MethodModification>>();

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

        public ModuleModification GetModule(UTF8String name) => modules[name];
        public bool TryGetModule(UTF8String name, out ModuleModification module) => modules.TryGetValue(name, out module);

        private void Populate(TypeDef def)
        {
            if (!(Import(def) is TypeModification mod)) return;
            mod.Attributes = def.Attributes;
            mod.GenericParameters = def.GenericParameters.Select(Import).ToList();
            mod.BaseType = Import(def.BaseType);
            foreach (InterfaceImpl type in def.Interfaces) mod.BaseInterface(Import(type));
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
            foreach (MethodOverride @override in def.Overrides) mod.Override(Import(@override));
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

        internal ModuleModification Import(ModuleDef def) => def is null ? null : TryGetModule(def.Name, out ModuleModification imported) ? imported : null;
        internal TypeModification Import(TypeDef def) => def?.GetModificationKind() is ModificationKind kind ? def.IsNested ? Import(def.DeclaringType).NestedType(def.GetTargetName(), kind) : GetModule(def.GetTargetModule()).Type(def.GetTargetName().ExtractNamespace(out UTF8String name), name, kind) : null;
        internal FieldModification Import(FieldDef def) => def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Field(def.GetTargetName(), Import(def.FieldSig), kind) : null;
        internal MethodModification Import(MethodDef def)
        {
            MethodModification result = def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Method(def.GetTargetName(), Import(def.MethodSig), kind) : null;
            lock (listeners)
            {
                if (def != null && listeners.TryGetValue(def, out Action<MethodModification> listener))
                {
                    listener(result);
                    listeners.Remove(def);
                }
            }
            return result;
        }
        internal PropertyModification Import(PropertyDef def) => def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Property(def.GetTargetName(), Import(def.PropertySig), kind) : null;
        internal EventModification Import(EventDef def) => def?.GetModificationKind() is ModificationKind kind ? Import(def.DeclaringType).Event(def.GetTargetName(), Import(def.EventType.ResolveTypeDefThrow()), kind) : null;

        internal AssemblyReference Import(AssemblyRef @ref) => new AssemblyReference(@ref.Name, @ref.Version, @ref.PublicKeyOrToken, @ref.Culture);
        internal ModuleReference Import(ModuleRef @ref) => new ModuleReference(@ref.Name);

        internal TypeSpecification Import(TypeSpec spec) => new TypeSpecification(Import(spec.TypeSig));
        internal TypeReference Import(TypeRef @ref) => new TypeReference(Import(@ref.Module), @ref.Namespace, @ref.Name, Import(@ref.ResolutionScope));

        internal ITypeReference<ITypeDefOrRef> Import(ITypeDefOrRef type) => type is TypeSpec spec ? Import(spec) : (ITypeReference<ITypeDefOrRef>)Import(type.ResolveTypeDef()) ?? Import(type as TypeRef);
        internal IFieldReference<IField> Import(IField field) => (IFieldReference<IField>)Import(field.ResolveFieldDef()) ?? new FieldReference(Import(field.DeclaringType), field.Name, Import(field.FieldSig));
        internal IMethodReference<IMethodDefOrRef> Import(IMethodDefOrRef method) => (IMethodReference<IMethodDefOrRef>)Import(method.ResolveMethodDef()) ?? new MethodReference(Import(method.DeclaringType), method.Name, Import(method.MethodSig));

        internal IReference<IResolutionScope> Import(IResolutionScope scope)
        {
            switch (scope)
            {
                case ModuleDef def:
                    return Import(def);
                case ModuleRef module:
                    return Import(module);
                case AssemblyRef assembly:
                    return Import(assembly);
                case TypeRef type:
                    return Import(type);
            }
            throw new NotImplementedException("unknown resolution scope type " + scope.GetType().Name);
        }

        internal GenericParameter Import(GenericParam param) => new GenericParameter(param.Number, param.Flags, param.Name, param.GenericParamConstraints.Select(Import));
        internal GenericParameterConstraint Import(GenericParamConstraint constraint) => new GenericParameterConstraint(Import(constraint.Constraint));

        internal InterfaceImplementation Import(InterfaceImpl impl) => new InterfaceImplementation(Import(impl.Interface));
        internal InterfaceMethodOverride Import(MethodOverride @override) => new InterfaceMethodOverride(Import(@override.MethodBody), Import(@override.MethodDeclaration));

        public ITypeSignature<TypeSig> Import(TypeSig sig)
        {
            switch (sig)
            {
                case LeafSig leaf:
                    return Import(leaf);
                case NonLeafSig nonLeaf:
                    return Import(nonLeaf);
            }
            throw new NotImplementedException("unknown TypeSig type " + sig.GetType().Name);
        }

        public ILeafSignature<LeafSig> Import(LeafSig sig)
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
        public ITypeDefOrRefSignature<TypeDefOrRefSig> Import(TypeDefOrRefSig sig)
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
        public CorLibTypeSignature Import(CorLibTypeSig sig) => new CorLibTypeSignature(Import(sig.TypeDefOrRef), sig.ElementType);
        public IClassOrValueTypeSignature<ClassOrValueTypeSig> Import(ClassOrValueTypeSig sig)
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
        public ValueTypeSignature Import(ValueTypeSig sig) => new ValueTypeSignature(Import(sig.TypeDefOrRef));
        public ClassSignature Import(ClassSig sig) => new ClassSignature(Import(sig.TypeDefOrRef));
        public IGenericSignature<GenericSig> Import(GenericSig sig)
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
        public GenericVarSignature Import(GenericVar sig) => new GenericVarSignature(sig.Number, Import(sig.OwnerType));
        public GenericMVarSignature Import(GenericMVar sig) => new GenericMVarSignature(sig.Number, list =>
        {
            lock (listeners)
            {
                if (listeners.TryGetValue(sig.OwnerMethod, out Action<MethodModification> existing)) listeners[sig.OwnerMethod] = def =>
                {
                    existing(def);
                    list(def);
                };
                else listeners.Add(sig.OwnerMethod, list);
            }
        });
        public SentinelSignature Import(SentinelSig sig) => new SentinelSignature();
        public FnPtrSignature Import(FnPtrSig sig) => new FnPtrSignature(Import(sig.Signature));
        public GenericInstSignature Import(GenericInstSig sig) => new GenericInstSignature(Import(sig.GenericType), sig.GenericArguments.Select(Import).ToList());
        public INonLeafSignature<NonLeafSig> Import(NonLeafSig sig)
        {
            switch (sig)
            {
                case PtrSig ptr:
                    return Import(ptr);
                case ByRefSig byRef:
                    return Import(byRef);
                case ArraySigBase array:
                    return Import(array);
                case ModifierSig modifier:
                    return Import(modifier);
                case PinnedSig pinned:
                    return Import(pinned);
                case ValueArraySig va:
                    return Import(va);
                case ModuleSig module:
                    return Import(module);
            }
            throw new NotImplementedException("unknown NonLeafSig type " + sig.GetType().Name);
        }
        public PtrSignature Import(PtrSig sig) => new PtrSignature(Import(sig.Next));
        public ByRefSignature Import(ByRefSig sig) => new ByRefSignature(Import(sig.Next));
        public IArrayBaseSignature<ArraySigBase> Import(ArraySigBase sig)
        {
            switch (sig)
            {
                case ArraySig array:
                    return Import(array);
                case SZArraySig sz:
                    return Import(sz);
            }
            throw new NotImplementedException("unknown ArraySigBase type " + sig.GetType().Name);
        }
        public ArraySignature Import(ArraySig sig) => new ArraySignature(Import(sig.Next), sig.Rank, sig.Sizes, sig.LowerBounds);
        public SZArraySignature Import(SZArraySig sig) => new SZArraySignature(Import(sig.Next));
        public IModifierSignature<ModifierSig> Import(ModifierSig sig)
        {
            switch (sig)
            {
                case CModReqdSig reqd:
                    return Import(reqd);
                case CModOptSig opt:
                    return Import(opt);
            }
            throw new NotImplementedException("unknown ModifierSig type " + sig.GetType().Name);
        }
        public CModReqdSignature Import(CModReqdSig sig) => new CModReqdSignature(Import(sig.Modifier), Import(sig.Next));
        public CModOptSignature Import(CModOptSig sig) => new CModOptSignature(Import(sig.Modifier), Import(sig.Next));
        public PinnedSignature Import(PinnedSig sig) => new PinnedSignature(Import(sig.Next));
        public ValueArraySignature Import(ValueArraySig sig) => new ValueArraySignature(Import(sig.Next), sig.Size);
        public ModuleSignature Import(ModuleSig sig) => new ModuleSignature(sig.Index, Import(sig.Next));

        public ICallSignature<CallingConventionSig> Import(CallingConventionSig sig)
        {
            switch (sig)
            {
                case FieldSig field:
                    return Import(field);
                case MethodBaseSig method:
                    return Import(method);
                case LocalSig local:
                    return Import(local);
                case GenericInstMethodSig inst:
                    return Import(inst);
            }
            throw new NotImplementedException("unknown CallingConventionSig type " + sig.GetType().Name);
        }
        public FieldSignature Import(FieldSig sig) => new FieldSignature(Import(sig.Type));
        public IMethodBaseSignature<MethodBaseSig> Import(MethodBaseSig sig)
        {
            switch (sig)
            {
                case MethodSig method:
                    return Import(method);
                case PropertySig property:
                    return Import(property);
            }
            throw new NotImplementedException("unknown MethodBaseSig type " + sig.GetType().Name);
        }
        public MethodSignature Import(MethodSig sig) => new MethodSignature(sig.CallingConvention, sig.GenParamCount, Import(sig.RetType), sig.Params.Select(Import), sig.ParamsAfterSentinel?.Select(Import));
        public PropertySignature Import(PropertySig sig) => new PropertySignature(sig.HasThis, Import(sig.RetType), sig.Params.Select(Import));
        public LocalSignature Import(LocalSig sig) => new LocalSignature(sig.Locals.Select(Import));
        public GenericInstMethodSignature Import(GenericInstMethodSig sig) => new GenericInstMethodSignature(sig.GenericArguments.Select(Import));
    }
}
