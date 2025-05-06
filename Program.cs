// var converter = new XacroConverter(Path.GetFullPath("../../../tests/motoman_hc10_support/urdf/hc10.xacro"), "../../../output/hc10.urdf");
// converter.Convert();

using System;
using System.CommandLine;
using System.IO;

namespace XacroProcessor
{
    class Program
    {
        static int Main(string[] args)
        {
            // Create the input option
            var inputOption = new Option<FileInfo>(
                aliases: new[] { "--input", "-i" },
                description: "The XACRO file to process")
            {
                IsRequired = true
            };

            // Create the root command
            var rootCommand = new RootCommand("XACRO file processor");
            rootCommand.AddOption(inputOption);
            
            // Set the handler
            rootCommand.SetHandler((FileInfo file) =>
            {
                // Validate file exists
                if (!file.Exists)
                {
                    Console.Error.WriteLine($"Error: The file '{file.FullName}' does not exist.");
                    Environment.Exit(1);
                }

                // Validate file is of type XACRO
                if (!file.Extension.Equals(".xacro", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"Error: The file '{file.FullName}' is not a XACRO file. File extension should be .xacro");
                    Environment.Exit(1);
                }

                // Process the XACRO file
                Console.WriteLine($"Processing XACRO file: {file.FullName}");
                
                new XacroConverter(file.FullName, Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.Name) + ".urdf")).Convert();
                Console.WriteLine($"Converted to URDF file: {Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.Name) + ".urdf")}");
            }, inputOption);

            return rootCommand.Invoke(args);
        }
    }
}