using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSurgeon
{
    public interface ITokenTransformer
    {
        ITypeDefOrRef Transform(ITypeDefOrRef token, ISearchContext context);
        CorLibTypeSig Transform(CorLibTypeSig token, ISearchContext context);
        IField Transform(IField token, ISearchContext context);
        IMethod Transform(IMethod token, ISearchContext context);
        IMDTokenProvider Transform(IMDTokenProvider token, ISearchContext context);
    }

    public class DefaultTokenTransformer : ITokenTransformer
    {
        public virtual ITypeDefOrRef Transform(ITypeDefOrRef token, ISearchContext context) => token;
        public virtual CorLibTypeSig Transform(CorLibTypeSig token, ISearchContext context) => token;
        public virtual IField Transform(IField token, ISearchContext context) => token;
        public virtual IMethod Transform(IMethod token, ISearchContext context) => token;
        public virtual IMDTokenProvider Transform(IMDTokenProvider token, ISearchContext context) => token;
    }

    public class ResolvingTokenTransformer : DefaultTokenTransformer
    {
        private readonly ITokenResolver resolver;

        public ResolvingTokenTransformer(ITokenResolver resolver) => this.resolver = resolver;

        public override IMDTokenProvider Transform(IMDTokenProvider token, ISearchContext context) => resolver.ResolveToken(token.Rid);
    }
}
