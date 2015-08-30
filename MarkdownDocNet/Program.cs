using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace MarkdownDocNet
{
    class Program
    {
        public static void PrintUsage(string error=null)
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            Console.WriteLine("MarkdownDocNet v" + version);

            if (!String.IsNullOrEmpty(error))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: " + error);
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.WriteLine("Usage: MarkdownDocNet <documentation.xml> <assembly.dll> <output.md>");
            Console.WriteLine("This will take the .NET XML documentation file and the corresponding Assembly");
            Console.WriteLine("and transform the included documentation into human-readable markdown code.");
            Console.WriteLine("All documentation will be written to <output.md>.");
        }

        static int Main(string[] args)
        {
            if (args.Length > 0 &&
                (  args[0] == "--help"
                || args[0] == "-h"
                || args[0] == "/h"
                || args[0] == "-?"
                || args[0] == "/?"
                || args[0] == "--version"))
            {
                PrintUsage();
                return 0;
            }

            else if (args.Length < 3)
            {
                PrintUsage("Too few arguments");
                return 1;
            }
            else if (args.Length > 3)
            {
                PrintUsage("Too many arguments");
                return 1;
            }
            
            var docFile = args[0];
            var assemblyFile = args[1];
            var outputFileRelative = args[2];

            docFile = Path.GetFullPath(docFile);
            assemblyFile = Path.GetFullPath(assemblyFile);
            var outputFile = Path.GetFullPath(outputFileRelative);

            Console.WriteLine("Writing documentation to " + outputFileRelative);

            var parser = new DocParser(docFile, assemblyFile, outputFile);
            parser.ParseXml();
            parser.GenerateDoc();

            Console.WriteLine("Done.");

            return 0;
        }
    }
}
