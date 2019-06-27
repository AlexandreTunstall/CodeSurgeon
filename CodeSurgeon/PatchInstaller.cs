using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSurgeon
{
    public class PatchInstaller : IResolutionScope
    {
        public IModuleSource Modules { get; }

        protected readonly List<IPatch> patches = new List<IPatch>();

        public PatchInstaller(IModuleSource modules) => Modules = modules;

        public void Add(IPatch patch)
        {
            if (patch == null) throw new ArgumentNullException("patch");
            patches.Add(patch);
        }

        public virtual void Install()
        {
            lock (patches) foreach (IPatch patch in patches) InstallPatch(patch);
        }

        protected virtual void InstallPatch(IPatch patch)
        {
            try
            {
                foreach (UTF8String name in patch.RequiredModules) patch.Required(Modules.Load(name));
                ModuleDef target = Modules.Load(patch.TargetModule);
                patch.Patch(target);
                Modules.Save(target);
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
