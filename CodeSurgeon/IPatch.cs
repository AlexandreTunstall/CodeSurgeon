using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeSurgeon
{
    public interface IPatch
    {
        string PatchName { get; }
        UTF8String TargetModule { get; }
        IEnumerable<UTF8String> RequiredModules { get; }

        void Required(ModuleDef module);
        void Patch(ModuleDef module);
    }

    public abstract class AbstractPatch : IPatch
    {
        public string PatchName { get; }
        public UTF8String TargetModule { get; }
        public IEnumerable<UTF8String> RequiredModules { get; }

        public AbstractPatch(string patchName, UTF8String targetModule, IEnumerable<UTF8String> requiredModules)
        {
            PatchName = patchName;
            TargetModule = targetModule;
            RequiredModules = requiredModules;
        }

        public abstract void Required(ModuleDef module);
        public abstract void Patch(ModuleDef module);
    }

    public class StandardPatch : AbstractPatch
    {
        protected readonly List<TypeModification> typeMods = new List<TypeModification>();

        public StandardPatch(string patchName, UTF8String targetModule, IEnumerable<UTF8String> requiredModules) : base(patchName, targetModule, requiredModules) { }

        public void Add(TypeModification mod)
        {
            if (mod.IsNested) throw new ArgumentException("attempt to add nested type to patch, add the root declaring type instead");
            typeMods.Add(mod);
        }

        public override void Required(ModuleDef module)
        {
            List<TypeModification> done = new List<TypeModification>();
            foreach (TypeModification mod in typeMods.Where(m => m.ReadOnly))
            {
                TypeDef type = module.Find(new TypeRefUser(module, mod.Namespace, mod.Name));
                if (type == null) continue;
                mod.Apply(type, def => throw new NotSupportedException("attempt to add a type to a read-only module"));
                done.Add(mod);
            }
            typeMods.RemoveAll(done.Contains);
        }

        public override void Patch(ModuleDef module)
        {
            foreach (TypeModification mod in typeMods)
            {
                TypeDef type = module.Find(new TypeRefUser(module, mod.Namespace, mod.Name));
                mod.Apply(type, module.Types.Add);
            }
        }
    }
}
