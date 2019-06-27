using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSurgeon
{
    public interface IModification
    {
        ModificationKind Kind { get; }
        UTF8String FullName { get; }
        SymbolKind SymbolKind { get; }
    }

    public interface IModificationBuilder<out TMod> where TMod : IModification
    {
        ModificationKind Kind { get; }

        TMod Build();
    }

    public enum ModificationKind
    {
        FailIfPresent,
        FailIfMissing,
        CreateIfMissing
    }

    public enum SymbolKind
    {
        Type,
        Field,
        Method,
        Property,
        Event
    }

    public abstract class AbstractModification<TSymbol> : IModification where TSymbol : AbstractModification<TSymbol>
    {
        private protected const SigComparerOptions SearchOptions = SigComparerOptions.DontCompareTypeScope | SigComparerOptions.IgnoreModifiers | SigComparerOptions.DontCompareReturnType | SigComparerOptions.PrivateScopeIsComparable;
        private protected static readonly SigComparer Comparer = new SigComparer(SearchOptions);

        public ModificationKind Kind { get; }
        public bool ReadOnly { get; }

        public abstract UTF8String FullName { get; }
        public abstract SymbolKind SymbolKind { get; }

        private protected abstract TSymbol This { get; }

        private protected AbstractModification(ModificationKind kind, bool readOnly)
        {
            if (!(this is TSymbol)) throw new InvalidOperationException("derived type must inherit TSymbol");
            Kind = kind;
            ReadOnly = readOnly;
        }

        private protected bool CheckExistence(IDnlibDef target)
        {
            switch (Kind)
            {
                case ModificationKind.FailIfPresent:
                    if (target != null) throw new SymbolInstallException<TSymbol>(This, new InstallException("a definition for this symbol already exists"));
                    return true;
                case ModificationKind.FailIfMissing:
                    if (target == null) throw new SymbolInstallException<TSymbol>(This, new InstallException("could not find a definition for this symbol"));
                    return false;
                case ModificationKind.CreateIfMissing:
                    return target != null;
                default:
                    throw new SymbolInstallException<TSymbol>(This, new InstallException("unknown modification kind " + Kind));
            }
        }

        private protected void BeginModify()
        {
            if (ReadOnly) throw new ReadOnlyInstallException();
        }
    }

    public abstract class AccessibleModification<TSymbol> : AbstractModification<TSymbol> where TSymbol : AccessibleModification<TSymbol>
    {
        internal abstract AccessLevel AccessLevel { get; }

        private protected AccessibleModification(ModificationKind kind, bool readOnly) : base(kind, readOnly) { }

        private protected bool CheckAccessLevel(AccessLevel actual)
        {
            switch (AccessLevel)
            {
                case AccessLevel.Private:
                    if (actual == AccessLevel.Private) return true;
                    goto case AccessLevel.FamilyAndAssembly;
                case AccessLevel.FamilyAndAssembly:
                    switch (actual)
                    {
                        case AccessLevel.FamilyAndAssembly:
                        case AccessLevel.Assembly:
                            return true;
                    }
                    goto case AccessLevel.Family;
                case AccessLevel.Assembly:
                    if (actual == AccessLevel.Assembly) return true;
                    goto case AccessLevel.FamilyOrAssembly;
                case AccessLevel.Family:
                    if (actual == AccessLevel.Family) return true;
                    goto case AccessLevel.FamilyOrAssembly;
                case AccessLevel.FamilyOrAssembly:
                    if (actual == AccessLevel.FamilyOrAssembly) return true;
                    goto case AccessLevel.Public;
                case AccessLevel.Public:
                    return actual == AccessLevel.Public;
            }
            throw new ArgumentException("unknown existing access level " + actual);
        }
    }

    internal enum AccessLevel
    {
        Private,
        FamilyAndAssembly,
        Assembly,
        Family,
        FamilyOrAssembly,
        Public
    }

    public sealed class TypeModification : AccessibleModification<TypeModification>
    {
        public UTF8String Namespace { get; }
        public UTF8String Name { get; }

        public IEnumerable<TypeModification> NestedTypes { get; }
        public IEnumerable<FieldModification> Fields { get; }
        public IEnumerable<MethodModification> Methods { get; }

        public TypeAttributes Attributes { get; }

        public override UTF8String FullName => IsNested ? Name : Namespace.Concat(".", Name);
        public override SymbolKind SymbolKind => SymbolKind.Type;
        public bool IsNested => Namespace is null;

        internal override AccessLevel AccessLevel => Attributes.GetAccessLevel(IsNested);

        private protected override TypeModification This => this;

        private TypeModification(Builder builder) : base(builder.Kind, builder.ReadOnly)
        {
            Namespace = builder.Namespace;
            Name = builder.Name;
            NestedTypes = builder.NestedTypes.ToList();
            Fields = builder.Fields.ToList();
            Methods = builder.Methods;
            Attributes = builder.Attributes;
        }

        public void Apply(TypeDef type, Action<TypeDef> add)
        {
            try
            {
                if (!CheckExistence(type)) CheckAttributes(type);
                else if (IsNested) add(type = new TypeDefUser(Name));
                else add(type = new TypeDefUser(Namespace, Name));
                foreach (TypeModification mod in NestedTypes) mod.Apply(type.NestedTypes.FirstOrDefault(nt => nt.Name == mod.Name), AddType);
                foreach (FieldModification mod in Fields) mod.Apply(type.FindField(mod.Name), AddField);
                foreach (MethodModification mod in Methods) mod.Apply(type.FindMethod(mod.Name, mod.Signature, SearchOptions), AddMethod);
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<TypeModification>(this, e);
            }

            void AddType(TypeDef def)
            {
                BeginModify();
                def.DeclaringType = type;
            }
            void AddField(FieldDef def)
            {
                BeginModify();
                def.DeclaringType = type;
            }
            void AddMethod(MethodDef def)
            {
                BeginModify();
                def.DeclaringType = type;
            }
        }

        private void CheckAttributes(TypeDef type)
        {
            TypeAttributes newAttributes = type.Attributes;
            TypeAttributes mask = TypeAttributes.VisibilityMask;
            if (!CheckAccessLevel(type.Attributes.GetAccessLevel(IsNested))) Attributes.Replace(ref newAttributes, mask);
            if (!Attributes.Equals(newAttributes, ~mask)) Attributes.Replace(ref newAttributes, ~mask);
            if (type.Attributes == newAttributes) return;
            BeginModify();
            type.Attributes = newAttributes;
        }

        public sealed class Builder : IModificationBuilder<TypeModification>
        {
            public UTF8String Namespace { get; }
            public UTF8String Name { get; }
            public ModificationKind Kind { get; }
            public bool ReadOnly { get; }

            public TypeAttributes Attributes { get; set; }

            internal IEnumerable<TypeModification> NestedTypes { get; }
            internal IEnumerable<FieldModification> Fields { get; }
            internal IEnumerable<MethodModification> Methods { get; }

            private readonly Dictionary<UTF8String, IModificationBuilder<IModification>> members = new Dictionary<UTF8String, IModificationBuilder<IModification>>();
            private readonly List<MethodModification.Builder> methods = new List<MethodModification.Builder>();

            public Builder(UTF8String @namespace, UTF8String name, ModificationKind kind, bool readOnly) : this(name, kind, readOnly)
            {
                if (@namespace is null) throw new ArgumentException("namespace cannot be null, use an empty string for the default namespace instead");
                Namespace = @namespace;
            }

            private Builder(UTF8String name, ModificationKind kind, bool readOnly)
            {
                if (name is null) throw new ArgumentException("name cannot be null");
                Name = name;
                Kind = kind;
                ReadOnly = readOnly;
                NestedTypes = members.Values.OfType<Builder>().Select(it => it.Build());
                Fields = members.Values.OfType<FieldModification.Builder>().Select(f => f.Build());
                Methods = methods.Select(m => m.Build());
            }

            public Builder NestedType(UTF8String name, ModificationKind kind) => GetOrCreate(name, kind, (n, k) => new Builder(n, k, ReadOnly));
            public FieldModification.Builder Field(UTF8String name, FieldSig signature, ModificationKind kind) => GetOrCreate(name, kind, (n, k) => new FieldModification.Builder(n, signature, k, ReadOnly));
            public MethodModification.Builder Method(UTF8String name, MethodSig signature, ModificationKind kind)
            {
                MethodModification.Builder builder = new MethodModification.Builder(name, signature, kind, ReadOnly);
                methods.Add(builder);
                return builder;
            }

            private TBuilder GetOrCreate<TBuilder>(UTF8String name, ModificationKind kind, Func<UTF8String, ModificationKind, TBuilder> factory) where TBuilder : IModificationBuilder<IModification>
            {
                IModificationBuilder<IModification> existing;
                lock (members)
                {
                    if (!members.TryGetValue(name, out existing))
                    {
                        TBuilder builder = factory(name, kind);
                        members.Add(name, builder);
                        return builder;
                    }
                }
                if (!(existing is TBuilder casted)) throw new ArgumentException("member " + name.String + " already exists but is of a different type");
                if (existing.Kind != kind) throw new ArgumentException("existing member " + name.String + " has kind " + existing.Kind + " but kind " + kind + "was requested");
                return casted;
            }

            public TypeModification Build() => new TypeModification(this);
        }
    }

    public sealed class FieldModification : AccessibleModification<FieldModification>
    {

        public UTF8String Name { get; }
        public FieldSig Signature { get; }

        public FieldAttributes Attributes { get; }

        public override UTF8String FullName => Name;
        public override SymbolKind SymbolKind => SymbolKind.Field;

        internal override AccessLevel AccessLevel => Attributes.GetAccessLevel();

        private protected override FieldModification This => this;

        private FieldModification(Builder builder) : base(builder.Kind, builder.ReadOnly)
        {
            Name = builder.Name;
            Attributes = builder.Attributes;
            Signature = builder.Signature;
        }

        public void Apply(FieldDef field, Action<FieldDef> add)
        {
            try
            {
                if (!CheckExistence(field)) CheckDefinition(field);
                else add(field = new FieldDefUser(Name, Signature, Attributes));
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<FieldModification>(this, e);
            }
        }

        private void CheckDefinition(FieldDef field)
        {
            if (!Comparer.Equals(field.FieldSig, Signature)) throw new InstallException("existing field has an incompatible signature");
            CheckAttributes(field);
        }

        private void CheckAttributes(FieldDef field)
        {
            FieldAttributes newAttributes = field.Attributes;
            FieldAttributes mask = FieldAttributes.FieldAccessMask;
            if (!CheckAccessLevel(field.Attributes.GetAccessLevel())) Attributes.Replace(ref newAttributes, mask);
            if (!Attributes.Equals(newAttributes, ~mask)) Attributes.Replace(ref newAttributes, ~mask);
            if (field.Attributes == newAttributes) return;
            BeginModify();
            field.Attributes = newAttributes;
        }

        public sealed class Builder : IModificationBuilder<FieldModification>
        {
            public UTF8String Name { get; }
            public FieldSig Signature { get; }
            public ModificationKind Kind { get; }
            public bool ReadOnly { get; }

            public FieldAttributes Attributes { get; set; }

            public Builder(UTF8String name, FieldSig signature, ModificationKind kind, bool readOnly)
            {
                Name = name;
                Signature = signature;
                Kind = kind;
                ReadOnly = readOnly;
            }

            public FieldModification Build() => new FieldModification(this);
        }
    }

    public sealed class MethodModification : AccessibleModification<MethodModification>
    {
        public UTF8String Name { get; }
        public MethodSig Signature { get; }

        public MethodAttributes Attributes { get; }

        public override UTF8String FullName => Name.Concat("[", Signature.ToString(), "]");
        public override SymbolKind SymbolKind => SymbolKind.Method;

        internal override AccessLevel AccessLevel => Attributes.GetAccessLevel();

        private protected override MethodModification This => this;

        private MethodModification(Builder builder) : base(builder.Kind, builder.ReadOnly)
        {
            Name = builder.Name;
            Signature = builder.Signature;
            Attributes = builder.Attributes;
        }

        public void Apply(MethodDef method, Action<MethodDef> add)
        {
            try
            {
                if (!CheckExistence(method)) CheckAttributes(method);
                else add(method = new MethodDefUser(Name, Signature, Attributes));
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<MethodModification>(this, e);
            }
        }

        private void CheckAttributes(MethodDef method)
        {
            MethodAttributes newAttributes = method.Attributes;
            MethodAttributes mask = MethodAttributes.MemberAccessMask;
            if (!CheckAccessLevel(method.Attributes.GetAccessLevel())) Attributes.Replace(ref newAttributes, mask);
            if (!Attributes.Equals(newAttributes, ~mask)) Attributes.Replace(ref newAttributes, ~mask);
            if (method.Attributes == newAttributes) return;
            BeginModify();
            method.Attributes = newAttributes;
        }

        public sealed class Builder : IModificationBuilder<MethodModification>
        {
            public UTF8String Name { get; }
            public MethodSig Signature { get; }
            public ModificationKind Kind { get; }
            public bool ReadOnly { get; }

            public MethodAttributes Attributes { get; set; }

            public Builder(UTF8String name, MethodSig signature, ModificationKind kind, bool readOnly)
            {
                Name = name;
                Signature = signature;
                Kind = kind;
                ReadOnly = readOnly;
            }

            public MethodModification Build() => new MethodModification(this);
        }
    }

    public sealed class PropertyModification : AbstractModification<PropertyModification>
    {
        public UTF8String Name { get; }
        public PropertySig Signature { get; }

        public PropertyAttributes Attributes { get; }
        public MethodModification GetMethod { get; }
        public MethodModification SetMethod { get; }
        public IEnumerable<MethodModification> OtherMethods { get; }

        public override UTF8String FullName => Name.Concat("[", Signature.ToString(), "]");
        public override SymbolKind SymbolKind => SymbolKind.Property;

        private protected override PropertyModification This => this;

        public PropertyModification(Builder builder) : base(builder.Kind, builder.ReadOnly)
        {
            Name = builder.Name;
            Signature = builder.Signature;
        }

        public sealed class Builder : IModificationBuilder<PropertyModification>
        {
            public UTF8String Name { get; }
            public PropertySig Signature { get; }
            public ModificationKind Kind { get; }
            public bool ReadOnly { get; }

            public PropertyAttributes Attributes { get; set; }
            public MethodModification.Builder GetMethod { get; }
            public MethodModification.Builder SetMethod { get; }
            public IEnumerable<MethodModification.Builder> OtherMethods { get; }

            public Builder(UTF8String name, PropertySig signature, ModificationKind kind, bool readOnly)
            {
                Name = name;
                Signature = signature;
                Kind = kind;
                ReadOnly = readOnly;
            }

            public PropertyModification Build() => new PropertyModification(this);
        }
    }

    public sealed class EventModification : AbstractModification<EventModification>
    {
        public UTF8String Name { get; }
        public TypeModification Type { get; }

        public PropertyAttributes Attributes { get; }

        public override UTF8String FullName => Name.Concat("[", Type.FullName, "]");
        public override SymbolKind SymbolKind => SymbolKind.Event;

        private protected override EventModification This => this;

        public EventModification(Builder builder) : base(builder.Kind, builder.ReadOnly)
        {
            Name = builder.Name;
            Type = builder.Type;
        }

        public sealed class Builder : IModificationBuilder<EventModification>
        {
            public UTF8String Name { get; }
            public TypeModification Type { get; }
            public ModificationKind Kind { get; }
            public bool ReadOnly { get; }

            public PropertyAttributes Attributes { get; set; }

            public Builder(UTF8String name, TypeModification type, ModificationKind kind, bool readOnly)
            {
                Name = name;
                Type = type;
                Kind = kind;
                ReadOnly = readOnly;
            }

            public EventModification Build() => new EventModification(this);
        }
    }
}
