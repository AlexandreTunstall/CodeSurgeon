using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSurgeon
{
    public interface ISearchContext : IResolutionScope
    {
        ModuleDef Get(UTF8String moduleName);
        TSymbol Get<TSymbol>(IResolvable<TSymbol> reference);
    }

    public abstract class CachedSearchContext : DummyResolutionScope, ISearchContext
    {
        public IModuleSource Modules { get; }

        protected readonly Dictionary<UTF8String, ModuleDef> modules = new Dictionary<UTF8String, ModuleDef>();
        protected readonly Dictionary<IModification<IDnlibDef>, object> symbols = new Dictionary<IModification<IDnlibDef>, object>();

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

        public virtual TSymbol Get<TSymbol>(IResolvable<TSymbol> resolvable)
        {
            // Don't cache if it's not a modification
            if (!(resolvable is IModification<IDnlibDef> mod)) return resolvable.Resolve(this);
            lock (symbols)
            {
                if (symbols.TryGetValue(mod, out object value)) return (TSymbol)value;
                TSymbol result = resolvable.Resolve(this);
                symbols.Add(mod, result);
                return result;
            }
        }
    }

    public interface IResolvable<out TSymbol>
    {
        TSymbol Resolve(ISearchContext context);
    }
}
