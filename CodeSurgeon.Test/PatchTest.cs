using dnlib.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace CodeSurgeon.Test
{
    [TestClass]
    public class PatchTest
    {
        private const string MissingNamespace = "TestNamespace.DoesNotExist";
        private const string ReadOnlyNamespace = "TestNamespace.ReadOnly";
        private const string NormalClass = "NormalClass";
        private const string NestedClass = "NestedClass";
        private const string MissingMember = "DoesNotExist";
        private const string StringField = "someString";
        private const string NopMethod = "Nop";

        private static ModuleDef module;
        private static MemoryStream stream;

        [ClassInitialize]
        public static void CreateModule(TestContext context)
        {
            stream = new MemoryStream(4096);
            EmitResult result = CSharpCompilation.Create("TestModule", new[] { CSharpSyntaxTree.ParseText(@"
namespace " + ReadOnlyNamespace + @"
{
    class " + NormalClass + @"
    {
        class " + NestedClass + @" { }
        string " + StringField + @";
        void " + NopMethod + @"() { }
    }
}") }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)).Emit(stream);
            if (!result.Success) Assert.Inconclusive("failed to emit test assembly", result.Diagnostics);
            stream.Position = 0L;
            module = ModuleDefMD.Load(stream);
        }

        [ClassCleanup]
        public static void CloseStreams() => stream.Close();

        [TestMethod]
        public void TestDependencyExistingType()
        {
            PatchInstaller installer = new PatchInstaller(new MockModuleSource(module));
            StandardPatch patch = new StandardPatch("TestPatch", module.Name, Enumerable.Empty<UTF8String>());
            patch.Add(new TypeModification.Builder(ReadOnlyNamespace, NormalClass, ModificationKind.FailIfMissing, true)
            {
                Attributes = TypeAttributes.BeforeFieldInit
            }.Build());
            installer.Add(patch);
            installer.Install();
        }

        [TestMethod, ExpectedException(typeof(SymbolInstallException<TypeModification>), "expected install to fail", AllowDerivedTypes = false)]
        public void TestDependencyMissingType()
        {
            PatchInstaller installer = new PatchInstaller(new MockModuleSource(module));
            StandardPatch patch = new StandardPatch("TestPatch", module.Name, Enumerable.Empty<UTF8String>());
            patch.Add(new TypeModification.Builder(MissingNamespace, NormalClass, ModificationKind.FailIfMissing, true)
            {
                Attributes = TypeAttributes.BeforeFieldInit
            }.Build());
            installer.Add(patch);
            installer.Install();
        }

        [TestMethod]
        public void TestDependencyExistingNestedType()
        {
            PatchInstaller installer = new PatchInstaller(new MockModuleSource(module));
            StandardPatch patch = new StandardPatch("TestPatch", module.Name, Enumerable.Empty<UTF8String>());
            TypeModification.Builder type = new TypeModification.Builder(ReadOnlyNamespace, NormalClass, ModificationKind.FailIfMissing, true)
            {
                Attributes = TypeAttributes.BeforeFieldInit
            };
            type.NestedType(NestedClass, ModificationKind.FailIfMissing).Attributes = TypeAttributes.BeforeFieldInit;
            patch.Add(type.Build());
            installer.Add(patch);
            installer.Install();
        }

        [TestMethod, ExpectedException(typeof(SymbolInstallException<TypeModification>), "expected install to fail", AllowDerivedTypes = false)]
        public void TestDependencyMissingNestedType()
        {
            PatchInstaller installer = new PatchInstaller(new MockModuleSource(module));
            StandardPatch patch = new StandardPatch("TestPatch", module.Name, Enumerable.Empty<UTF8String>());
            TypeModification.Builder type = new TypeModification.Builder(ReadOnlyNamespace, NormalClass, ModificationKind.FailIfMissing, true)
            {
                Attributes = TypeAttributes.BeforeFieldInit
            };
            type.NestedType(MissingMember, ModificationKind.FailIfMissing);
            patch.Add(type.Build());
            installer.Add(patch);
            installer.Install();
        }

        [TestMethod]
        public void TestDependencyExistingField()
        {
            PatchInstaller installer = new PatchInstaller(new MockModuleSource(module));
            StandardPatch patch = new StandardPatch("TestPatch", module.Name, Enumerable.Empty<UTF8String>());
            TypeModification.Builder type = new TypeModification.Builder(ReadOnlyNamespace, NormalClass, ModificationKind.FailIfMissing, true)
            {
                Attributes = TypeAttributes.BeforeFieldInit
            };
            type.Field(StringField, new FieldSig(new CorLibTypeSig(new TypeRefUser(null, "System", "String", installer), ElementType.String)), ModificationKind.FailIfMissing);
            patch.Add(type.Build());
            installer.Add(patch);
            installer.Install();
        }

        [TestMethod, ExpectedException(typeof(SymbolInstallException<FieldModification>), "expected install to fail", AllowDerivedTypes = false)]
        public void TestDependencyMissingField()
        {
            PatchInstaller installer = new PatchInstaller(new MockModuleSource(module));
            StandardPatch patch = new StandardPatch("TestPatch", module.Name, Enumerable.Empty<UTF8String>());
            TypeModification.Builder type = new TypeModification.Builder(ReadOnlyNamespace, NormalClass, ModificationKind.FailIfMissing, true)
            {
                Attributes = TypeAttributes.BeforeFieldInit
            };
            type.Field(MissingMember, new FieldSig(new CorLibTypeSig(new TypeRefUser(null, "System", "String", installer), ElementType.String)), ModificationKind.FailIfMissing);
            patch.Add(type.Build());
            installer.Add(patch);
            installer.Install();
        }

        [TestMethod]
        public void TestDependencyExistingMethod()
        {
            PatchInstaller installer = new PatchInstaller(new MockModuleSource(module));
            StandardPatch patch = new StandardPatch("TestPatch", module.Name, Enumerable.Empty<UTF8String>());
            TypeModification.Builder type = new TypeModification.Builder(ReadOnlyNamespace, NormalClass, ModificationKind.FailIfMissing, true)
            {
                Attributes = TypeAttributes.BeforeFieldInit
            };
            type.Method(NopMethod, new MethodSig(CallingConvention.HasThis, 0u, new CorLibTypeSig(new TypeRefUser(null, "System", "Void", installer), ElementType.Void)), ModificationKind.FailIfMissing).Attributes = MethodAttributes.HideBySig;
            patch.Add(type.Build());
            installer.Add(patch);
            installer.Install();
        }

        [TestMethod, ExpectedException(typeof(SymbolInstallException<MethodModification>), "expected install to fail", AllowDerivedTypes = false)]
        public void TestDependencyMissingMethod()
        {
            PatchInstaller installer = new PatchInstaller(new MockModuleSource(module));
            StandardPatch patch = new StandardPatch("TestPatch", module.Name, Enumerable.Empty<UTF8String>());
            TypeModification.Builder type = new TypeModification.Builder(ReadOnlyNamespace, NormalClass, ModificationKind.FailIfMissing, true)
            {
                Attributes = TypeAttributes.BeforeFieldInit
            };
            type.Method(MissingMember, new MethodSig(CallingConvention.ThisCall, 0u, new ClassSig(new TypeRefUser(null, "System", "Void"))), ModificationKind.FailIfMissing);
            patch.Add(type.Build());
            installer.Add(patch);
            installer.Install();
        }

        private class MockModuleSource : IModuleSource
        {
            public ModuleDef Module { get; }

            public MockModuleSource(ModuleDef module) => Module = module;

            public virtual ModuleDef Load(UTF8String moduleName)
            {
                Assert.AreEqual(Module.Name, moduleName, "attempt to load invalid module name");
                return Module;
            }

            public virtual void Save(ModuleDef module) => Assert.AreSame(Module, module, "attempt to save a different module to the one patched");
        }
    }
}
