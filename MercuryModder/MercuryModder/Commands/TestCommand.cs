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
        var cmd = new Command("test", "Test")
        {
            new Option<DirectoryInfo>(name: "--tracks") {
                Description = "Path to a directory with the custom tracks",
                Required = true
            },
            new Option<DirectoryInfo>(name: "--gameDir") {
                Description = "Path to the game base directory (WindowsNoEditor)",
                Required = true
            },
        };
        
        cmd.SetAction(Command);
        
        return cmd;
    }
    public static void Command(ParseResult result)
    {
        DirectoryInfo trackDir = result.GetValue<DirectoryInfo>("--tracks");
        DirectoryInfo gameDir = result.GetValue<DirectoryInfo>("--gameDir");

        // Place for random experiments
    }
}
