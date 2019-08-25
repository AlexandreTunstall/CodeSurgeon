using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSurgeon
{
    public interface IReference<out TSymbol> : IResolvable<TSymbol> where TSymbol : class, IFullName
    {
        UTF8String FullName { get; }
        SymbolKind SymbolKind { get; }
    }

    public enum SymbolKind
    {
        Assembly,
        Module,
        Type,
        Field,
        Method,
        Property,
        Event
    }

    public interface IAssemblyReference<out TSymbol> : IReference<TSymbol>, IEquatable<IAssemblyReference<IAssembly>> where TSymbol : class, IAssembly
    {
        UTF8String Name { get; }
    }

    public interface IModuleReference<out TSymbol> : IReference<TSymbol>, IEquatable<IModuleReference<IModule>> where TSymbol : class, IModule
    {
        UTF8String Name { get; }
    }

    public interface IMemberReference<out TSymbol> : IReference<TSymbol> where TSymbol : class, IMemberRef
    {
        ITypeReference<ITypeDefOrRef> DeclaringType { get; }
        UTF8String Name { get; }
    }

    public interface ITypeReference<out TSymbol> : IMemberReference<TSymbol>, IEquatable<ITypeReference<ITypeDefOrRef>> where TSymbol : class, ITypeDefOrRef
    {
        IModuleReference<IModule> Module { get; }
        IReference<IResolutionScope> Scope { get; }
        UTF8String Namespace { get; }

        bool IsNested { get; }
    }

    public interface IFieldReference<out TSymbol> : IMemberReference<TSymbol>, IEquatable<IFieldReference<IField>> where TSymbol : class, IField
    {
        FieldSignature Signature { get; }
    }

    public interface IMethodReference<out TSymbol> : IMemberReference<TSymbol>, IEquatable<IMethodReference<IMethod>> where TSymbol : class, IMethod
    {
        MethodSignature Signature { get; }
    }

    public sealed class AssemblyReference : IAssemblyReference<AssemblyRef>
    {
        public UTF8String Name { get; }
        public Version Version { get; }
        public PublicKeyBase PublicKey { get; }
        public UTF8String Locale { get; }

        public UTF8String FullName => Name;

        public SymbolKind SymbolKind => SymbolKind.Assembly;

        public AssemblyReference(UTF8String name, Version version, PublicKeyBase publicKey, UTF8String locale)
        {
            Name = name;
            Version = version;
            PublicKey = publicKey;
            Locale = locale;
        }

        public AssemblyRef Resolve(ISearchContext context) => new AssemblyRefUser(Name, Version, PublicKey, Locale);

        public bool Equals(IAssemblyReference<IAssembly> other) => !(other is null) && Name.Compare(other.Name);
    }

    public sealed class ModuleReference : IModuleReference<ModuleRef>
    {
        public UTF8String Name { get; }

        public UTF8String FullName => Name;
        public SymbolKind SymbolKind => SymbolKind.Module;

        public ModuleReference(UTF8String name) => Name = name;

        public ModuleRef Resolve(ISearchContext context) => new ModuleRefUser(null, Name);

        public bool Equals(IModuleReference<IModule> other) => Equals(this, other);
        internal static bool Equals(IModuleReference<IModule> @this, IModuleReference<IModule> other) => !(other is null) && @this.Name == other.Name;
    }

    public abstract class AbstractMemberReference<TSymbol> : IMemberReference<TSymbol> where TSymbol : class, IMemberRef
    {
        public ITypeReference<ITypeDefOrRef> DeclaringType { get; }
        public UTF8String Name { get; }

        public abstract UTF8String FullName { get; }
        public abstract SymbolKind SymbolKind { get; }

        private protected AbstractMemberReference(ITypeReference<ITypeDefOrRef> declaringType, UTF8String name)
        {
            DeclaringType = declaringType;
            Name = name;
        }

        public abstract TSymbol Resolve(ISearchContext context);

        internal static bool Equals(IMemberReference<TSymbol> @this, IMemberReference<TSymbol> other) => !(other is null) && @this.DeclaringType.Compare(other.DeclaringType) && @this.Name == other.Name;
    }

    public sealed class TypeReference : AbstractMemberReference<TypeRef>, ITypeReference<TypeRef>
    {
        public IModuleReference<ModuleDef> Module { get; }
        public IReference<IResolutionScope> Scope { get; }
        public UTF8String Namespace { get; }
        
        public override UTF8String FullName => this.ToFullName();
        public override SymbolKind SymbolKind => SymbolKind.Type;
        public bool IsNested => Namespace is null;

        public TypeReference(IModuleReference<ModuleDef> module, UTF8String @namespace, UTF8String name, IReference<IResolutionScope> scope) : this((ITypeReference<ITypeDefOrRef>)null, @namespace, name)
        {
            if (@namespace is null) throw new ArgumentNullException(nameof(@namespace), nameof(@namespace) + " cannot be null, use an empty string for the default namespace instead");
            Module = module;
            Scope = scope;
            Namespace = @namespace;
        }

        public TypeReference(ITypeReference<ITypeDefOrRef> declaringType, UTF8String name) : this(declaringType, null, name)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            Scope = declaringType.Scope;
        }

        private TypeReference(ITypeReference<ITypeDefOrRef> declaringType, UTF8String @namespace, UTF8String name) : base(declaringType, name)
        {
            Namespace = @namespace;
        }

        public override TypeRef Resolve(ISearchContext context) => IsNested ? ResolveNested(context) : new TypeRefUser(Module is null ? null : context.Get(Module), Namespace, Name, context.Get(Scope));

        private TypeRef ResolveNested(ISearchContext context)
        {
            ITypeReference<ITypeDefOrRef> parent;
            List<UTF8String> nameParts = new List<UTF8String>()
            {
                Name
            };
            for (parent = DeclaringType; parent.IsNested; parent = parent.DeclaringType)
            {
                nameParts.Add("/");
                nameParts.Add(parent.Name);
            }
            return new TypeRefUser(context.Get(parent).Module, parent.Namespace, UTF8String.Empty.Concat(nameParts), context.Get(Scope));
        }

        public bool Equals(ITypeReference<ITypeDefOrRef> other) => Equals(this, other);
        internal static bool Equals(ITypeReference<ITypeDefOrRef> @this, ITypeReference<ITypeDefOrRef> other) => !(other is null) && (@this.IsNested ? other.IsNested && @this.DeclaringType.Equals(other.DeclaringType) : !other.IsNested && @this.Module.Compare(other.Module) && @this.Namespace == other.Namespace) && @this.Name == other.Name;

        IModuleReference<IModule> ITypeReference<TypeRef>.Module => Module;
    }

    public sealed class FieldReference : AbstractMemberReference<MemberRef>, IFieldReference<MemberRef>
    {
        public FieldSignature Signature { get; }

        public override UTF8String FullName => this.ToFullName();
        public override SymbolKind SymbolKind => SymbolKind.Field;

        public FieldReference(ITypeReference<ITypeDefOrRef> declaringType, UTF8String name, FieldSignature signature) : base(declaringType, name) => Signature = signature;

        public override MemberRef Resolve(ISearchContext context) => new MemberRefUser(null, Name, context.Get(Signature), context.Get(DeclaringType));

        public bool Equals(IFieldReference<IField> other) => Equals(this, other);
        internal static bool Equals(IFieldReference<IField> @this, IFieldReference<IField> other) => AbstractMemberReference<IField>.Equals(@this, other) && @this.Signature.Compare(other.Signature);
    }

    public sealed class MethodReference : AbstractMemberReference<MemberRef>, IMethodReference<MemberRef>
    {
        public MethodSignature Signature { get; }

        public override UTF8String FullName => this.ToFullName();
        public override SymbolKind SymbolKind => SymbolKind.Method;

        public MethodReference(ITypeReference<ITypeDefOrRef> declaringType, UTF8String name, MethodSignature signature) : base(declaringType, name) => Signature = signature;

        public override MemberRef Resolve(ISearchContext context) => new MemberRefUser(null, Name, context.Get(Signature), context.Get(DeclaringType));

        public bool Equals(IMethodReference<IMethod> other) => Equals(this, other);
        internal static bool Equals(IMethodReference<IMethod> @this, IMethodReference<IMethod> other) => AbstractMemberReference<IMethod>.Equals(@this, other);
    }

    public sealed class TypeSpecification : AbstractMemberReference<TypeSpec>, ITypeReference<TypeSpec>
    {
        public ITypeSignature<TypeSig> Signature { get; }

        public IModuleReference<IModule> Module => throw new NotImplementedException();
        public IReference<IResolutionScope> Scope => throw new NotImplementedException();
        public UTF8String Namespace => throw new NotImplementedException();
        public override UTF8String FullName => this.ToFullName();
        public override SymbolKind SymbolKind => SymbolKind.Type;

        public bool IsNested => throw new NotImplementedException();

        public TypeSpecification(ITypeSignature<TypeSig> signature) : base(GetDeclaringType(signature), null) => Signature = signature;

        public override TypeSpec Resolve(ISearchContext context) => new TypeSpecUser(context.Get(Signature));

        public bool Equals(ITypeReference<ITypeDefOrRef> other)
        {
            throw new NotImplementedException();
        }

        private static ITypeReference<ITypeDefOrRef> GetDeclaringType(ITypeSignature<TypeSig> signature)
        {
            if (signature is GenericInstSignature generic) signature = generic.GenericType;
            return signature is ITypeDefOrRefSignature<TypeDefOrRefSig> type ? type.Type.DeclaringType : null;
        }
    }
}
