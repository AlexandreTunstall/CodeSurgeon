using CodeSurgeon.Attributes;
using CodeSurgeon.CLI.TestTarget;
using System;
using System.Runtime.CompilerServices;

[module: Required("CodeSurgeon.CLI.TestTarget", false)]

namespace CodeSurgeon.CLI.TestPatch
{
    [Mixin, Name(typeof(Program))]
    public class ProgramPatch
    {
        [Mixin]
        public static void Main(string[] args)
        {
            Base(args);
            Console.WriteLine(GetMessage());
        }

        [Inject]
        public static string GetMessage() => "Goodbye, world!";

        [BaseDependency]
        private static void Base(string[] args) => throw new NotImplementedException();
    }
}
