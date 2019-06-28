using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSurgeon
{
    public class PatchInstaller : CachedSearchContext, IResolutionScope
    {
        protected readonly List<IPatch> patches = new List<IPatch>();

        public PatchInstaller(IModuleSource modules) : base(modules) { }

        public void Add(IPatch patch)
        {
            if (patch == null) throw new ArgumentNullException("patch");
            patches.Add(patch);
        }

        public virtual void Install()
        {
            lock (patches) foreach (IPatch patch in patches) InstallPatch(patch);
            foreach (ModuleDef module in modules.Values) Modules.Save(module);
        }

        protected virtual void InstallPatch(IPatch patch)
        {
            try
            {
                patch.Patch(this);
            }
            catch (Exception e) when (!(e is InstallException))
            {
                throw new InstallException("failed to install " + patch.PatchName, e);
            }
        }

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
