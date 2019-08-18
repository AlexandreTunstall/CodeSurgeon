using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeSurgeon.CLI.TestTarget
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello world!");
            int current = 0;
            foreach (int value in Infinite(() => current++).Take(10)) Console.WriteLine("Counting: " + value);
        }

        private static IEnumerable<T> Infinite<T>(Func<T> generator)
        {
            while (true) yield return generator();
        }
    }
}
