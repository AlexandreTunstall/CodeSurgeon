using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSurgeon
{
    public interface IModification<out TSymbol> where TSymbol : class, IDnlibDef
    {
        ModificationKind Kind { get; }
        UTF8String FullName { get; }
        SymbolKind SymbolKind { get; }
        TSymbol Resolve(ISearchContext context);
        void Apply(ISearchContext context);
    }

    public enum ModificationKind
    {
        FailIfPresent,
        FailIfMissing,
        CreateIfMissing
    }

    public enum SymbolKind
    {
        Module,
        Type,
        Field,
        Method,
        Property,
        Event
    }

    public abstract class AbstractModification<TSymbol, TSelf> : IModification<TSymbol> where TSymbol : class, IDnlibDef where TSelf : AbstractModification<TSymbol, TSelf>
    {
        private protected const SigComparerOptions SearchOptions = SigComparerOptions.DontCompareTypeScope | SigComparerOptions.IgnoreModifiers | SigComparerOptions.DontCompareReturnType | SigComparerOptions.PrivateScopeIsComparable;
        private protected static readonly SigComparer Comparer = new SigComparer(SearchOptions);

        public ModificationKind Kind { get; }
        public bool ReadOnly { get; }

        public abstract UTF8String FullName { get; }
        public abstract SymbolKind SymbolKind { get; }

        private protected abstract TSelf This { get; }

        private protected AbstractModification(ModificationKind kind, bool readOnly)
        {
            if (!(this is TSelf)) throw new InvalidOperationException("derived type must inherit TSymbol");
            Kind = kind;
            ReadOnly = readOnly;
        }

        public abstract TSymbol Resolve(ISearchContext context);
        public abstract void Apply(ISearchContext context);

        private protected bool CheckExistence(IDnlibDef target)
        {
            switch (Kind)
            {
                case ModificationKind.FailIfPresent:
                    if (target != null) throw new SymbolInstallException<TSelf>(This, new InstallException("a definition for this symbol already exists"));
                    return true;
                case ModificationKind.FailIfMissing:
                    if (target == null) throw new SymbolInstallException<TSelf>(This, new InstallException("could not find a definition for this symbol"));
                    return false;
                case ModificationKind.CreateIfMissing:
                    return target != null;
                default:
                    throw new SymbolInstallException<TSelf>(This, new InstallException("unknown modification kind " + Kind));
            }
        }

        private protected void BeginModify()
        {
            if (ReadOnly) throw new ReadOnlyInstallException();
        }

        private protected class NamedComparer<T> : IEqualityComparer<(UTF8String name, T obj)>
        {
            public Func<T, T, bool> Comparer { get; }

            public NamedComparer(Func<T, T, bool> comparer) => Comparer = comparer;

            public bool Equals((UTF8String name, T obj) x, (UTF8String name, T obj) y) => x.name == y.name && Comparer(x.obj, y.obj);

            public int GetHashCode((UTF8String name, T obj) obj) => obj.GetHashCode();
        }
    }

    public abstract class MemberModification<TSymbol, TSelf> : AbstractModification<TSymbol, TSelf> where TSymbol : class, IDnlibDef where TSelf : MemberModification<TSymbol, TSelf>
    {
        public TypeModification DeclaringType { get; }

        private protected MemberModification(TypeModification declaringType, ModificationKind kind, bool readOnly) : base(kind, readOnly) => DeclaringType = declaringType;
    }

    public abstract class AccessibleModification<TSymbol, TSelf> : MemberModification<TSymbol, TSelf> where TSymbol : class, IDnlibDef where TSelf : AccessibleModification<TSymbol, TSelf>
    {
        internal abstract AccessLevel AccessLevel { get; }

        private protected AccessibleModification(TypeModification declaringType, ModificationKind kind, bool readOnly) : base(declaringType, kind, readOnly) { }

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

    public sealed class ModuleModification : AbstractModification<ModuleDef, ModuleModification>
    {
        public UTF8String Name { get; }

        public IEnumerable<TypeModification> Types => types.Values;

        public override UTF8String FullName => Name;
        public override SymbolKind SymbolKind => SymbolKind.Module;

        private protected override ModuleModification This => this;

        private Dictionary<(UTF8String @namespace, UTF8String name), TypeModification> types = new Dictionary<(UTF8String @namespace, UTF8String name), TypeModification>();

        public ModuleModification(UTF8String name, ModificationKind kind, bool readOnly) : base(kind, readOnly)
        {
            if (name is null) throw new ArgumentNullException("name");
            Name = name;
        }

        public TypeModification Type(UTF8String @namespace, UTF8String name, ModificationKind kind)
        {
            lock (types)
            {
                if (!types.TryGetValue((@namespace, name), out TypeModification type)) types.Add((@namespace, name), type = new TypeModification(this, @namespace, name, kind, ReadOnly));
                else if (type.Kind != kind) throw new ArgumentException("existing type " + name.String + " has kind " + type.Kind + " but kind " + kind + " was requested");
                return type;
            }
        }

        public override ModuleDef Resolve(ISearchContext context)
        {
            try
            {
                ModuleDef module = context.Get(Name);
                if (!CheckExistence(module)) return module;
                BeginModify();
                return new ModuleDefUser(Name);
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<ModuleModification>(this, e);
            }
        }

        public override void Apply(ISearchContext context)
        {
            foreach (TypeModification mod in Types) mod.Apply(context);
        }
    }

    public sealed class TypeModification : AccessibleModification<TypeDef, TypeModification>
    {
        private static readonly IEqualityComparer<(UTF8String name, MethodSig sig)> NamedMethodComparer = new NamedComparer<MethodSig>(Comparer.Equals);

        public ModuleModification Module { get; }
        public UTF8String Namespace { get; }
        public UTF8String Name { get; }

        public IEnumerable<TypeModification> NestedTypes { get; }
        public IEnumerable<FieldModification> Fields { get; }
        public IEnumerable<MethodModification> Methods => methods.Values;
        public IEnumerable<PropertyModification> Properties { get; }
        public IEnumerable<EventModification> Events { get; }

        public TypeAttributes Attributes { get; set; }

        public override UTF8String FullName
        {
            get
            {
                TypeModification current = this;
                List<UTF8String> tokens = new List<UTF8String>();
                while (current.IsNested)
                {
                    current = current.DeclaringType;
                    tokens.Add(current.Name);
                    tokens.Add("/");
                }
                tokens.Add(current.Name);
                return current.Namespace.Concat(tokens);
            }
        }
        public override SymbolKind SymbolKind => SymbolKind.Type;
        public bool IsNested => Namespace is null;

        internal override AccessLevel AccessLevel => Attributes.GetAccessLevel(IsNested);

        private protected override TypeModification This => this;

        private readonly Dictionary<UTF8String, IModification<IDnlibDef>> members = new Dictionary<UTF8String, IModification<IDnlibDef>>();
        private readonly Dictionary<(UTF8String, MethodSig), MethodModification> methods = new Dictionary<(UTF8String, MethodSig), MethodModification>(NamedMethodComparer);

        internal TypeModification(ModuleModification module, UTF8String @namespace, UTF8String name, ModificationKind kind, bool readOnly) : this(null, name, kind, readOnly)
        {
            if (@namespace is null) throw new ArgumentNullException(nameof(@namespace), nameof(@namespace) + " cannot be null, use an empty string for the default namespace instead");
            Module = module;
            Namespace = @namespace;
        }

        private TypeModification(TypeModification declaringType, UTF8String name, ModificationKind kind, bool readOnly) : base(declaringType, kind, readOnly)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            Name = name;
            NestedTypes = members.Values.OfType<TypeModification>();
            Fields = members.Values.OfType<FieldModification>();
            Properties = members.Values.OfType<PropertyModification>();
            Events = members.Values.OfType<EventModification>();
        }

        public TypeModification NestedType(UTF8String name, ModificationKind kind) => GetOrCreate(name, kind, (n, k) => new TypeModification(this, n, k, ReadOnly));
        public FieldModification Field(UTF8String name, FieldSig signature, ModificationKind kind) => GetOrCreate(name, kind, (n, k) => new FieldModification(this, n, signature, k, ReadOnly));
        public MethodModification Method(UTF8String name, MethodSig signature, ModificationKind kind)
        {
            lock (methods)
            {
                if (!methods.TryGetValue((name, signature), out MethodModification method)) methods.Add((name, signature), method = new MethodModification(this, name, signature, kind, ReadOnly));
                return method;
            }
        }
        public PropertyModification Property(UTF8String name, PropertySig signature, ModificationKind kind) => GetOrCreate(name, kind, (n, k) => new PropertyModification(this, n, signature, k, ReadOnly));
        public EventModification Event(UTF8String name, TypeModification type, ModificationKind kind) => GetOrCreate(name, kind, (n, k) => new EventModification(this, n, type, k, ReadOnly));

        public override TypeDef Resolve(ISearchContext context)
        {
            try
            {
                ModuleDef module = IsNested ? null : context.Get(Module);
                TypeDef declaringType = IsNested ? context.Get(DeclaringType) : null;
                TypeDef type = IsNested ? declaringType.NestedTypes.FirstOrDefault(nt => nt.Name == Name) : module.Find(new TypeRefUser(module, Namespace, Name));
                if (!CheckExistence(type)) return type;
                BeginModify();
                if (IsNested) return new TypeDefUser(Name)
                {
                    DeclaringType = declaringType
                };
                else module.Types.Add(type = new TypeDefUser(Namespace, Name));
                return type;
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<TypeModification>(this, e);
            }
        }

        public override void Apply(ISearchContext context)
        {
            try
            {
                TypeDef type = context.Get(this);
                CheckAttributes(type);
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<TypeModification>(this, e);
            }
            foreach (IModification<IDnlibDef> mod in NestedTypes.Concat<IModification<IDnlibDef>>(Fields).Concat(Methods).Concat(Properties).Concat(Events)) mod.Apply(context);
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

        private TMod GetOrCreate<TMod>(UTF8String name, ModificationKind kind, Func<UTF8String, ModificationKind, TMod> factory) where TMod : IModification<IDnlibDef>
        {
            IModification<IDnlibDef> existing;
            lock (members)
            {
                if (!members.TryGetValue(name, out existing))
                {
                    TMod builder = factory(name, kind);
                    members.Add(name, builder);
                    return builder;
                }
            }
            if (!(existing is TMod casted)) throw new ArgumentException("member " + name.String + " already exists but is of a different type");
            if (existing.Kind != kind) throw new ArgumentException("existing member " + name.String + " has kind " + existing.Kind + " but kind " + kind + " was requested");
            return casted;
        }
    }

    public sealed class FieldModification : AccessibleModification<FieldDef, FieldModification>
    {
        public UTF8String Name { get; }
        public FieldSig Signature { get; }

        public FieldAttributes Attributes { get; set; }

        public override UTF8String FullName => Name;
        public override SymbolKind SymbolKind => SymbolKind.Field;

        internal override AccessLevel AccessLevel => Attributes.GetAccessLevel();

        private protected override FieldModification This => this;

        internal FieldModification(TypeModification declaringType, UTF8String name, FieldSig signature, ModificationKind kind, bool readOnly) : base(declaringType, kind, readOnly)
        {
            Name = name;
            Signature = signature;
        }

        public override FieldDef Resolve(ISearchContext context)
        {
            try
            {
                TypeDef declaringType = context.Get(DeclaringType);
                FieldDef field = declaringType.FindField(Name);
                if (!CheckExistence(field)) return field;
                BeginModify();
                return new FieldDefUser(Name, Signature, Attributes)
                {
                    DeclaringType = declaringType
                };
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<FieldModification>(this, e);
            }
        }

        public override void Apply(ISearchContext context)
        {
            try
            {
                FieldDef field = context.Get(this);
                CheckDefinition(field);
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
    }

    public sealed class MethodModification : AccessibleModification<MethodDef, MethodModification>
    {
        public UTF8String Name { get; }
        public MethodSig Signature { get; }

        public MethodAttributes Attributes { get; set; }

        public override UTF8String FullName => Name.Concat("[", Signature.ToString(), "]");
        public override SymbolKind SymbolKind => SymbolKind.Method;

        internal override AccessLevel AccessLevel => Attributes.GetAccessLevel();

        private protected override MethodModification This => this;

        internal MethodModification(TypeModification declaringType, UTF8String name, MethodSig signature, ModificationKind kind, bool readOnly) : base(declaringType, kind, readOnly)
        {
            Name = name;
            Signature = signature;
        }

        public override MethodDef Resolve(ISearchContext context)
        {
            try
            {
                TypeDef declaringType = context.Get(DeclaringType);
                MethodDef method = declaringType.FindMethod(Name, Signature, SearchOptions);
                if (!CheckExistence(method)) return method;
                BeginModify();
                return new MethodDefUser(Name, Signature, Attributes)
                {
                    DeclaringType = declaringType
                };
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<MethodModification>(this, e);
            }
        }

        public override void Apply(ISearchContext context)
        {
            try
            {
                MethodDef method = context.Get(this);
                CheckAttributes(method);
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
    }

    public sealed class PropertyModification : MemberModification<PropertyDef, PropertyModification>
    {
        public UTF8String Name { get; }
        public PropertySig Signature { get; }

        public PropertyAttributes Attributes { get; set; }
        public IEnumerable<MethodModification> GetMethods => getMethods;
        public IEnumerable<MethodModification> SetMethods => setMethods;
        public IEnumerable<MethodModification> OtherMethods => otherMethods;

        public override UTF8String FullName => Name.Concat("[", Signature.ToString(), "]");
        public override SymbolKind SymbolKind => SymbolKind.Property;

        private protected override PropertyModification This => this;

        private readonly ISet<MethodModification> getMethods = new HashSet<MethodModification>();
        private readonly ISet<MethodModification> setMethods = new HashSet<MethodModification>();
        private readonly ISet<MethodModification> otherMethods = new HashSet<MethodModification>();

        internal PropertyModification(TypeModification declaringType, UTF8String name, PropertySig signature, ModificationKind kind, bool readOnly) : base(declaringType, kind, readOnly)
        {
            Name = name;
            Signature = signature;
        }

        public void Get(MethodModification method) => getMethods.Add(method);
        public void Set(MethodModification method) => setMethods.Add(method);
        public void Other(MethodModification method) => otherMethods.Add(method);

        public override PropertyDef Resolve(ISearchContext context)
        {
            try
            {
                TypeDef declaringType = context.Get(DeclaringType);
                PropertyDef property = declaringType.FindProperty(Name, Signature, SearchOptions);
                if (!CheckExistence(property)) return property;
                BeginModify();
                return new PropertyDefUser(Name, Signature, Attributes)
                {
                    DeclaringType = declaringType
                };
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<PropertyModification>(this, e);
            }
        }

        public override void Apply(ISearchContext context)
        {
            try
            {
                PropertyDef property = context.Get(this);
                CheckAttributes(property);
                context.CheckAccessors(GetMethods, property.GetMethods, BeginModify);
                context.CheckAccessors(SetMethods, property.SetMethods, BeginModify);
                context.CheckAccessors(OtherMethods, property.OtherMethods, BeginModify);
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<PropertyModification>(this, e);
            }
        }

        private void CheckAttributes(PropertyDef property)
        {
            PropertyAttributes newAttributes = property.Attributes;
            PropertyAttributes mask = default;
            if (!Attributes.Equals(newAttributes, ~mask)) Attributes.Replace(ref newAttributes, ~mask);
            if (property.Attributes == newAttributes) return;
            BeginModify();
            property.Attributes = newAttributes;
        }
    }

    public sealed class EventModification : MemberModification<EventDef, EventModification>
    {
        public UTF8String Name { get; }
        public TypeModification Type { get; }

        public EventAttributes Attributes { get; set; }
        public MethodModification AddMethod { get; set; }
        public MethodModification RemoveMethod { get; set; }
        public MethodModification InvokeMethod { get; set; }
        public IEnumerable<MethodModification> OtherMethods => otherMethods;

        public override UTF8String FullName => Name.Concat("[", Type.FullName, "]");
        public override SymbolKind SymbolKind => SymbolKind.Event;

        private protected override EventModification This => this;

        private ISet<MethodModification> otherMethods = new HashSet<MethodModification>();

        internal EventModification(TypeModification declaringType, UTF8String name, TypeModification type, ModificationKind kind, bool readOnly) : base(declaringType, kind, readOnly)
        {
            Name = name;
            Type = type;
        }

        public void Other(MethodModification method) => otherMethods.Add(method);

        public override EventDef Resolve(ISearchContext context)
        {
            try
            {
                TypeDef declaringType = context.Get(DeclaringType);
                TypeDef type = context.Get(Type);
                EventDef @event = declaringType.FindEvent(Name, type, SearchOptions);
                if (!CheckExistence(@event)) return @event;
                BeginModify();
                return new EventDefUser(Name, type, Attributes)
                {
                    DeclaringType = declaringType
                };
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<EventModification>(this, e);
            }
        }

        public override void Apply(ISearchContext context)
        {
            try
            {
                EventDef @event = context.Get(this);
                CheckAttributes(@event);
                MethodDef desired;
                if (AddMethod != null && (desired = context.Get(AddMethod)) != @event.AddMethod)
                {
                    BeginModify();
                    @event.AddMethod = desired;
                }
                if (RemoveMethod != null && (desired = context.Get(RemoveMethod)) != @event.RemoveMethod)
                {
                    BeginModify();
                    @event.RemoveMethod = desired;
                }
                if (InvokeMethod != null && (desired = context.Get(InvokeMethod)) != @event.InvokeMethod)
                {
                    BeginModify();
                    @event.InvokeMethod = desired;
                }
                context.CheckAccessors(OtherMethods, @event.OtherMethods, BeginModify);
            }
            catch (ReadOnlyInstallException e)
            {
                throw new SymbolInstallException<EventModification>(this, e);
            }
        }

        private void CheckAttributes(EventDef @event)
        {
            EventAttributes newAttributes = @event.Attributes;
            EventAttributes mask = default;
            if (!Attributes.Equals(newAttributes, ~mask)) Attributes.Replace(ref newAttributes, ~mask);
            if (@event.Attributes == newAttributes) return;
            BeginModify();
            @event.Attributes = newAttributes;
        }
    }
}
