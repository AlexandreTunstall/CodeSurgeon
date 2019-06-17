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

    public abstract class AbstractModification : IModification
    {
        public ModificationKind Kind { get; }
        public bool ReadOnly { get; }

        internal abstract AccessLevel AccessLevel { get; }

        private protected AbstractModification(ModificationKind kind, bool readOnly)
        {
            Kind = kind;
            ReadOnly = readOnly;
        }

        private protected bool CheckExistence(IDnlibDef target, string targetKind, string name)
        {
            switch (Kind)
            {
                case ModificationKind.FailIfPresent:
                    if (target != null) throw new InstallException(targetKind + " " + name + " already exists");
                    return true;
                case ModificationKind.FailIfMissing:
                    if (target == null) throw new InstallException(targetKind + " " + name + " could not be found");
                    return false;
                case ModificationKind.CreateIfMissing:
                    return target != null;
                default:
                    throw new InstallException("unknown modification kind " + Kind);
            }
        }

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

        private protected void BeginModify()
        {
            if (ReadOnly) throw new InstallException("existing read-only definition is incompatible with the patch");
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

    public sealed class TypeModification : AbstractModification
    {
        private const SigComparerOptions SearchOptions = SigComparerOptions.DontCompareTypeScope | SigComparerOptions.IgnoreModifiers | SigComparerOptions.DontCompareReturnType | SigComparerOptions.PrivateScopeIsComparable;

        public UTF8String Namespace { get; }
        public UTF8String Name { get; }

        public IEnumerable<TypeModification> NestedTypes { get; }
        public IEnumerable<FieldModification> Fields { get; }
        public IEnumerable<MethodModification> Methods { get; }

        public TypeAttributes Attributes { get; }

        public bool IsNested => Namespace is null;

        internal override AccessLevel AccessLevel => Attributes.GetAccessLevel(IsNested);

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
            if (!CheckExistence(type, "type", Name)) CheckAttributes(type);
            else if (IsNested) add(type = new TypeDefUser(Name));
            else add(type = new TypeDefUser(Namespace, Name));
            foreach (TypeModification mod in NestedTypes) mod.Apply(type.NestedTypes.FirstOrDefault(nt => nt.Name == mod.Name), AddType);
            foreach (FieldModification mod in Fields) mod.Apply(type.FindField(mod.Name, mod.Signature, SearchOptions), AddField);
            foreach (MethodModification mod in Methods) mod.Apply(type.FindMethod(mod.Name, mod.Signature, SearchOptions), AddMethod);

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
            TypeAttributes modified = type.Attributes;
            if (!CheckAccessLevel(type.Attributes.GetAccessLevel(IsNested)))
            {
                BeginModify();
                Attributes.Replace(ref modified, TypeAttributes.VisibilityMask);
            }
            if (!Attributes.Equals(modified, ~TypeAttributes.VisibilityMask))
            {
                BeginModify();
                Attributes.Replace(ref modified, ~TypeAttributes.VisibilityMask);
            }
            type.Attributes = modified;
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

    public sealed class FieldModification : AbstractModification
    {
        public UTF8String Name { get; }
        public FieldSig Signature { get; }

        public FieldAttributes Attributes { get; }

        internal override AccessLevel AccessLevel => Attributes.GetAccessLevel();

        private FieldModification(Builder builder) : base(builder.Kind, builder.ReadOnly)
        {
            Name = builder.Name;
            Attributes = builder.Attributes;
            Signature = builder.Signature;
        }

        public void Apply(FieldDef field, Action<FieldDef> add)
        {
            if (!CheckExistence(field, "field", Name)) CheckAttributes(field);
            else add(field = new FieldDefUser(Name, Signature, Attributes));
        }

        private void CheckAttributes(FieldDef field)
        {
            FieldAttributes modified = field.Attributes;
            if (!CheckAccessLevel(field.Attributes.GetAccessLevel()))
            {
                BeginModify();
                Attributes.Replace(ref modified, FieldAttributes.FieldAccessMask);
            }
            if (!Attributes.Equals(modified, ~FieldAttributes.FieldAccessMask))
            {
                BeginModify();
                Attributes.Replace(ref modified, ~FieldAttributes.FieldAccessMask);
            }
            field.Attributes = modified;
        }

        public class Builder : IModificationBuilder<FieldModification>
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

    public class MethodModification : AbstractModification
    {
        public UTF8String Name { get; }
        public MethodSig Signature { get; }

        public MethodAttributes Attributes { get; set; }

        internal override AccessLevel AccessLevel => Attributes.GetAccessLevel();

        private MethodModification(Builder builder) : base(builder.Kind, builder.ReadOnly)
        {
            Name = builder.Name;
            Signature = builder.Signature;
            Attributes = builder.Attributes;
        }

        public void Apply(MethodDef method, Action<MethodDef> add)
        {
            if (CheckExistence(method, "method", Name)) CheckAttributes(method);
            else add(method = new MethodDefUser(Name, Signature, Attributes));
        }

        private void CheckAttributes(MethodDef method)
        {
            MethodAttributes modified = method.Attributes;
            if (!CheckAccessLevel(method.Attributes.GetAccessLevel()))
            {
                BeginModify();
                Attributes.Replace(ref modified, MethodAttributes.MemberAccessMask);
            }
            if (!Attributes.Equals(modified, ~MethodAttributes.MemberAccessMask))
            {
                BeginModify();
                Attributes.Replace(ref modified, ~MethodAttributes.MemberAccessMask);
            }
            method.Attributes = modified;
        }

        public class Builder : IModificationBuilder<MethodModification>
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
}
