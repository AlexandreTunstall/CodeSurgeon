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
}
