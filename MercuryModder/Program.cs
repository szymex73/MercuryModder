using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using MercuryModder.Assets;
using MercuryModder.Commands;

namespace MercuryModder;

public class Program {
    static int Main(string[] args)
    {
        var gameDir = new Option<DirectoryInfo>(name: "--gameDir", description: "Path to the game base directory (WindowsNoEditor)") { IsRequired = true };
        var trackDir = new Option<DirectoryInfo>(name: "--tracks", description: "Path to a directory with the custom tracks") { IsRequired = true };
        var outputDir = new Option<DirectoryInfo>(name: "--output", description: "Where to output modified files") { IsRequired = true };

        var rootCommand = new RootCommand("Tool for modding Mercury asset files with custom tracks");

        var prepareCommand = new Command("prepare", "Prepare a track folder with genre folders");
        prepareCommand.AddOption(trackDir);
        rootCommand.AddCommand(prepareCommand);
        prepareCommand.SetHandler(Prepare.Command, trackDir);

        var checkCommand = new Command("check", "Go through the custom tracks and ensure the files are correct.");
        var info = new Option<bool>(name: "--info", description: "Whether to print song information") { IsRequired = false };
        checkCommand.AddOption(trackDir);
        checkCommand.AddOption(info);
        rootCommand.AddCommand(checkCommand);
        checkCommand.SetHandler(Check.Command, trackDir, info);

        var modifyCommand = new Command("modify", "Prepare assets to be replaced in game.");
        var insertFirst = new Option<bool>(name: "--insert-first", description: "Whether to add the new tracks at the start of the list") { IsRequired = false };
        var printModified = new Option<bool>(name: "--print-modified", description: "Whether to print a list of files that will be modified") { IsRequired = false };
        var startId = new Option<int>(name: "--start-id", description: "What ID to start counting from when adding tracks") { IsRequired = false };
        startId.SetDefaultValue(7001);
        modifyCommand.AddOption(trackDir);
        modifyCommand.AddOption(gameDir);
        modifyCommand.AddOption(outputDir);
        modifyCommand.AddOption(insertFirst);
        modifyCommand.AddOption(printModified);
        modifyCommand.AddOption(startId);
        rootCommand.AddCommand(modifyCommand);
        modifyCommand.SetHandler(Modify.Command, trackDir, gameDir, outputDir, insertFirst, printModified, startId);

        var testCommand = new Command("test", "Test");
        testCommand.AddOption(trackDir);
        testCommand.AddOption(gameDir);
        rootCommand.AddCommand(testCommand);
        testCommand.SetHandler(Test.Command, trackDir, gameDir);

        var parser = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseExceptionHandler(Program.ExceptionHandler)
            .Build();

        return parser.Invoke(args);
    }

    internal static void ExceptionHandler(Exception e, InvocationContext c)
    {
        Console.WriteLine("Unhandled exception");
        Console.WriteLine(e);
    }
}
