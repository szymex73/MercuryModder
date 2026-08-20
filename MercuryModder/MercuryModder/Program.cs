using System;
using System.CommandLine;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using MercuryModder.Assets;
using MercuryModder.Commands;

namespace MercuryModder;

public class Program {
    static ICommand[] Commands = new ICommand[]
    {
        new AcbCommand(),
        new AudioSwapCommand(),
        new CheckCommand(),
        new ModifyCommand(),
        new PrepareCommand(),
        new SwapBgmCommand(),
        new TestCommand()
    };
    static int Main(string[] args)
    {
        var rootCommand = new RootCommand("Tool for modding Mercury asset files with custom tracks");

        foreach (var command in Commands)
        {
            rootCommand.Add(command.Build());
        }

        return CommandLineParser.Parse(rootCommand, args).Invoke();
    }
}
