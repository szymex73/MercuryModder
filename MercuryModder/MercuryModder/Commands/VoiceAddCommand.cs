using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using MercuryModder.Assets;
using MercuryModder.Helpers;
using SaturnData.Notation.Core;
using SaturnData.Notation.Serialization;
using SkiaSharp;
using SonicAudioLib.Archives;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace MercuryModder.Commands;

public class VoiceAddCommand : ICommand
{
    public Command Build()
    {
        var cmd = new Command("voiceadd", "Voice inject")
        {
            new Option<DirectoryInfo>("--audio") {
                Description = "Path to a folder with .wav files to be swapped in",
                Required = true
            },
            new Option<DirectoryInfo>("--gameDir") {
                Description = "Path to the clean game base directory (WindowsNoEditor)",
                Required = true
            },
            new Option<DirectoryInfo>("--output") {
                Description = "Path to the output modified data",
                Required = true
            },
        };
        
        cmd.SetAction(Command);
        
        return cmd;
    }

    public static void Command(ParseResult result)
    {
        DirectoryInfo audioDir = result.GetValue<DirectoryInfo>("--audio");
        DirectoryInfo gameDir = result.GetValue<DirectoryInfo>("--gameDir");
        DirectoryInfo outputDir = result.GetValue<DirectoryInfo>("--output");

        AcbAsset cueFile = new AcbAsset($"{gameDir}/Mercury/Content/Sound/Voice/MER_VOICE.uasset");
        CriAfs2Archive awb = new CriAfs2Archive();
        uint awbCounter = 0;
        var awbId = (uint)cueFile.AddAwb("MER_VOICE_MOD");
        Console.WriteLine(awbId);

        foreach (var file in audioDir.EnumerateFiles("*.wav"))
        {
            string cueName = Path.GetFileNameWithoutExtension(file.Name);
            byte[] hcaBytes = AudioHelper.GetHCAFromWAVFile(file.FullName);
            var hca = new HcaTrack(hcaBytes);
            awb.Add(new CriAfs2Entry
            {
                Id = awbCounter,
                FilePath = new FileInfo(Path.ChangeExtension(file.FullName, "hca"))
            });

            Console.WriteLine(hca.SampleRate);

            var trackId = cueFile.AddTrack(awbCounter, awbId, hca.NumSamples, -1, 1, -1, 2, hca.SampleRate);
            cueFile.AddCue(cueName, new int[] { trackId }, (int)hca.Milliseconds);
            awbCounter += 1;
        }

        Directory.CreateDirectory($"{outputDir}/Mercury/Content/Sound/Voice/");
        var awbFile = File.Open($"{outputDir}/Mercury/Content/Sound/Voice/MER_VOICE_MOD.awb", FileMode.OpenOrCreate);
        awb.Write(awbFile);
        awbFile.Close();

        var awbStream = File.Open($"{outputDir}/Mercury/Content/Sound/Voice/MER_VOICE_MOD.awb", FileMode.Open);
        var hash = MD5.HashData(awbStream);
        awbStream.Close();
        cueFile.SetAwbHash("MER_VOICE_MOD", hash);
        cueFile.SetAwbHeader((int)awbId, awb.Header);

        cueFile.Save($"{outputDir}/Mercury/Content/Sound/Voice/MER_VOICE.uasset");
    }
}
