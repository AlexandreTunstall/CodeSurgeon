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
        public static FieldSig Import(this ModuleDef module, FieldSig sig) => new FieldSig(module.Import(sig.Type));
        
        /*public static TypeSig AddScope(this TypeSig sig)
        {
            switch (sig)
            {
                case ClassSig @class:
                    @class.ToTypeDefOrRef()
                    return new ClassSig()
            }
        }*/
        
        public static TypeSig ApplyToLeaf(this TypeSig sig, Func<LeafSig, TypeSig> transform)
        {
            if (sig is LeafSig ls) return transform(ls);
            TypeSig unwrapped = ApplyToLeaf(sig.Next, transform);
            switch (sig)
            {
                case PtrSig ptr:
                    return new PtrSig(unwrapped);
                case ByRefSig byRef:
                    return new ByRefSig(unwrapped);
                case ArraySig array:
                    return new ArraySig(unwrapped, array.Rank, array.Sizes, array.LowerBounds);
                case SZArraySig szArray:
                    return new SZArraySig(unwrapped);
                case CModReqdSig cModReqd:
                    return new CModReqdSig(cModReqd.Modifier, unwrapped);
                case CModOptSig cModOpt:
                    return new CModOptSig(cModOpt.Modifier, unwrapped);
                case PinnedSig pinned:
                    return new PinnedSig(unwrapped);
                case ValueArraySig valueArray:
                    return new ValueArraySig(unwrapped, valueArray.Size);
                case ModuleSig module:
                    return new ModuleSig(module.Index, unwrapped);
            }
            throw new NotImplementedException("unknown element type " + sig.ElementType);
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

        public static AccessLevel GetAccessLevel(this TypeAttributes attributes, bool nested)
        {
            if (!nested)
            {
                switch (attributes & TypeAttributes.VisibilityMask)
                {
                    default:
                        return AccessLevel.Private;
                    case TypeAttributes.Public:
                        return AccessLevel.Public;
                }
            }
            switch (attributes & TypeAttributes.VisibilityMask)
            {
                default:
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
    }
}
