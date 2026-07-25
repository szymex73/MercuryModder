using System.CommandLine;
using MercuryModder.Assets;

namespace MercuryModder.Commands;

public class AcbCommand : ICommand
{
    public Command Build()
    {
        var cmd = new Command("acb", "Acb asset import/export");
        
        var import = new Option<bool>(name: "--import", description: "Whether to import the acb back into the asset (by default acb is extracted out)") { IsRequired = false };
        var assetPath = new Option<FileInfo>(name: "--asset", description: "Path to the cue file .uasset") { IsRequired = true };
        var acbPath = new Option<FileInfo>(name: "--acb", description: "Path to the cue file .acb") { IsRequired = true };
        
        cmd.AddOption(import);
        cmd.AddOption(assetPath);
        cmd.AddOption(acbPath);
        cmd.SetHandler(Command, assetPath, acbPath, import);
        
        return cmd;
    }

    public static void Command(FileInfo assetPath, FileInfo acbPath, bool import)
    {
        AcbAsset cueFile = new AcbAsset(assetPath.ToString());

        if (import)
        {
            var acbContent = File.ReadAllBytes(acbPath.ToString());
            cueFile.Save(assetPath.ToString(), acbContent);
        } else
        {
            File.WriteAllBytes(acbPath.ToString(), cueFile.GetCueFile());
        }
    }
}
