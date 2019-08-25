using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSurgeon
{
    public class PatchInstaller : CachedSearchContext
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
            foreach (ModuleModification module in patches.SelectMany(p => p.Modules).Where(m => !m.ReadOnly).Distinct()) Modules.Save(Get(module));
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
    }
}
