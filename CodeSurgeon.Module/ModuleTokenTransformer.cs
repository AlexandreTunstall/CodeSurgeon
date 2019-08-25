using System;
using System.Collections.Generic;
using System.Text;
using dnlib.DotNet;

namespace CodeSurgeon.Module
{
    internal class ModuleTokenTransformer : DefaultTokenTransformer
    {
        private readonly ModuleImporter importer;
        private readonly ModuleDef module;

        public ModuleTokenTransformer(ModuleImporter importer, ModuleDef module)
        {
            this.importer = importer;
            this.module = module;
        }

        public override ITypeDefOrRef Transform(ITypeDefOrRef token, ITransformContext context) => importer.Import(token) is ITypeReference<ITypeDefOrRef> imported ? context.SearchContext.Get(imported) : token;

        public override CorLibTypeSig Transform(CorLibTypeSig token, ITransformContext context) => context.SearchContext.Get(importer.Import(token));

        public override IField Transform(IField token, ITransformContext context) => importer.Import(token) is IFieldReference<IField> imported ? context.SearchContext.Get(imported) : token;
        
        public override IMethodDefOrRef Transform(IMethodDefOrRef token, ITransformContext context) => token.GetLeafMethod() is IHasCustomAttribute hasAttr && hasAttr.IsBaseDependency() ? InjectBase(context) : importer.Import(token) is IMethodReference<IMethodDefOrRef> imported ? context.SearchContext.Get(imported) : token;

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

        public override TypeSig Transform(TypeSig token, ITransformContext context) => context.SearchContext.Get(importer.Import(token));

        public override MethodSig Transform(MethodSig token, ITransformContext context) => context.SearchContext.Get(importer.Import(token));

        private MethodDef InjectBase(ITransformContext context)
        {
            MethodDef def = context.GenerateHiddenMethod(context.Method.MethodSig, context.Method.Attributes);
            def.MethodBody = context.Method.MethodBody;
            return def;
        }
    }
}
