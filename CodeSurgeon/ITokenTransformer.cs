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
        IMethodDefOrRef Transform(IMethodDefOrRef token, ITransformContext context);
        IMDTokenProvider Transform(IMDTokenProvider token, ITransformContext context);
        TypeSig Transform(TypeSig token, ITransformContext context);
        MethodSig Transform(MethodSig token, ITransformContext context);
    }

    public class DefaultTokenTransformer : ITokenTransformer
    {
        public virtual ITypeDefOrRef Transform(ITypeDefOrRef token, ITransformContext context) => token;
        public virtual CorLibTypeSig Transform(CorLibTypeSig token, ITransformContext context) => token;
        public virtual IField Transform(IField token, ITransformContext context) => token;
        public virtual IMethodDefOrRef Transform(IMethodDefOrRef token, ITransformContext context) => token;
        public virtual IMDTokenProvider Transform(IMDTokenProvider token, ITransformContext context) => token;
        public virtual TypeSig Transform(TypeSig token, ITransformContext context) => token;
        public virtual MethodSig Transform(MethodSig token, ITransformContext context) => token;
    }
}
