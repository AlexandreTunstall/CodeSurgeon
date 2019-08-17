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

        private static readonly string[] ExtraReferences = { nameof(CodeSurgeon) + "." + nameof(Attributes), "netstandard" };
        private static readonly string[] References = { "System.Runtime" };

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
}", stream);
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
[module: Required(""" + ModuleName + @""", false)]
namespace TestNamespace
{
    [Dependency, Name(""" + ReadOnlyNamespace + "." + NormalClass + @"""), From(""" + ModuleName + @""")]
    class NormalClassPatch
    {
        [Dependency]
        class " + NestedClass + @" { }
        [Dependency]
        string " + StringField + @";
        [Dependency]
        void " + NopMethod + @"() { }
        string " + StringProperty + @" { get; set; }
    }
}");

        [TestMethod]
        public void TestMixin() => InstallPatch(@"
using CodeSurgeon.Attributes;
[module: Required(""" + ModuleName + @""", false)]
namespace TestNamespace
{
    [Mixin, Name(""" + ReadOnlyNamespace + "." + NormalClass + @"""), From(""" + ModuleName + @""")]
    class NormalClassPatch
    {
        [Mixin]
        public void " + NopMethod + @"() => System.Console.WriteLine(""Hello world!"");
    }
}", "System.Console");

        private void InstallPatch(string source, params string[] references)
        {
            using (MemoryStream stream = new MemoryStream(4096))
            {
                Compile(source, stream, true, references);
                stream.Position = 0L;
                PatchInstaller installer = new PatchInstaller(new MockModuleSource(module));
                installer.Add(ModuleDefMD.Load(stream).CreatePatch("TestPatch"));
                installer.Install();
            }
        }

        private void Compile(string source, MemoryStream stream, bool withAttributes = false, params string[] references)
        {
            Debug.WriteLine("=== New Compilation Requested ===");
            EmitResult result = CSharpCompilation.Create(ModuleName, new[] { CSharpSyntaxTree.ParseText(source) }, Reference(withAttributes ? References.Concat(ExtraReferences).Concat(references) : References), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)).Emit(stream);
            if (!result.Success) Assert.Inconclusive("failed to emit test assembly");
        }

        private IEnumerable<MetadataReference> Reference(params string[] names) => Reference((IEnumerable<string>)names);
        private IEnumerable<MetadataReference> Reference(IEnumerable<string> names) => DependencyContext.Default.CompileLibraries.SelectMany(cl => cl.ResolveReferencePaths()).Where(p => names.Contains(Path.GetFileNameWithoutExtension(p))).Select(p => MetadataReference.CreateFromFile(p));

        private class MockModuleSource : IModuleSource
        {
            public ModuleDef Module { get; }

            private readonly UTF8String name;

            public MockModuleSource(ModuleDef module) => name = StripExtension((Module = module).Name);

            public virtual ModuleDef Load(UTF8String moduleName)
            {
                Assert.AreEqual(name, moduleName, "attempt to load invalid module name");
                return Module;
            }

            public virtual void Save(ModuleDef module) => Assert.AreSame(Module, module, "attempt to save a different module to the one patched");

            private UTF8String StripExtension(UTF8String path) => path.Substring(0, path.LastIndexOf('.'));
        }
    }
}
