using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
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
        }
        
        public static TypeSig ApplyToLeaf(this TypeSig sig, Func<LeafSig, TypeSig> transform)
        {
            if (sig is LeafSig ls) return transform(ls);
            TypeSig unwrapped = ApplyToLeaf(sig.Next, transform);
            switch (sig)
            {
                case ValueArraySig valueArray:
                    return new ValueArraySig(unwrapped, valueArray.Size);
                case SZArraySig szArray:
                    return new SZArraySig(unwrapped);
                case ArraySig array:
                    return new ArraySig(unwrapped, array.Rank, array.Sizes, array.LowerBounds);
                case ByRefSig byRef:
                    return new ByRefSig(unwrapped);
                case PtrSig ptr:
                    return new PtrSig(unwrapped);
                case ModuleSig module:
                    return new ModuleSig(module.Index, unwrapped);
                case CModOptSig cModOpt:
                    return new CModOptSig(cModOpt.Modifier, unwrapped);
                case CModReqdSig cModReqd:
                    return new CModReqdSig(cModReqd.Modifier, unwrapped);
                case PinnedSig pinned:
                    return new PinnedSig(unwrapped);
            }
            throw new NotImplementedException("unknown element type " + sig.ElementType);
        }*/

        public static bool Equals(this TypeAttributes a, TypeAttributes b, TypeAttributes mask) => (a & mask) == (b & mask);
        public static bool Equals(this FieldAttributes a, FieldAttributes b, FieldAttributes mask) => (a & mask) == (b & mask);
        public static bool Equals(this MethodAttributes a, MethodAttributes b, MethodAttributes mask) => (a & mask) == (b & mask);

        public static void Replace(this TypeAttributes replacement, ref TypeAttributes original, TypeAttributes mask) => original = original & ~mask | replacement & mask;
        public static void Replace(this FieldAttributes replacement, ref FieldAttributes original, FieldAttributes mask) => original = original & ~mask | replacement & mask;
        public static void Replace(this MethodAttributes replacement, ref MethodAttributes original, MethodAttributes mask) => original = original & ~mask | replacement & mask;

        public static bool IsValidFileName(this string name) => !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    internal static class InternalExtensions
    {
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
    }
}
