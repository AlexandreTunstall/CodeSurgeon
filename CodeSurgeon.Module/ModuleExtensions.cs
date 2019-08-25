using CodeSurgeon.Attributes;
using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSurgeon.Module
{
    public static class ModuleExtensions
    {
        internal static readonly string Required = typeof(RequiredAttribute).FullName;
        internal static readonly string Name = typeof(NameAttribute).FullName;
        internal static readonly string From = typeof(FromAttribute).FullName;
        internal static readonly string Dependency = typeof(DependencyAttribute).FullName;
        internal static readonly string Inject = typeof(InjectAttribute).FullName;
        internal static readonly string Mixin = typeof(MixinAttribute).FullName;
        internal static readonly string BaseDependency = typeof(BaseDependencyAttribute).FullName;
        internal static readonly string CompilerGenerated = typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute).FullName;

        public static IPatch CreatePatch(this ModuleDef module, string patchName = null) => new ModuleImporter().CreatePatch(module, patchName);

        internal static ModificationKind? GetModificationKind(this IHasCustomAttribute definition)
        {
            if (definition.CustomAttributes.Find(Dependency) != null || definition.CustomAttributes.Find(Mixin) != null) return ModificationKind.FailIfMissing;
            else if (definition.CustomAttributes.Find(Inject) != null) return ModificationKind.FailIfPresent;
            else if (definition.CustomAttributes.Find(CompilerGenerated) != null || definition is IMemberDef member && member.DeclaringType?.CustomAttributes?.Find(CompilerGenerated) != null) return ModificationKind.CreateIfMissing;
            else return null;
        }

        internal static UTF8String GetTargetModule(this TypeDef type) => type.CustomAttributes.Find(Name)?.ConstructorArguments?[0].Value is ClassSig sig ? sig.DefinitionAssembly.Name : type.CustomAttributes.Find(From).ConstructorArguments[0].ExtractName();

        internal static UTF8String GetTargetName(this IDnlibDef definition) => GetTargetName((IHasCustomAttribute)definition) ?? definition.Name;
        internal static UTF8String GetTargetName(this IHasCustomAttribute definition) => definition.CustomAttributes.Find(Name)?.ConstructorArguments?[0].ExtractName();

        internal static UTF8String ExtractName(this CAArgument argument)
        {
            switch (argument.Value)
            {
                case UTF8String utf8:
                    return utf8;
                case string str:
                    return str;
                case byte[] raw:
                    return new UTF8String(raw);
                case ClassSig sig:
                    return sig.FullName;
                default:
                    return null;
            }
        }

        internal static UTF8String ExtractNamespace(this UTF8String fullName, out UTF8String name)
        {
            int index = fullName.LastIndexOf('.');
            if (index < 0)
            {
                name = fullName;
                return UTF8String.Empty;
            }
            name = fullName.Substring(index + 1);
            return fullName.Substring(0, index);
        }

        internal static bool IsBaseDependency(this IHasCustomAttribute token) => !(token.CustomAttributes.Find(BaseDependency) is null);

        internal static MethodDef GetLeafMethod(this IMethod method)
        {
            switch (method)
            {
                case MethodDef def:
                    return def;
                case MemberRef @ref:
                    return method.DeclaringType.GetLeafType()?.ResolveTypeDef()?.ResolveMethod(@ref);
                default:
                    return null;
            }
        }

        internal static ITypeDefOrRef GetLeafType(this ITypeDefOrRef type)
        {
            switch (type)
            {
                case TypeSpec spec:
                    return spec.TypeSig.GetLeafType();
                default:
                    return type;
            }
        }

        internal static ITypeDefOrRef GetLeafType(this TypeSig sig)
        {
            while (true)
            {
                switch (sig)
                {
                    case NonLeafSig nonLeaf:
                        sig = nonLeaf.Next;
                        break;
                    case TypeDefOrRefSig type:
                        return type.TypeDefOrRef;
                    default:
                        return null;
                }
            }
        }
    }
}
