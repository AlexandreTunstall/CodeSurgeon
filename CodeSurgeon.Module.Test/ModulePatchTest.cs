using CodeSurgeon.Attributes;
using dnlib.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.DependencyModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CodeSurgeon.Module.Test
{
    [TestClass]
    public class ModulePatchTest
    {
        private const string ModuleName = "TestModule";
        private const string MissingNamespace = "TestNamespace.DoesNotExist";
        private const string ReadOnlyNamespace = "TestNamespace.ReadOnly";
        private const string NormalClass = "NormalClass";
        private const string NestedClass = "NestedClass";
        private const string MissingMember = "DoesNotExist";
        private const string StringField = "someStringField";
        private const string NopMethod = "Nop";
        private const string StringProperty = "SomeStringProperty";

        private static readonly string[] RequiredReferences = { "System.Runtime" };
        private static readonly IEnumerable<string> AllReferences = new string[] { nameof(CodeSurgeon) + "." + nameof(Attributes), "netstandard" }.Concat(RequiredReferences);

        private ModuleDef module;
        private MemoryStream stream;

        [TestInitialize]
        public void CreateModule()
        {
            stream = new MemoryStream(4096);
            Compile(@"
namespace " + ReadOnlyNamespace + @"
{
    class " + NormalClass + @"
    {
        class " + NestedClass + @" { }
        string " + StringField + @";
        void " + NopMethod + @"() { }
        string " + StringProperty + @" { get; set; }
    }
}", stream, Locate(RequiredReferences));
            stream.Position = 0L;
            module = ModuleDefMD.Load(stream);
        }

        [TestCleanup]
        public void CloseStreams() => stream.Close();

        [TestMethod]
        public void TestEmpty() => InstallPatch("");

        [TestMethod]
        public void TestDependency() => InstallPatch(@"
using CodeSurgeon.Attributes;
[module: Required(""" + module.Assembly.Name + @""", false), Required(""System.Runtime"", true)]
namespace TestNamespace
{
    [Dependency, Name(""" + ReadOnlyNamespace + "." + NormalClass + @"""), From(""" + module.Assembly.Name + @""")]
    class NormalClassPatch
    {
        [Dependency]
        class " + NestedClass + @" { }
        [Dependency]
        string " + StringField + @";
        [Dependency]
        void " + NopMethod + @"() { }
        [Dependency]
        string " + StringProperty + @" { get; set; }
    }
}");

        [TestMethod]
        public void TestMixin() => InstallPatch(@"
using CodeSurgeon.Attributes;
[module: Required(""" + module.Assembly.Name + @""", false), Required(""System.Runtime"", true), Required(""System.Console"", true)]
namespace TestNamespace
{
    [Mixin, Name(""" + ReadOnlyNamespace + "." + NormalClass + @"""), From(""" + module.Assembly.Name + @""")]
    class NormalClassPatch
    {
        [Mixin]
        public void " + NopMethod + @"() => System.Console.WriteLine(""Hello world!"");
    }
}", "System.Console");
        
        [TestMethod]
        public void TestCompilerGenerated() => InstallPatch(@"
using CodeSurgeon.Attributes;
using System.Collections.Generic;
[module: Required(""" + module.Assembly.Name + @""", false), Required(""System.Runtime"", true)]
namespace TestNamespace
{
    [Mixin, Name(""" + ReadOnlyNamespace + "." + NormalClass + @"""), From(""" + module.Assembly.Name + @""")]
    class NormalClassPatch
    {
        [Inject]
        IEnumerable<int> " + MissingMember + @"()
        {
            yield return 0;
        }
    }
}
");

        private void InstallPatch(string source, params string[] references)
        {
            using (MemoryStream stream = new MemoryStream(4096))
            {
                List<string> paths = Locate(references.Concat(AllReferences)).ToList();
                Compile(source, stream, paths);
                stream.Position = 0L;
                PatchInstaller installer = new PatchInstaller(new MockModuleSource(module, stream, paths.ToDictionary<string, UTF8String, ModuleDef>(p => Path.GetFileNameWithoutExtension(p), p => ModuleDefMD.Load(p))));
                installer.Add(ModuleDefMD.Load(stream).CreatePatch("TestPatch"));
                installer.Install();
            }
        }

        private void Compile(string source, MemoryStream stream, params string[] references) => Compile(source, stream, (IEnumerable<string>)references);
        private void Compile(string source, MemoryStream stream, IEnumerable<string> references)
        {
            Debug.WriteLine("=== New Compilation Requested ===");
            EmitResult result = CSharpCompilation.Create(ModuleName, new[] { CSharpSyntaxTree.ParseText(source) }, references.Select(p => MetadataReference.CreateFromFile(p)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)).Emit(stream);
            if (!result.Success) Assert.Inconclusive("failed to emit test assembly");
        }

        private IEnumerable<string> Locate(IEnumerable<string> names) => DependencyContext.Default.CompileLibraries.SelectMany(cl => cl.ResolveReferencePaths()).Where(p => names.Contains(Path.GetFileNameWithoutExtension(p)));

        private class MockModuleSource : IModuleSource
        {
            public ModuleDef Module { get; }
            public UTF8String Name { get; }

            private readonly Stream stream;
            private readonly Dictionary<UTF8String, ModuleDef> modules;

            public MockModuleSource(ModuleDef module, Stream stream, IReadOnlyDictionary<UTF8String, ModuleDef> references)
            {
                Name = (Module = module).Assembly.Name;
                this.stream = stream;
                (modules = new Dictionary<UTF8String, ModuleDef>(references)).Add(Name, module);
            }

            public virtual ModuleDef Load(UTF8String moduleName)
            {
                if (!modules.TryGetValue(moduleName, out ModuleDef module)) Assert.Fail("attempt to load invalid module name: " + moduleName);
                return module;
            }

            public virtual void Save(ModuleDef module)
            {
                Assert.AreSame(Module, module, "attempt to save a different module to the one being patched");
                module.Write(stream);
            }
        }
    }
}
