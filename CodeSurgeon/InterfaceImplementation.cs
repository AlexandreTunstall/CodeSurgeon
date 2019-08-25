using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSurgeon
{
    public sealed class InterfaceImplementation : IResolvable<InterfaceImpl>
    {
        public ITypeReference<ITypeDefOrRef> Interface { get; }

        public InterfaceImplementation(ITypeReference<ITypeDefOrRef> @interface) => Interface = @interface;

        public InterfaceImpl Resolve(ISearchContext context) => new InterfaceImplUser(context.Get(Interface));
    }

    public sealed class InterfaceMethodOverride : IResolvable<MethodOverride>
    {
        public IMethodReference<IMethodDefOrRef> MethodBody { get; }
        public IMethodReference<IMethodDefOrRef> MethodDeclaration { get; }

        public InterfaceMethodOverride(IMethodReference<IMethodDefOrRef> methodBody, IMethodReference<IMethodDefOrRef> methodDeclaration)
        {
            MethodBody = methodBody;
            MethodDeclaration = methodDeclaration;
        }

        public MethodOverride Resolve(ISearchContext context) => new MethodOverride(context.Get(MethodBody), context.Get(MethodDeclaration));
    }
}
