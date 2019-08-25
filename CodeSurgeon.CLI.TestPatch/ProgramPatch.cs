using CodeSurgeon.Attributes;
using CodeSurgeon.CLI.TestTarget;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[module: Required("CodeSurgeon.CLI.TestTarget", false), Required("System.Runtime", true), Required("System.Runtime.Extensions", true), Required("System.Console", true)]

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

        [Mixin]
        private static IEnumerable<T> Infinite<T>(Func<T> generator)
        {
            for (; true; generator()) yield return generator();
        }

        [BaseDependency]
        private static void Base(string[] args) => throw new NotImplementedException();
    }
}
