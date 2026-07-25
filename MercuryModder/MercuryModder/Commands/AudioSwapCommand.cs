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

public class AudioSwapCommand : ICommand
{
    public Command Build()
    {
        var cmd = new Command("audioswap", "Audio swap (WIP)");
        
        var audioDir = new Option<DirectoryInfo>(name: "--audio", description: "Path to a folder with .wav files to be swapped in") { IsRequired = true };
        var gameDir = new Option<DirectoryInfo>(name: "--gameDir", description: "Path to the modified game base directory (WindowsNoEditor)") { IsRequired = true };
        
        cmd.AddOption(audioDir);
        cmd.AddOption(gameDir);
        cmd.SetHandler(Command, audioDir, gameDir);
        
        return cmd;
    }

    static string[] looped = new[]{"MER_BGM_SYS_302", "MER_BGM_SYS_303", "MER_BGM_SYS_306", "MER_BGM_SYS_307"};
    static string[] loop2cues = new []{"MER_BGM_SYS_302", "MER_BGM_SYS_303"};
    static Dictionary<string, int> loopIds = new Dictionary<string, int>{
        {"MER_BGM_SYS_302", 18},
        {"MER_BGM_SYS_303", 20}
    };

    public static void Command(DirectoryInfo audioDir, DirectoryInfo gameDir)
    {
        AcbAsset cueFile = new AcbAsset($"{gameDir}/Mercury/Content/Sound/Bgm/MER_BGM.uasset");
        CriAfs2Archive awb = new CriAfs2Archive();
        var awbId = (uint)cueFile.AddAwb("MER_BGM_V74");

        uint awbEntryId = 0;
        foreach (var newAudio in Directory.EnumerateFiles(audioDir.ToString(), "*.wav"))
        {
            var cueName = Path.GetFileNameWithoutExtension(newAudio);
            Console.WriteLine($"Cue name: {cueName}");

            byte[] hcaBytes = AudioHelper.GetHCAFromWAVFile(newAudio, looped.Contains(cueName));
            var hca = new HcaTrack(hcaBytes);
            awb.Add(new CriAfs2Entry
            {
                Id = awbEntryId,
                FilePath = new FileInfo(Path.ChangeExtension(newAudio, "hca"))
            });

            // Adding new
            int loopFlag = 1;
            if (loop2cues.Contains(cueName)) loopFlag = 2;
            int extId = loopIds.GetValueOrDefault(cueName, 65535);
            Console.WriteLine($"Extension Index: {extId}");
            var spkId = cueFile.AddTrack(awbEntryId, awbId, hca.NumSamples, false, loopFlag, extId);
            var hdpId = cueFile.AddTrack(awbEntryId, awbId, hca.NumSamples, true, loopFlag, extId);
            awbEntryId += 1;

            var cueNameId = cueFile.CueNameTable.Rows
                .Select((cueNameRow, index) => new { cueNameRow, index })
                .SkipWhile(pair => pair.cueNameRow.GetValue<string>("CueName") != cueName)
                .Select(pair => pair.index)
                .FirstOrDefault(-1);
            if (cueNameId == -1) throw new Exception($"Could not find cue {cueName}");

            Console.WriteLine($"Cue Name ID: {cueNameId}");
            var cueIndex = cueFile.CueNameTable.Rows[cueNameId].GetValue<ushort>("CueIndex");
            Console.WriteLine($"Cue index: {cueIndex}");

            int[] trackIds = new int[] { spkId, hdpId };
            Console.WriteLine($"{spkId} {hdpId}");

            var cue = cueFile.CueTable.Rows[cueIndex];
            Console.WriteLine($"Cue ID: {cue.GetValue<uint>("CueId")}");
            var sequenceId = cue.GetValue<ushort>("ReferenceIndex");
            var sequence = cueFile.SequenceTable.Rows[sequenceId];

            sequence["NumTracks"] = (short)trackIds.Length;
            var trackIndex = new byte[trackIds.Length * 2];
            for (int i = 0; i < trackIds.Length; i++) Buffer.BlockCopy(BitConverter.GetBytes((ushort)trackIds[i]).Reverse().ToArray(), 0, trackIndex, i * 2, 2);
            sequence["TrackIndex"] = trackIndex;

            if (extId != 65535)
            {
                var wavExt = cueFile.WaveformExtensionDataTable.Rows[extId];
                wavExt["LoopStart"] = (uint) 0;
                wavExt["LoopEnd"] = (uint) hca.NumSamples;
            }
        }

        var awbFile = File.Open($"{gameDir}/Mercury/Content/Sound/Bgm/MER_BGM_V74.awb", FileMode.OpenOrCreate);
        awb.Write(awbFile);
        awbFile.Close();

        var awbStream = File.Open($"{gameDir}/Mercury/Content/Sound/Bgm/MER_BGM_V74.awb", FileMode.Open);
        var hash = MD5.HashData(awbStream);
        awbStream.Close();
        cueFile.SetAwbHash("MER_BGM_V74", hash);
        cueFile.SetAwbHeader((int)awbId, awb.Header);

        File.WriteAllBytes(($"{gameDir}/Mercury/Content/Sound/Bgm/MER_BGM.acb"), cueFile.GetCueFile());
        cueFile.Save($"{gameDir}/Mercury/Content/Sound/Bgm/MER_BGM.uasset");

        // Cleanup cache files (don't ask why)
        foreach (var newAudio in Directory.GetFiles(audioDir.ToString()))
        {
            if (Path.GetExtension(newAudio) == ".hca") File.Delete(newAudio);
        }
    }
}
