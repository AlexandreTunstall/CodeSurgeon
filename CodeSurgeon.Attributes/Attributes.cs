using System;

namespace CodeSurgeon.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Event)]
    public class NameAttribute : Attribute
    {
        public string Value { get; }
        public string Assembly { get; }

        public NameAttribute(string value) => Value = value;
        public NameAttribute(Type type) => Value = type.FullName.Replace('+', '/');
    }

    [AttributeUsage(AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate)]
    public class FromAttribute : Attribute
    {
        public string Assembly { get; }

        public FromAttribute(string assembly) => Assembly = assembly;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Event)]
    public class DependencyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Event)]
    public class InjectAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Event)]
    public class MixinAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method)]
    public class BaseDependencyAttribute : Attribute { }
}
