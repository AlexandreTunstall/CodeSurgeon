using System;
using System.Reflection;
using System.Text;

namespace CodeSurgeon.Attributes
{
    [AttributeUsage(AttributeTargets.Module, AllowMultiple = true)]
    public class RequiredAttribute : Attribute
    {
        public byte[] Assembly { get; }
        public bool ReadOnly { get; }

        public RequiredAttribute(string assembly, bool readOnly) : this(Encoding.UTF8.GetBytes(assembly), readOnly) { }
        public RequiredAttribute(byte[] assembly, bool readOnly)
        {
            Assembly = assembly;
            ReadOnly = readOnly;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Event)]
    public class NameAttribute : Attribute
    {
        public byte[] Value { get; }
        public byte[] Assembly { get; }

        public NameAttribute(string value) : this(Encoding.UTF8.GetBytes(value)) { }
        public NameAttribute(byte[] value) => Value = value;
        public NameAttribute(Type type) : this(type.FullName.Replace('+', '/')) => Assembly = Encoding.UTF8.GetBytes(type
#if NETSTANDARD
            .GetTypeInfo()
#endif
            .Assembly.GetName().Name);

        private static Assembly GetAssembly(Type type)
#if NETSTANDARD
            => type.GetTypeInfo().Assembly;
#else
            => type.Assembly;
#endif
    }

    [AttributeUsage(AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate)]
    public class FromAttribute : Attribute
    {
        public byte[] Assembly { get; }

        public FromAttribute(string assembly) : this(Encoding.UTF8.GetBytes(assembly)) { }
        public FromAttribute(byte[] assembly) => Assembly = assembly;
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
