﻿using CodeSurgeon.Attributes;
using dnlib.DotNet;
using System;
using System.Collections.Generic;
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

        public static IPatch CreatePatch(this ModuleDef module, string patchName = null) => new ModuleImporter().CreatePatch(module, patchName);

        internal static ModificationKind? GetModificationKind(this IHasCustomAttribute definition)
        {
            if (definition.CustomAttributes.Find(Dependency) != null || definition.CustomAttributes.Find(Mixin) != null) return ModificationKind.FailIfMissing;
            else if (definition.CustomAttributes.Find(Inject) != null) return ModificationKind.FailIfPresent;
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
    }
}