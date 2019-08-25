using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace CodeSurgeon
{
    public static class Extensions
    {
        public static ILeafSignature<LeafSig> GetLeaf(this ITypeSignature<TypeSig> sig)
        {
            while (true) {
                switch (sig)
                {
                    case ILeafSignature<LeafSig> leaf:
                        return leaf;
                    case INonLeafSignature<NonLeafSig> nonLeaf:
                        sig = nonLeaf.Next;
                        break;
                }
            }
        }

        public static bool Equals(this TypeAttributes a, TypeAttributes b, TypeAttributes mask) => (a & mask) == (b & mask);
        public static bool Equals(this FieldAttributes a, FieldAttributes b, FieldAttributes mask) => (a & mask) == (b & mask);
        public static bool Equals(this MethodAttributes a, MethodAttributes b, MethodAttributes mask) => (a & mask) == (b & mask);
        public static bool Equals(this PropertyAttributes a, PropertyAttributes b, PropertyAttributes mask) => (a & mask) == (b & mask);
        public static bool Equals(this EventAttributes a, EventAttributes b, EventAttributes mask) => (a & mask) == (b & mask);

        public static void Replace(this TypeAttributes replacement, ref TypeAttributes original, TypeAttributes mask) => original = original & ~mask | replacement & mask;
        public static void Replace(this FieldAttributes replacement, ref FieldAttributes original, FieldAttributes mask) => original = original & ~mask | replacement & mask;
        public static void Replace(this MethodAttributes replacement, ref MethodAttributes original, MethodAttributes mask) => original = original & ~mask | replacement & mask;
        public static void Replace(this PropertyAttributes replacement, ref PropertyAttributes original, PropertyAttributes mask) => original = original & ~mask | replacement & mask;
        public static void Replace(this EventAttributes replacement, ref EventAttributes original, EventAttributes mask) => original = original & ~mask | replacement & mask;

        public static bool IsValidFileName(this string name) => !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        public static UTF8String Concat(this UTF8String first, params UTF8String[] rest) => first.Concat((IEnumerable<UTF8String>)rest);
        public static UTF8String Concat(this UTF8String first, IEnumerable<UTF8String> rest)
        {
            byte[] data = new byte[first.DataLength + rest.Sum(s => s.DataLength)];
            int index = first.DataLength;
            Array.Copy(first.Data, data, index);
            foreach (UTF8String value in rest)
            {
                Array.Copy(value.Data, 0, data, index, value.DataLength);
                index += value.DataLength;
            }
            return new UTF8String(data);
        }
    }

    internal static class InternalExtensions
    {
        public static void CheckAccessors(this ISearchContext context, IEnumerable<MethodModification> desired, IList<MethodDef> existing, Action beginModify)
        {
            bool modified = false;
            foreach (MethodDef method in desired.Select(context.Get).Except(existing))
            {
                if (!modified)
                {
                    beginModify();
                    modified = true;
                }
                existing.Add(method);
            }
        }

        public static AccessLevel GetAccessLevel(this TypeAttributes attributes, bool nested, out bool isLevelValid)
        {
            isLevelValid = true;
            if (!nested)
            {
                switch (attributes & TypeAttributes.VisibilityMask)
                {
                    default:
                        isLevelValid = false;
                        return default;
                    case TypeAttributes.NotPublic:
                        return AccessLevel.Private;
                    case TypeAttributes.Public:
                        return AccessLevel.Public;
                }
            }
            switch (attributes & TypeAttributes.VisibilityMask)
            {
                default:
                    isLevelValid = false;
                    return default;
                case TypeAttributes.NestedPrivate:
                    return AccessLevel.Private;
                case TypeAttributes.NestedFamANDAssem:
                    return AccessLevel.FamilyAndAssembly;
                case TypeAttributes.NestedAssembly:
                    return AccessLevel.Assembly;
                case TypeAttributes.NestedFamily:
                    return AccessLevel.Family;
                case TypeAttributes.NestedFamORAssem:
                    return AccessLevel.FamilyOrAssembly;
                case TypeAttributes.NestedPublic:
                    return AccessLevel.Public;
            }
        }

        public static AccessLevel GetAccessLevel(this FieldAttributes attributes)
        {
            switch (attributes & FieldAttributes.FieldAccessMask)
            {
                default:
                    return AccessLevel.Private;
                case FieldAttributes.FamANDAssem:
                    return AccessLevel.FamilyAndAssembly;
                case FieldAttributes.Assembly:
                    return AccessLevel.Assembly;
                case FieldAttributes.Family:
                    return AccessLevel.Family;
                case FieldAttributes.FamORAssem:
                    return AccessLevel.FamilyOrAssembly;
                case FieldAttributes.Public:
                    return AccessLevel.Public;
            }
        }

        public static AccessLevel GetAccessLevel(this MethodAttributes attributes)
        {
            switch (attributes & MethodAttributes.MemberAccessMask)
            {
                default:
                    return AccessLevel.Private;
                case MethodAttributes.FamANDAssem:
                    return AccessLevel.FamilyAndAssembly;
                case MethodAttributes.Assembly:
                    return AccessLevel.Assembly;
                case MethodAttributes.Family:
                    return AccessLevel.Family;
                case MethodAttributes.FamORAssem:
                    return AccessLevel.FamilyOrAssembly;
                case MethodAttributes.Public:
                    return AccessLevel.Public;
            }
        }

        public static IMDTokenProvider Import(this ModuleDef module, IMDTokenProvider token)
        {
            switch (token)
            {
                case IField field:
                    return module.Import(field);
                case IMethod method:
                    return module.Import(method);
                case IType type:
                    return module.Import(type);
                default:
                    throw new ArgumentException("unrecognized token provider type: " + token.GetType());
            }
        }

        public static UTF8String ToFullName<TSymbol>(this ITypeReference<TSymbol> reference) where TSymbol : class, ITypeDefOrRef
        {
            ITypeReference<ITypeDefOrRef> current = reference;
            List<UTF8String> tokens = new List<UTF8String>();
            while (current.IsNested)
            {
                tokens.Add(current.Name);
                tokens.Add("/");
                current = current.DeclaringType;
            }
            tokens.Add(current.Name);
            if (current.Namespace.DataLength > 0)
            {
                tokens.Add(".");
                tokens.Add(current.Namespace);
            }
            if (current is TypeModification mod)
            {
                tokens.Add("]");
                tokens.Add(mod.Module.FullName);
                tokens.Add("[");
            }
            tokens.Reverse();
            return UTF8String.Empty.Concat(tokens);
        }
        public static UTF8String ToFullName<TSymbol>(this IFieldReference<TSymbol> reference) where TSymbol : class, IField => reference.DeclaringType.FullName.Concat(".", reference.Name);
        public static UTF8String ToFullName<TSymbol>(this IMethodReference<TSymbol> reference) where TSymbol : class, IMethod => reference.DeclaringType.FullName.Concat(".", reference.Name, "[", reference.Signature.ToString(), "]");

        public static bool Compare<T>(this T a, T b) where T : IEquatable<T> => EqualityComparer<T>.Default.Equals(a, b);
        public static bool Compare<T>(this IEnumerable<T> a, IEnumerable<T> b) where T : IEquatable<T> => Enumerable.SequenceEqual(a, b, EqualityComparer<T>.Default);
    }
}
