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

public class SwapBgm
{
    public static void Command(FileInfo newbgmFile, DirectoryInfo gameDir)
    {
        if (!newbgmFile.Exists)
        {
            Console.WriteLine($"New bgm file {newbgmFile} does not exist");
            return;
        }

        AcbAsset cueFile = new AcbAsset($"{gameDir}/Mercury/Content/Sound/Bgm/MER_BGM.uasset");
        CriAfs2Archive awb = new CriAfs2Archive();
        var awbId = (uint)cueFile.AddAwb("MER_BGM_V74");

        byte[] hcaBytes = GetHCAFromWAVFile(newbgmFile.FullName);
        var hca = new HcaTrack(hcaBytes);
        awb.Add(new CriAfs2Entry
        {
            Id = 0,
            FilePath = new FileInfo($"{newbgmFile.Directory.FullName}/track.hca")
        });
        var spkId = cueFile.AddTrack(0, awbId, hca.NumSamples, false);
        var hdpId = cueFile.AddTrack(0, awbId, hca.NumSamples, true);

        // Find the Cue ID of the attract BGM
        int cueNameId = 0;
        for (cueNameId = 0; cueNameId < cueFile.CueNameTable.Rows.Count; cueNameId++)
        {
            if (cueFile.CueNameTable.Rows[cueNameId].GetValue<string>("CueName") == "MER_BGM_SYS_301") break;
        }

        var cueId = cueFile.CueNameTable.Rows[cueNameId].GetValue<ushort>("CueIndex");
        Console.WriteLine($"Attract BGM Cue ID: {cueId}");

        int[] trackIds = new int[] { spkId, hdpId };

        var sequence = cueFile.SequenceTable.NewRow();
        sequence["NumTracks"] = (short)trackIds.Length;
        var trackIndex = new byte[trackIds.Length * 2];
        for (int i = 0; i < trackIds.Length; i++) Buffer.BlockCopy(BitConverter.GetBytes((ushort)trackIds[i]).Reverse().ToArray(), 0, trackIndex, i * 2, 2);
        sequence["TrackIndex"] = trackIndex;
        sequence["CommandIndex"] = (short)1;
        cueFile.SequenceTable.Rows.Add(sequence);
        var sequenceId = cueFile.SequenceTable.Rows.Count - 1;

        var cue = cueFile.CueTable.Rows[cueId];
        Console.WriteLine($"Cue ID: {cue.GetValue<uint>("CueId")}");
        cue["ReferenceIndex"] = (short)sequenceId;

        var awbFile = File.Open($"{gameDir}/Mercury/Content/Sound/Bgm/MER_BGM_V74.awb", FileMode.OpenOrCreate);
        awb.Write(awbFile);
        awbFile.Close();

        var awbStream = File.Open($"{gameDir}/Mercury/Content/Sound/Bgm/MER_BGM_V74.awb", FileMode.Open);
        var hash = MD5.HashData(awbStream);
        awbStream.Close();
        cueFile.SetAwbHash("MER_BGM_V74", hash);
        cueFile.SetAwbHeader((int)awbId, awb.Header);

        cueFile.Save($"{gameDir}/Mercury/Content/Sound/Bgm/MER_BGM.uasset");
    }

    private static byte[] GetHCAFromWAVFile(string wavPath)
    {
        string hcaPath = Path.ChangeExtension(wavPath, "hca");
        if (!File.Exists(wavPath))
        {
            var wavBytes = File.ReadAllBytes(wavPath);
            byte[] hcaBytes = AudioHelper.ConvertToHCA(wavBytes, VGAudio.Cli.FileType.Wave, false);
            File.WriteAllBytes(Path.ChangeExtension(wavPath, "hca"), hcaBytes);
            File.SetLastWriteTime(hcaPath, File.GetLastWriteTime(wavPath)); // Set mtime

            return hcaBytes;
        }
        else
        {
            DateTime wavMtime = File.GetLastWriteTime(wavPath);
            DateTime hcaMtime = File.GetLastWriteTime(hcaPath);

            if (wavMtime != hcaMtime)
            {
                var wavBytes = File.ReadAllBytes(wavPath);
                byte[] hcaBytes = AudioHelper.ConvertToHCA(wavBytes, VGAudio.Cli.FileType.Wave, false);
                File.WriteAllBytes(Path.ChangeExtension(wavPath, "hca"), hcaBytes);
                File.SetLastWriteTime(hcaPath, File.GetLastWriteTime(wavPath)); // Set mtime

                return hcaBytes;
            }
            else
            {
                // Same mtime so we can use the cached file
                return File.ReadAllBytes(hcaPath);
            }
        }
    }
}
