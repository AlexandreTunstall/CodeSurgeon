using dnlib.DotNet;
using System;
using System.IO;

namespace CodeSurgeon
{
    public interface IStreamSource
    {
        Stream OpenRead(UTF8String assemblyName);
        Stream OpenWrite(UTF8String assemblyName);
    }

    public class FileStreamSource : IStreamSource
    {
        public string Directory { get; }

        public FileStreamSource(string directory = ".") => Directory = directory;

        public virtual Stream OpenRead(UTF8String assemblyName)
        {
            string fileName = assemblyName;
            if (fileName == null || !fileName.IsValidFileName()) return null;
            return File.OpenRead(Path.Combine(Directory, fileName + ".dll"));
        }

        public virtual Stream OpenWrite(UTF8String assemblyName)
        {
            string fileName = assemblyName;
            if (fileName == null || !fileName.IsValidFileName()) return null;
            return File.OpenWrite(Path.Combine(Directory, fileName));
        }
    }
}
