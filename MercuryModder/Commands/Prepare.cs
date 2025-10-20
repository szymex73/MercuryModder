namespace MercuryModder.Commands;

public class Prepare
{
    public static void Command(DirectoryInfo trackDir)
    {
        if (!trackDir.Exists) trackDir.Create();
        
        trackDir.CreateSubdirectory("Anipop");
        trackDir.CreateSubdirectory("Vocaloid");
        trackDir.CreateSubdirectory("Touhou");
        trackDir.CreateSubdirectory("2.5D");
        trackDir.CreateSubdirectory("Variety");
        trackDir.CreateSubdirectory("Original");
        trackDir.CreateSubdirectory("TanoC");
        // For the purpose of adding inferno charts to existing songs
        trackDir.CreateSubdirectory("Inferno"); 

        Console.WriteLine($"Genre directories created in {trackDir}");
    }
}
