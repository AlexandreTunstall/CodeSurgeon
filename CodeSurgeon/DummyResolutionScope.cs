using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSurgeon
{
    public class DummyResolutionScope : IResolutionScope
    {
        int IResolutionScope.ResolutionScopeTag => throw new NotImplementedException();
        int IHasCustomAttribute.HasCustomAttributeTag => throw new NotImplementedException();
        CustomAttributeCollection IHasCustomAttribute.CustomAttributes => throw new NotImplementedException();
        bool IHasCustomAttribute.HasCustomAttributes => throw new NotImplementedException();
        MDToken IMDTokenProvider.MDToken => throw new NotImplementedException();
        uint IMDTokenProvider.Rid { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        string IFullName.FullName => throw new NotImplementedException();
        UTF8String IFullName.Name { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}
