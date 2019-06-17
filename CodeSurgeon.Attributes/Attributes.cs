using System;

namespace CodeSurgeon.Attributes
{
    [AttributeUsage(AttributeTargets.Module)]
    public class PatchAttribute : Attribute
    {
        public string Name { get; }

        public PatchAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Module, AllowMultiple = true)]
    public class RequireAttribute : Attribute
    {
        public string Name { get; }

        public RequireAttribute(string name) => Name = name;
    }
}
