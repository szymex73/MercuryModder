using System.Security.Cryptography;
using System.Text;
using MercuryModder.Assets;
using MercuryModder.Helpers;
using SaturnData.Notation.Serialization;
using SkiaSharp;
using SonicAudioLib.Archives;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace MercuryModder.Commands;

public class Modify
{
    // Used both as dir names and for genre indexing
    static string[] GENRES = new string[] { "Anipop", "Vocaloid", "Touhou", "2.5D", "Variety", "Original", "TanoC" };

    public static void Command(DirectoryInfo trackDir, DirectoryInfo gameDir, DirectoryInfo outputDir, bool insertFirst, bool printModified, int startId)
    {
        var songs = new List<Song>();
        foreach (var genre in GENRES)
        {
            var genreDir = Path.Combine(trackDir.ToString(), genre);
            foreach (var songDir in Directory.GetDirectories(genreDir))
            {
                var song = Song.Load(songDir);
                if (!Check.CheckSong(song, out var problems, out var warnings))
                {
                    // Only fail if problems were found, skip warnings
                    if (problems.Length != 0) throw new Exception($"Found problems when loading {songDir}. Please run \"MercuryMapper check\" first.");
                }
                songs.Add(song);
            }
        }

        var files = new List<String>();

        var musicTablePath = $"{gameDir}/Mercury/Content/Table/MusicParameterTable.uasset";
        if (!File.Exists(musicTablePath)) throw new Exception($"{musicTablePath} could not be accessed!");
        var musicTableAsset = new UAsset(musicTablePath, true, EngineVersion.VER_UE4_19);
        var musicParameterTable = musicTableAsset["MusicParameterTable"] as DataTableExport;
        files.Add("Mercury/Content/Table/MusicParameterTable.uasset");
        files.Add("Mercury/Content/Table/MusicParameterTable.uexp");

        var infernos = new List<Song>();
        foreach (var songDir in Directory.GetDirectories(Path.Combine(trackDir.ToString(), "Inferno")))
        {
            var song = Song.Load(songDir);
            int infId = Convert.ToInt32(song.Directory.Name);
            if (!Check.CheckInferno(song, out var problems, out var warnings))
            {
                // Only fail if problems were found, skip warnings
                if (problems.Length != 0) throw new Exception($"Found problems when loading {songDir} Inferno. Please run \"MercuryMapper check\" first.");
            }
            var mpt = musicParameterTable.Table.Data.Find(mpt => mpt.Name.ToString() == $"{infId}");
            if (mpt == null) throw new Exception($"Inferno for {infId} does not have a track in the original MPT!");
            infernos.Add(song);
        }

        var unlockTablePath = $"{gameDir}/Mercury/Content/Table/UnlockMusicTable.uasset";
        if (!File.Exists(unlockTablePath)) throw new Exception($"{unlockTablePath} could not be accessed!");
        var unlockTableAsset = new UAsset(unlockTablePath, true, EngineVersion.VER_UE4_19);
        var unlockTable = unlockTableAsset["UnlockMusicTable"] as DataTableExport;
        files.Add("Mercury/Content/Table/UnlockMusicTable.uasset");
        files.Add("Mercury/Content/Table/UnlockMusicTable.uexp");

        var infUnlockTablePath = $"{gameDir}/Mercury/Content/Table/UnlockInfernoTable.uasset";
        if (!File.Exists(infUnlockTablePath)) throw new Exception($"{infUnlockTablePath} could not be accessed!");
        var infUnlockTableAsset = new UAsset(infUnlockTablePath, true, EngineVersion.VER_UE4_19);
        var infUnlockTable = infUnlockTableAsset["UnlockInfernoTable"] as DataTableExport;
        files.Add("Mercury/Content/Table/UnlockInfernoTable.uasset");
        files.Add("Mercury/Content/Table/UnlockInfernoTable.uexp");

        var trackData = musicParameterTable.Table.Data;
        var unlockData = unlockTable.Table.Data;
        var infUnlockData = infUnlockTable.Table.Data;

        Directory.CreateDirectory($"{outputDir}/Mercury/Content/Sound/Bgm/");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AcbAsset cueFile = new AcbAsset($"{gameDir}/Mercury/Content/Sound/Bgm/MER_BGM.uasset");
        CriAfs2Archive awb = new CriAfs2Archive();
        uint awbCounter = 0;
        var awbId = (uint)cueFile.AddAwb("MER_BGM_V73");

        Directory.CreateDirectory($"{outputDir}/Mercury/Content/UI/Textures/JACKET/S07");

        var idStore = IDStore.ReadFromFile($"{trackDir}/song_ids.toml");
        var songIdCounter = startId;
        foreach (var song in songs)
        {
            var songId = -1;
            var songRelPath = song.GetRelativePath();

            if (idStore.ContainsKey(songRelPath))
            {
                songId = idStore[songRelPath];
            }
            else
            {
                while (idStore.ContainsValue(songIdCounter)) songIdCounter += 1;
                songId = songIdCounter;
                idStore[songRelPath] = songId;
                songIdCounter += 1;
            }

            if (songId == -1) throw new Exception($"Somehow songId was left at -1");
            
            Console.WriteLine($"{songId:0000} | Processing {song.Info.Title}");

            if (insertFirst) trackData.Insert(0, GetMPTEntry(musicTableAsset, song, songId));
            else trackData.Add(GetMPTEntry(musicTableAsset, song, songId));

            unlockData.Add(GetUMTEntry(unlockTableAsset, song, songId));
            if (song.Inferno != null) infUnlockData.Add(GetUITEntry(infUnlockTableAsset, song, songId));

            var jacketFilename = $"S{songId / 1000:00}/uT_J_S{songId:00_000}";
            var jacketName = $"uT_J_S{songId:00_000}";
            var jacket = new Texture2DAsset($"{gameDir}/Mercury/Content/UI/Textures/JACKET/S00/uT_J_S00_024.uasset");
            var image = SKImage.FromEncodedData($"{song.Directory}/jacket.png");
            var bitmap = SKBitmap.FromImage(image);
            jacket.SetBitmap(bitmap);
            jacket.SetName(jacketName);
            jacket.Save($"{outputDir}/Mercury/Content/UI/Textures/JACKET/{jacketFilename}.uasset");
            files.Add($"Mercury/Content/UI/Textures/JACKET/{jacketFilename}.uasset");

            // TODO: Use audio file info from saturndata?
            // TODO: Audio caching is kinda naive, change?
            byte[] hcaBytes = GetHCAFromWAVFile($"{song.Directory}/track.wav");
            var hca = new HcaTrack(hcaBytes);
            awb.Add(new CriAfs2Entry
            {
                Id = awbCounter,
                FilePath = new FileInfo($"{song.Directory}/track.hca")
            });

            var spkId = cueFile.AddTrack(awbCounter, awbId, hca.NumSamples, false);
            var hdpId = cueFile.AddTrack(awbCounter, awbId, hca.NumSamples, true);
            cueFile.AddCue($"MER_BGM_S{songId:00_000}", new int[] { spkId, hdpId }, (int)hca.Milliseconds);
            awbCounter += 1;

            var chartOutDirectory = $"{outputDir}/Mercury/Content/MusicData/S{songId:00-000}";
            Directory.CreateDirectory(chartOutDirectory);
            var nwa = new NotationWriteArgs()
            {
                FormatVersion = FormatVersion.Mer,
                WriteMusicFilePath = WriteMusicFilePathOption.NoExtension
            };
            song.Normal.Entry.AudioFile = $"MER_BGM_S{songId:00_000}";
            NotationSerializer.ToFile($"{chartOutDirectory}/S{songId:00-000}_00.mer", song.Normal.Entry, song.Normal.Chart, nwa);
            files.Add($"/Mercury/Content/MusicData/S{songId:00-000}/S{songId:00-000}_00.mer");
            song.Hard.Entry.AudioFile = $"MER_BGM_S{songId:00_000}";
            NotationSerializer.ToFile($"{chartOutDirectory}/S{songId:00-000}_01.mer", song.Hard.Entry, song.Hard.Chart, nwa);
            files.Add($"/Mercury/Content/MusicData/S{songId:00-000}/S{songId:00-000}_01.mer");
            song.Expert.Entry.AudioFile = $"MER_BGM_S{songId:00_000}";
            NotationSerializer.ToFile($"{chartOutDirectory}/S{songId:00-000}_02.mer", song.Expert.Entry, song.Expert.Chart, nwa);
            files.Add($"/Mercury/Content/MusicData/S{songId:00-000}/S{songId:00-000}_02.mer");
            // TODO: Check for a separate audio cut for inf?
            song.Inferno.Entry.AudioFile = $"MER_BGM_S{songId:00_000}";
            NotationSerializer.ToFile($"{chartOutDirectory}/S{songId:00-000}_03.mer", song.Inferno.Entry, song.Inferno.Chart, nwa);
            files.Add($"/Mercury/Content/MusicData/S{songId:00-000}/S{songId:00-000}_03.mer");
        }

        foreach (var song in infernos)
        {
            int infId = Convert.ToInt32(song.Directory.Name);
            var songEntry = trackData.Find(mpt => mpt.Name.ToString() == $"{infId}");
            Console.WriteLine($"{infId:0000} | Processing Inferno for {songEntry["MusicMessage"]}");

            string cueName;
            if (File.Exists(Path.Combine(song.Directory.ToString(), "track.wav"))) // Separate inf cut
            {
                byte[] hcaBytes = GetHCAFromWAVFile($"{song.Directory}/track.wav");

                var hca = new HcaTrack(hcaBytes);
                awb.Add(new CriAfs2Entry
                {
                    Id = awbCounter,
                    FilePath = new FileInfo($"{song.Directory}/track.hca")
                });

                var spkId = cueFile.AddTrack(awbCounter, awbId, hca.NumSamples, false);
                var hdpId = cueFile.AddTrack(awbCounter, awbId, hca.NumSamples, true);
                cueFile.AddCue($"MER_BGM_S{infId:00_000}_INF", new int[] { spkId, hdpId }, (int)hca.Milliseconds);
                awbCounter += 1;
                cueName = $"MER_BGM_S{infId:00_000}_INF";
            }
            else // Grab the cue name from the expert chart
            {
                var expert = new Song.ChartContainer($"{gameDir}/Mercury/Content/MusicData/S{infId:00-000}/S{infId:00-000}_02.mer", new NotationReadArgs());
                cueName = expert.Entry.AudioFile;
            }

            // Edit the MPT entry
            (songEntry["NotesDesignerInferno"] as StrPropertyData).Value = FString.FromString(song.Inferno.Entry.NotesDesigner);
            (songEntry["DifficultyInfernoLv"] as FloatPropertyData).Value = (float)song.Inferno.Entry.Level;
            (songEntry["ClearNormaRateInferno"] as FloatPropertyData).Value = song.Inferno.Entry.ClearThreshold;
            // Add UIT entry
            infUnlockData.Add(GetUITEntry(infUnlockTableAsset, song, infId));

            // Write the chart
            var nwa = new NotationWriteArgs()
            {
                FormatVersion = FormatVersion.Mer,
                WriteMusicFilePath = WriteMusicFilePathOption.NoExtension
            };
            song.Inferno.Entry.AudioFile = cueName;
            NotationSerializer.ToFile($"{outputDir}/Mercury/Content/MusicData/S{infId:00-000}/S{infId:00-000}_03.mer", song.Inferno.Entry, song.Inferno.Chart, nwa);
            files.Add($"/Mercury/Content/MusicData/S{infId:00-000}/S{infId:00-000}_03.mer");
        }

        var awbFile = File.Open($"{outputDir}/Mercury/Content/Sound/Bgm/MER_BGM_V73.awb", FileMode.OpenOrCreate);
        awb.Write(awbFile);
        awbFile.Close();
        files.Add("Mercury/Content/Sound/Bgm/MER_BGM_V73.awb");

        var hash = MD5.HashData(File.ReadAllBytes($"{outputDir}/Mercury/Content/Sound/Bgm/MER_BGM_V73.awb"));
        cueFile.SetAwbHash("MER_BGM_V73", hash);
        cueFile.SetAwbHeader((int)awbId, awb.Header);

        cueFile.Save($"{outputDir}/Mercury/Content/Sound/Bgm/MER_BGM.uasset");
        files.Add("Mercury/Content/Sound/Bgm/MER_BGM.uasset");
        files.Add("Mercury/Content/Sound/Bgm/MER_BGM.uexp");

        Directory.CreateDirectory($"{outputDir}/Mercury/Content/Table/");
        musicTableAsset.FixNameMapLookupIfNeeded();
        musicTableAsset.Write($"{outputDir}/Mercury/Content/Table/MusicParameterTable.uasset");
        unlockTableAsset.FixNameMapLookupIfNeeded();
        unlockTableAsset.Write($"{outputDir}/Mercury/Content/Table/UnlockMusicTable.uasset");
        infUnlockTableAsset.FixNameMapLookupIfNeeded();
        infUnlockTableAsset.Write($"{outputDir}/Mercury/Content/Table/UnlockInfernoTable.uasset");

        // Patch engine config file to increase audio bind count, each .awb needs a new bind
        // Just increasing by +10 flat for now, it slightly increases memory usage but w/e
        var DefaultEngine = File.ReadAllText($"{gameDir}/Mercury/Config/DefaultEngine.ini");
        DefaultEngine = DefaultEngine.Replace("MaxBinds=31", "MaxBinds=41");
        Directory.CreateDirectory($"{outputDir}/Mercury/Config/");
        File.WriteAllText($"{outputDir}/Mercury/Config/DefaultEngine.ini", DefaultEngine);
        files.Add("Mercury/Config/DefaultEngine.ini");

        if (printModified)
        {
            Console.WriteLine("--- Modified files ---");
            files.Sort();
            foreach (String fp in files)
            {
                Console.WriteLine(fp);
            }
        }
        IDStore.SaveToFile(idStore, $"{trackDir}/song_ids.toml");
    }

    private static StructPropertyData GetMPTEntry(UAsset asset, Song song, int songId)
    {
        return new StructPropertyData(FName.FromString(asset, $"{songId}"), FName.FromString(asset, "MusicParameterTableData"))
        {
            Value = new List<PropertyData>
            {
                new UInt32PropertyData(FName.FromString(asset, "UniqueID")) { Value = (uint) songId },
                new StrPropertyData(FName.FromString(asset, "MusicMessage")) { Value = FString.FromString(song.Info.Title) },
                new StrPropertyData(FName.FromString(asset, "ArtistMessage")) { Value = FString.FromString(song.Info.Artist) },
                new StrPropertyData(FName.FromString(asset, "CopyrightMessage")) { Value = FString.FromString("-") },
                new UInt32PropertyData(FName.FromString(asset, "VersionNo")) { Value = 0 },
                new StrPropertyData(FName.FromString(asset, "AssetDirectory")) { Value = FString.FromString($"S{songId:00-000}") },
                new StrPropertyData(FName.FromString(asset, "MovieAssetName")) { Value = FString.FromString("-") },
                new StrPropertyData(FName.FromString(asset, "MovieAssetNameHard")) { Value = null },
                new StrPropertyData(FName.FromString(asset, "MovieAssetNameExpert")) { Value = null },
                new StrPropertyData(FName.FromString(asset, "MovieAssetNameInferno")) { Value = null },
                new StrPropertyData(FName.FromString(asset, "JacketAssetName")) { Value = FString.FromString($"S{songId / 1000:00}/uT_J_S{songId:00_000}") },
                new StrPropertyData(FName.FromString(asset, "Rubi")) { Value = FString.FromString(song.Info.Rubi) },

                new BoolPropertyData(FName.FromString(asset, "bValidCulture_ja_JP")) { Value = true },
                new BoolPropertyData(FName.FromString(asset, "bValidCulture_en_US")) { Value = true },
                new BoolPropertyData(FName.FromString(asset, "bValidCulture_zh_Hant_TW")) { Value = true },
                new BoolPropertyData(FName.FromString(asset, "bValidCulture_en_HK")) { Value = true },
                new BoolPropertyData(FName.FromString(asset, "bValidCulture_en_SG")) { Value = true },
                new BoolPropertyData(FName.FromString(asset, "bValidCulture_ko_KR")) { Value = true },
                new BoolPropertyData(FName.FromString(asset, "bValidCulture_h_Hans_CN_Guest")) { Value = true },
                new BoolPropertyData(FName.FromString(asset, "bValidCulture_h_Hans_CN_GeneralMember")) { Value = true },
                new BoolPropertyData(FName.FromString(asset, "bValidCulture_h_Hans_CN_VipMember")) { Value = true },
                new BoolPropertyData(FName.FromString(asset, "bValidCulture_Offline")) { Value = true },
                new BoolPropertyData(FName.FromString(asset, "bValidCulture_NoneActive")) { Value = false },

                new BoolPropertyData(FName.FromString(asset, "bRecommend")) { Value = true },

                new IntPropertyData(FName.FromString(asset, "WaccaPointCost")) { Value = 0 },
                new BytePropertyData(FName.FromString(asset, "bCollaboration")) { ByteType = BytePropertyType.Byte, EnumType = FName.FromString(asset, "None"), Value = 0 },
                new BytePropertyData(FName.FromString(asset, "bWaccaOriginal")) { ByteType = BytePropertyType.Byte, EnumType = FName.FromString(asset, "None"), Value = 0 },
                new BytePropertyData(FName.FromString(asset, "TrainingLevel")) { ByteType = BytePropertyType.Byte, EnumType = FName.FromString(asset, "None"), Value = 0 },
                new BytePropertyData(FName.FromString(asset, "Reserved")) { ByteType = BytePropertyType.Byte, EnumType = FName.FromString(asset, "None"), Value = 0 },

                new StrPropertyData(FName.FromString(asset, "Bpm")) { Value = FString.FromString(song.Info.Bpm) },
                new StrPropertyData(FName.FromString(asset, "HashTag")) { Value = FString.FromString("Nim_WAC") },

                new StrPropertyData(FName.FromString(asset, "NotesDesignerNormal")) { Value = FString.FromString(song.Normal.Entry.NotesDesigner) },
                new StrPropertyData(FName.FromString(asset, "NotesDesignerHard")) { Value = FString.FromString(song.Hard.Entry.NotesDesigner) },
                new StrPropertyData(FName.FromString(asset, "NotesDesignerExpert")) { Value = FString.FromString(song.Expert.Entry.NotesDesigner) },
                new StrPropertyData(FName.FromString(asset, "NotesDesignerInferno")) { Value = song.Inferno != null ? FString.FromString(song.Inferno.Entry.NotesDesigner) : FString.FromString("-") },

                new FloatPropertyData(FName.FromString(asset, "DifficultyNormalLv")) { Value = (float) song.Normal.Entry.Level },
                new FloatPropertyData(FName.FromString(asset, "DifficultyHardLv")) { Value = (float) song.Hard.Entry.Level },
                new FloatPropertyData(FName.FromString(asset, "DifficultyExtremeLv")) { Value = (float) song.Expert.Entry.Level },
                new FloatPropertyData(FName.FromString(asset, "DifficultyInfernoLv")) { Value = song.Inferno != null ? (float) song.Inferno.Entry.Level : 0 },

                new FloatPropertyData(FName.FromString(asset, "ClearNormaRateNormal")) { Value = song.Normal.Entry.ClearThreshold },
                new FloatPropertyData(FName.FromString(asset, "ClearNormaRateHard")) { Value = song.Hard.Entry.ClearThreshold },
                new FloatPropertyData(FName.FromString(asset, "ClearNormaRateExtreme")) { Value = song.Expert.Entry.ClearThreshold },
                new FloatPropertyData(FName.FromString(asset, "ClearNormaRateInferno")) { Value = song.Inferno != null ? song.Inferno.Entry.ClearThreshold : 0 },

                new FloatPropertyData(FName.FromString(asset, "PreviewBeginTime")) { Value = song.Info.PreviewStart },
                new FloatPropertyData(FName.FromString(asset, "PreviewSeconds")) { Value = song.Info.PreviewLength },
                new IntPropertyData(FName.FromString(asset, "ScoreGenre")) {Value = Array.IndexOf(GENRES, song.Info.Genre)},
                new IntPropertyData(FName.FromString(asset, "MusicTagForUnlock0")) { Value = 0 },
                new IntPropertyData(FName.FromString(asset, "MusicTagForUnlock1")) { Value = 0 },
                new IntPropertyData(FName.FromString(asset, "MusicTagForUnlock2")) { Value = 0 },
                new IntPropertyData(FName.FromString(asset, "MusicTagForUnlock3")) { Value = 0 },
                new IntPropertyData(FName.FromString(asset, "MusicTagForUnlock4")) { Value = 0 },
                new IntPropertyData(FName.FromString(asset, "MusicTagForUnlock5")) { Value = 0 },
                new IntPropertyData(FName.FromString(asset, "MusicTagForUnlock6")) { Value = 0 },
                new IntPropertyData(FName.FromString(asset, "MusicTagForUnlock7")) { Value = 0 },
                new IntPropertyData(FName.FromString(asset, "MusicTagForUnlock8")) { Value = 0 },
                new IntPropertyData(FName.FromString(asset, "MusicTagForUnlock9")) { Value = 0 },
                new UInt64PropertyData(FName.FromString(asset, "WorkBuffer")) { Value = 0 },
                new StrPropertyData(FName.FromString(asset, "AssetFullPath")) { Value = FString.FromString($"D:/project/Mercury/Mercury/Content//MusicData/S{songId:00-000}") }
            }
        };
    }

    private static StructPropertyData GetUMTEntry(UAsset asset, Song song, int songId)
    {
        // TODO: Allow some way of specifying custom unlock data
        // TODO: The name (Data_{songId}) doesn't seem to be respected, investigate
        return new StructPropertyData(FName.FromString(asset, $"Data_{songId}"), FName.FromString(asset, "UnlockMusicTableData"))
        {
            Value = new List<PropertyData>
            {
                new IntPropertyData(FName.FromString(asset, "MusicId")) { Value = (int) songId },
                new Int64PropertyData(FName.FromString(asset, "AdaptStartTime")) { Value = 0 },
                new Int64PropertyData(FName.FromString(asset, "AdaptEndTime")) { Value = 0 },
                new BoolPropertyData(FName.FromString(asset, "bRequirePurchase")) { Value = false },
                new IntPropertyData(FName.FromString(asset, "RequiredMusicOpenWaccaPoint")) { Value = 0 },
                new BoolPropertyData(FName.FromString(asset, "bVipPreOpen")) { Value = false },
                new StrPropertyData(FName.FromString(asset, "NameTag")) { Value = FString.FromString(song.Info.Title) },
                new StrPropertyData(FName.FromString(asset, "ExplanationTextTag")) { Value = FString.FromString(null) },
                new Int64PropertyData(FName.FromString(asset, "ItemActivateStartTime")) { Value = 0 },
                new Int64PropertyData(FName.FromString(asset, "ItemActivateEndTime")) { Value = 0 },
                new BoolPropertyData(FName.FromString(asset, "bIsInitItem")) { Value = true },
                new IntPropertyData(FName.FromString(asset, "GainWaccaPoint")) { Value = 0 },
            }
        };
    }

    private static StructPropertyData GetUITEntry(UAsset asset, Song song, int songId)
    {
        return new StructPropertyData(FName.FromString(asset, $"{songId}"), FName.FromString(asset, "UnlockInfernoTableData"))
        {
            Value = new List<PropertyData>
            {
                new IntPropertyData(FName.FromString(asset, "MusicId")) { Value = (int) songId },
                new BoolPropertyData(FName.FromString(asset, "bRequirePurchase")) { Value = false },
                new IntPropertyData(FName.FromString(asset, "RequiredInfernoOpenWaccaPoint")) { Value = 0 },
                new BoolPropertyData(FName.FromString(asset, "bVipPreOpen")) { Value = true },
                new StrPropertyData(FName.FromString(asset, "NameTag")) { Value = FString.FromString(song.Info.Title) },
                new StrPropertyData(FName.FromString(asset, "ExplanationTextTag")) { Value = FString.FromString(null) },
                new Int64PropertyData(FName.FromString(asset, "ItemActivateStartTime")) { Value = 0 },
                new Int64PropertyData(FName.FromString(asset, "ItemActivateEndTime")) { Value = 0 },
                new BoolPropertyData(FName.FromString(asset, "bIsInitItem")) { Value = true },
                new IntPropertyData(FName.FromString(asset, "GainWaccaPoint")) { Value = 0 },
            }
        };
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
            } else
            {
                // Same mtime so we can use the cached file
                return File.ReadAllBytes(hcaPath);
            }
        }
    }
}
