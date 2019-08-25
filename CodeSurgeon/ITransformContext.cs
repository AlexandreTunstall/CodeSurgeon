using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSurgeon
{
    public interface ITransformContext
    {
        ISearchContext SearchContext { get; }
        MethodDef Method { get; }

        MethodDef GenerateHiddenMethod(MethodSig methodSig, MethodAttributes attributes);
    }

    public class StandardTransformContext : ITransformContext
    {
        private const MethodAttributes GenAttributes = MethodAttributes.CompilerControlled;
        private const MethodAttributes MergeAttributes = MethodAttributes.Static;

        public ISearchContext SearchContext { get; }
        public MethodDef Method { get; }

        public StandardTransformContext(ISearchContext searchContext, MethodDef method)
        {
            SearchContext = searchContext;
            Method = method;
        }

        public MethodDef GenerateHiddenMethod(MethodSig methodSig, MethodAttributes attributes) => new MethodDefUser(FindUnusedName(Method.DeclaringType, Method.Name + "<Base>$"), methodSig, GenAttributes | attributes & MergeAttributes)
        {
            DeclaringType = Method.DeclaringType
        };

        private UTF8String FindUnusedName(TypeDef type, UTF8String baseName)
        {
            for (ulong index = 0uL; index < ulong.MaxValue; index++)
            {
                UTF8String proposed = baseName + index;
                if (type.Methods.All(m => m.Name != proposed)) return proposed;
            }
            throw new ArgumentException("could not find unused method name for " + type + " with base name " + baseName);
        }
    }
}
