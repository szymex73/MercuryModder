using System.CommandLine;

namespace MercuryModder.Commands;

public class PrepareCommand : ICommand
{
    public Command Build()
    {
        var cmd = new Command("prepare", "Prepare a track folder with genre folders")
        {
            new Option<DirectoryInfo>("--tracks") { 
                Description = "Path to a directory with the custom tracks",
                Required = true
            }
        };
        
        cmd.SetAction(Command);
        
        return cmd;
    }

    public static void Command(ParseResult result)
    {
        DirectoryInfo trackDir = result.GetValue<DirectoryInfo>("--tracks");

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
