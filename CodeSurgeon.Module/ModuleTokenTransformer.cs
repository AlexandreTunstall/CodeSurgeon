using System;
using System.Collections.Generic;
using System.Text;
using dnlib.DotNet;

namespace CodeSurgeon.Module
{
    internal class ModuleTokenTransformer : ResolvingTokenTransformer
    {
        private readonly ModuleImporter importer;
        private readonly ModuleDef module;

        public ModuleTokenTransformer(ModuleImporter importer, ModuleDef module) : base(module)
        {
            this.importer = importer;
            this.module = module;
        }

        public override ITypeDefOrRef Transform(ITypeDefOrRef token, ISearchContext context) => importer.Import(token.ResolveTypeDef())?.Resolve(context) ?? token;

        public override IField Transform(IField token, ISearchContext context) => importer.Import(token.ResolveFieldDef())?.Resolve(context) ?? token;

        public override IMethod Transform(IMethod token, ISearchContext context) => importer.Import(token.ResolveMethodDef())?.Resolve(context) ?? token;

        public override IMDTokenProvider Transform(IMDTokenProvider token, ISearchContext context)
        {
            switch (token = base.Transform(token, context))
            {
                case ITypeDefOrRef type:
                    return Transform(type, context);
                case IField field:
                    return Transform(field, context);
                case IMethod method:
                    return Transform(method, context);
                default:
                    return token;
            }
        }
    }
}
