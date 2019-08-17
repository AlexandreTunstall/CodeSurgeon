using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSurgeon
{
    public interface ITokenTransformer
    {
        ITypeDefOrRef Transform(ITypeDefOrRef token, ITransformContext context);
        CorLibTypeSig Transform(CorLibTypeSig token, ITransformContext context);
        IField Transform(IField token, ITransformContext context);
        IMethod Transform(IMethod token, ITransformContext context);
        IMDTokenProvider Transform(IMDTokenProvider token, ITransformContext context);
    }

    public class DefaultTokenTransformer : ITokenTransformer
    {
        public virtual ITypeDefOrRef Transform(ITypeDefOrRef token, ITransformContext context) => token;
        public virtual CorLibTypeSig Transform(CorLibTypeSig token, ITransformContext context) => token;
        public virtual IField Transform(IField token, ITransformContext context) => token;
        public virtual IMethod Transform(IMethod token, ITransformContext context) => token;
        public virtual IMDTokenProvider Transform(IMDTokenProvider token, ITransformContext context) => token;
    }

    public class ResolvingTokenTransformer : DefaultTokenTransformer
    {
        private readonly ITokenResolver resolver;

        public ResolvingTokenTransformer(ITokenResolver resolver) => this.resolver = resolver;

        public override IMDTokenProvider Transform(IMDTokenProvider token, ITransformContext context) => resolver.ResolveToken(token.Rid);
    }
}
