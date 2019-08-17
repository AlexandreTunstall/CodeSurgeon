using CodeSurgeon.Attributes;
using CodeSurgeon.CLI.TestTarget;
using System;

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
            Console.WriteLine("Goodbye, world!");
        }

        [BaseDependency]
        private static void Base(string[] args) => throw new NotImplementedException();
    }
}
