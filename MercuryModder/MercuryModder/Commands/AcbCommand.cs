using System.CommandLine;
using MercuryModder.Assets;

namespace MercuryModder.Commands;

public class AcbCommand : ICommand
{
    public Command Build()
    {
        var cmd = new Command("acb", "Acb asset import/export") {
            new Option<bool>("--import") {
                Description = "Whether to import the acb back into the asset (by default acb is extracted out)",
                Required = false
            },
            new Option<FileInfo>("--asset") {
                Description = "Path to the cue file .uasset",
                Required = true
            },
            new Option<FileInfo>("--acb") {
                Description = "Path to the cue file .acb",
                Required = true
            },
        };
        
        cmd.SetAction(Command);
        
        return cmd;
    }

    public static void Command(ParseResult result)
    {
        bool import = result.GetValue<bool>("--import");
        FileInfo assetPath = result.GetValue<FileInfo>("--asset");
        FileInfo acbPath = result.GetValue<FileInfo>("--acb");

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
