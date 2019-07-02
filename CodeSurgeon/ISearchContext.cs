using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSurgeon
{
    public interface ISearchContext
    {
        ModuleDef Get(UTF8String moduleName);
        TSymbol Get<TSymbol>(IModification<TSymbol> modification) where TSymbol : class, IDnlibDef;
    }

    public abstract class CachedSearchContext : ISearchContext
    {
        public IModuleSource Modules { get; }

        protected readonly Dictionary<UTF8String, ModuleDef> modules = new Dictionary<UTF8String, ModuleDef>();
        protected readonly Dictionary<IModification<IDnlibDef>, IDnlibDef> symbols = new Dictionary<IModification<IDnlibDef>, IDnlibDef>();

        public CachedSearchContext(IModuleSource modules) => Modules = modules;

        public virtual void Add(UTF8String name, ModuleDef module) => modules.Add(name, module);

        public virtual ModuleDef Get(UTF8String name)
        {
            lock (modules)
            {
                if (!modules.TryGetValue(name, out ModuleDef module)) modules.Add(name, module = Modules.Load(name));
                return module;
            }
        }

        public virtual TSymbol Get<TSymbol>(IModification<TSymbol> modification) where TSymbol : class, IDnlibDef
        {
            lock (symbols)
            {
                if (symbols.TryGetValue(modification, out IDnlibDef value)) return (TSymbol)value;
                TSymbol result = modification.Resolve(this);
                symbols.Add(modification, result);
                return result;
            }
        }
    }
}
