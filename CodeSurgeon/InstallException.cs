using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSurgeon
{
    public class InstallException : Exception
    {
        public InstallException() { }
        public InstallException(string message) : base(message) { }
        public InstallException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    public class ReadOnlyInstallException : Exception
    {   // Only used as an inner exception to detail why
        private const string DefaultMessage = "existing read-only definition is incompatible with the patch";

        public ReadOnlyInstallException() : base(DefaultMessage) { }
        public ReadOnlyInstallException(Exception cause) : base(DefaultMessage, cause) { }
    }

    public class SymbolInstallException : InstallException
    {
        public IModification Modification { get; }

        public SymbolInstallException(IModification mod) : base(GetMessage(mod)) => Modification = mod;
        public SymbolInstallException(IModification mod, Exception cause) : base(GetMessage(mod), cause) => Modification = mod;

        private static string GetMessage(IModification mod) => "failed to install " + mod.SymbolKind.ToString().ToLower() + " " + mod.FullName;
    }

    public class SymbolInstallException<TSymbol> : SymbolInstallException where TSymbol : IModification
    {
        public new TSymbol Modification { get; }

        public SymbolInstallException(TSymbol mod) : base(mod) => Modification = mod;
        public SymbolInstallException(TSymbol mod, Exception cause) : base(mod, cause) => Modification = mod;
    }
}
