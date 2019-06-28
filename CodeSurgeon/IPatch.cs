using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeSurgeon
{
    public interface IPatch
    {
        string PatchName { get; }
        IEnumerable<ModuleModification> Modules { get; }

        void Patch(ISearchContext context);
    }

    public abstract class AbstractPatch : IPatch
    {
        public string PatchName { get; }
        public abstract IEnumerable<ModuleModification> Modules { get; }

        public AbstractPatch(string patchName) => PatchName = patchName;

        public virtual void Patch(ISearchContext context)
        {
            foreach (ModuleModification mod in Modules) mod.Apply(context);
        }
    }

    public class StandardPatch : AbstractPatch
    {
        public override IEnumerable<ModuleModification> Modules => modules.Values;

        protected readonly Dictionary<UTF8String, ModuleModification> modules = new Dictionary<UTF8String, ModuleModification>();

        public StandardPatch(string patchName) : base(patchName) { }

        public ModuleModification Module(UTF8String name, ModificationKind kind, bool readOnly)
        {
            lock (modules)
            {
                if (!modules.TryGetValue(name, out ModuleModification module)) modules.Add(name, module = new ModuleModification(name, kind, readOnly));
                else if (module.Kind != kind) throw new ArgumentException("existing module " + name.String + " has kind " + module.Kind + " but kind " + kind + " was requested");
                else if (module.ReadOnly != readOnly) throw new ArgumentException("existing module " + name.String + " is " + (module.ReadOnly ? "read-only" : "writable") + " but " + (readOnly ? "read-only" : "writable") + " was requested");
                return module;
            }
        }
    }
}
