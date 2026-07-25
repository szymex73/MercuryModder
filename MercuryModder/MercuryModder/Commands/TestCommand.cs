using System.CommandLine;
using MercuryModder.Helpers;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;

namespace MercuryModder.Commands;

public class TestCommand : ICommand
{
    public Command Build()
    {
        var cmd = new Command("test", "Test");
        
        var trackDir = new Option<DirectoryInfo>(name: "--tracks", description: "Path to a directory with the custom tracks") { IsRequired = true };
        var gameDir = new Option<DirectoryInfo>(name: "--gameDir", description: "Path to the game base directory (WindowsNoEditor)") { IsRequired = true };
        
        cmd.AddOption(trackDir);
        cmd.AddOption(gameDir);
        cmd.SetHandler(Command, trackDir, gameDir);
        
        return cmd;
    }
    public static void Command(DirectoryInfo trackDir, DirectoryInfo gameDir)
    {
        // Place for random experiments
    }
}
