using dnlib.DotNet;
using System;
using System.Linq;

namespace CodeSurgeon.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Loading");
            PatchInstaller installer = new PatchInstaller(new StreamModuleSource(new FileStreamSource("../../../../CodeSurgeon/bin/Debug/netstandard2.0")));
            StandardPatch testPatch = new StandardPatch("TestPatch", "CodeSurgeon", Enumerable.Empty<UTF8String>());
            TypeModification.Builder programType = new TypeModification.Builder("CodeSurgeon", "PatchInstaller", ModificationKind.FailIfMissing, true)
            {
                Attributes = TypeAttributes.BeforeFieldInit
            };
            programType.Field("<Modules>k__BackingField", new FieldSig(new ClassSig(new TypeRefUser(null, "CodeSurgeon", "IModuleSource", installer))), ModificationKind.FailIfMissing).Attributes = FieldAttributes.InitOnly;
            programType.Field("patches", new FieldSig(new GenericInstSig(new ClassSig(new TypeRefUser(null, "System.Collections.Generic", "List`1", installer)), new ClassSig(new TypeRefUser(null, "CodeSurgeon", "IPatch", installer)))), ModificationKind.FailIfMissing).Attributes = FieldAttributes.InitOnly;
            testPatch.Add(programType.Build());
            installer.Add(testPatch);
            Console.WriteLine("Installing");
            installer.Install();
            Console.WriteLine("Done");
        }
    }
}
