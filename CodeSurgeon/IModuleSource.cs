using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CodeSurgeon
{
    public interface IModuleSource
    {
        ModuleDef Load(UTF8String moduleName);
        void Save(ModuleDef module);
    }

    public class StreamModuleSource : IModuleSource
    {
        public IStreamSource Streams { get; }

        public StreamModuleSource(IStreamSource streams) => Streams = streams;

        public virtual ModuleDef Load(UTF8String moduleName)
        {
            using (Stream stream = Streams.OpenRead(moduleName) ?? throw new InstallException("cannot open assembly " + moduleName)) return ModuleDefMD.Load(stream);
        }

        public virtual void Save(ModuleDef module)
        {
            using (Stream stream = Streams.OpenWrite(module.Name) ?? throw new InstallException("cannot open assembly " + module.Name)) module.Write(stream);
        }
    }
}
