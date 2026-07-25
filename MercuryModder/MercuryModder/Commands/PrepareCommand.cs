using System.CommandLine;

namespace MercuryModder.Commands;

public class PrepareCommand : ICommand
{
    public Command Build()
    {
        var cmd = new Command("prepare", "Prepare a track folder with genre folders");
        
        var trackDir = new Option<DirectoryInfo>(name: "--tracks", description: "Path to a directory with the custom tracks") { IsRequired = true };
        
        cmd.AddOption(trackDir);
        cmd.SetHandler(Command, trackDir);
        
        return cmd;
    }

    public static void Command(DirectoryInfo trackDir)
    {
        if (!trackDir.Exists) trackDir.Create();
        
        trackDir.CreateSubdirectory("Anipop");
        trackDir.CreateSubdirectory("Vocaloid");
        trackDir.CreateSubdirectory("Touhou");
        trackDir.CreateSubdirectory("2_5D");
        trackDir.CreateSubdirectory("Variety");
        trackDir.CreateSubdirectory("Original");
        trackDir.CreateSubdirectory("TanoC");
        // For the purpose of adding inferno charts to existing songs
        trackDir.CreateSubdirectory("Inferno"); 

        Console.WriteLine($"Genre directories created in {trackDir}");
    }
}
