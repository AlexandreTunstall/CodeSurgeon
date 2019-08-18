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

        public override ITypeDefOrRef Transform(ITypeDefOrRef token, ITransformContext context) => importer.Import(token.ResolveTypeDef()) is TypeModification mod ? context.SearchContext.Get(mod) : token;

        public override IField Transform(IField token, ITransformContext context) => importer.Import(token.ResolveFieldDef()) is FieldModification mod ? context.SearchContext.Get(mod) : token;

        public override IMethod Transform(IMethod token, ITransformContext context) => token is IHasCustomAttribute hasAttr && hasAttr.IsBaseDependency() ? InjectBase(context) : importer.Import(token.ResolveMethodDef()) is MethodModification mod ? context.SearchContext.Get(mod) : token;

        public override IMDTokenProvider Transform(IMDTokenProvider token, ITransformContext context)
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

        private MethodDef InjectBase(ITransformContext context)
        {
            MethodDef def = context.GenerateHiddenMethod(context.Method.MethodSig, context.Method.Attributes);
            def.MethodBody = context.Method.MethodBody;
            return def;
        }
    }
}
