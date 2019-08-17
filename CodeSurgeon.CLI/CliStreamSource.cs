using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace CodeSurgeon.CLI
{
    public abstract class CliStreamSource : IStreamSource
    {
        protected IEnumerable<IStreamSource> ReadSources { get; }

        private protected CliStreamSource(IEnumerable<IStreamSource> sources) => ReadSources = sources;

        public Stream OpenRead(UTF8String assemblyName) => ReadSources.Select(src => src.OpenRead(assemblyName)).FirstOrDefault();

        public abstract Stream OpenWrite(UTF8String assemblyName);
    }

    public sealed class ReadOnlyStreamSource : CliStreamSource
    {
        private readonly bool failOnWrite;

        public ReadOnlyStreamSource(IEnumerable<IStreamSource> sources, bool failOnWrite) : base(sources) => this.failOnWrite = failOnWrite;

        public override Stream OpenWrite(UTF8String assemblyName) => failOnWrite ? null : new MemoryStream(4096);
    }

    public sealed class WritableStreamSource : CliStreamSource
    {
        private readonly IStreamSource writeSource;

        public WritableStreamSource(IEnumerable<IStreamSource> readSources, IStreamSource writeSource) : base(readSources) => this.writeSource = writeSource;

        public override Stream OpenWrite(UTF8String assemblyName) => writeSource.OpenWrite(assemblyName);
    }
}
