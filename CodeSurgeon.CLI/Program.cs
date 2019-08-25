using CodeSurgeon.Module;
using dnlib.DotNet;
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodeSurgeon.CLI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CommandLineApplication app = new CommandLineApplication()
            {
                Name = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]),
                Description = "Install compiled patches into assemblies."
            };
            app.HelpOption("-?|-h|--help");
            CommandOption patches = app.Option("-p|--patch <file>", "Path to a patch that should be installed", CommandOptionType.MultipleValue);
            CommandOption targets = app.Option("-t|--target <dir>", "Directory to search for the libraries onto which the patches should be installed", CommandOptionType.MultipleValue);
            CommandOption output = app.Option("-o|--output <dir>", "The directory to which the patched libraries should be written, ignored if --readonly is specified", CommandOptionType.SingleValue);
            CommandOption noWrite = app.Option("-r|--readonly", "Treat libraries as read-only and fail if anything is modified", CommandOptionType.NoValue);
            app.OnExecute(() =>
            {
                if (!patches.HasValue() || !targets.HasValue())
                {
                    Console.WriteLine("Please specify a patch and target directory");
                    app.ShowHint();
                    return 0;
                }
                IEnumerable<IStreamSource> sources = targets.Values.Select<string, IStreamSource>(t => new FileStreamSource(t)).ToList();
                IStreamSource source;
                if (output.HasValue())
                {
                    Directory.CreateDirectory(output.Value());
                    source = new WritableStreamSource(sources, new FileStreamSource(output.Value()));
                }
                else if (noWrite.HasValue())
                {
                    Console.WriteLine("Attempting a read-only install, failing if the patch makes any changes");
                    source = new ReadOnlyStreamSource(sources, true);
                }
                else
                {
                    Console.WriteLine("Output not specified, attempting an install without saving");
                    source = new ReadOnlyStreamSource(sources, false);
                }
                PatchInstaller installer = new PatchInstaller(new StreamModuleSource(source));
                foreach (string patch in patches.Values) installer.Add(ModuleDefMD.Load(patch).CreatePatch());
                installer.Install();
                return 0;
            });
            try
            {
                app.Execute(args);
            }
            catch (CommandParsingException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception occurred whilst installing");
                Console.WriteLine(e);
            }
        }
    }
}
