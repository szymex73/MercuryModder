using System.ComponentModel;
using System.Formats.Asn1;
using SonicAudioLib.CriMw;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace MercuryModder.Assets;

public class AcbAsset
{
    public UAsset Asset;

    // Root UTF table in ACB
    public CriTable MainTable;

    // All necessary "child" tables from ACB to inject songs
    public CriTable WaveformTable;
    public CriTable SynthTable;
    public CriTable TrackEventTable;
    public CriTable TrackTable;
    public CriTable SequenceTable;
    public CriTable CueTable;
    public CriTable CueNameTable;
    public CriTable StreamAwbTable;
    public CriTable StreamAwbHeadersTable;

    private uint lastCueID;

    public AcbAsset(string path)
    {
        // Extract the ACB contents out of the asset
        Asset = new UAsset(path, true, EngineVersion.VER_UE4_19);
        var cueSheet = Asset.Exports[0] as NormalExport;
        var cueData = cueSheet.Extras.ToList().Skip(20).ToArray();

        // Parse the root table
        MainTable = new CriTable();
        MainTable.Load(cueData);

        // Parse relevant tables
        WaveformTable = new CriTable();
        WaveformTable.Load(MainTable.Rows[0]["WaveformTable"] as byte[]);
        SynthTable = new CriTable();
        SynthTable.Load(MainTable.Rows[0]["SynthTable"] as byte[]);
        TrackEventTable = new CriTable();
        TrackEventTable.Load(MainTable.Rows[0]["TrackEventTable"] as byte[]);
        TrackTable = new CriTable();
        TrackTable.Load(MainTable.Rows[0]["TrackTable"] as byte[]);
        SequenceTable = new CriTable();
        SequenceTable.Load(MainTable.Rows[0]["SequenceTable"] as byte[]);
        CueTable = new CriTable();
        CueTable.Load(MainTable.Rows[0]["CueTable"] as byte[]);
        CueNameTable = new CriTable();
        CueNameTable.Load(MainTable.Rows[0]["CueNameTable"] as byte[]);
        StreamAwbTable = new CriTable();
        StreamAwbTable.Load(MainTable.Rows[0]["StreamAwbHash"] as byte[]);
        StreamAwbHeadersTable = new CriTable();
        StreamAwbHeadersTable.Load(MainTable.Rows[0]["StreamAwbAfs2Header"] as byte[]);

        CueTable.Fields["UserData"].DefaultValue = "";
        SynthTable.Fields["VoiceLimitGroupName"].DefaultValue = "";
        TrackTable.Fields["TargetName"].DefaultValue = "";
        TrackTable.Fields["TargetAcbName"].DefaultValue = "";

        for (int i = 0; i < CueTable.Rows.Count; i++)
        {
            var cueId = CueTable.Rows[i].GetValue<uint>("CueId");
            if (cueId > lastCueID) lastCueID = cueId;
        }
    }

    public int AddAwb(string name)
    {
        CriRow row = StreamAwbTable.NewRow();

        // Using a placeholder hash here since it won't be known until after all
        // tracks are added to the audio bank file
        row["Name"] = name;
        row["Hash"] = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        StreamAwbTable.Rows.Add(row);
        return StreamAwbTable.Rows.Count - 1;
    }

    public void SetAwbHash(string name, byte[] hash)
    {
        var row = StreamAwbTable.Rows.FirstOrDefault(row => (row["Name"] as string) == name);
        row["Hash"] = hash;
    }

    public void SetAwbHeader(int index, byte[] header)
    {
        if (index >= StreamAwbHeadersTable.Rows.Count)
        {
            while (StreamAwbHeadersTable.Rows.Count <= index)
            {
                StreamAwbHeadersTable.Rows.Add(StreamAwbHeadersTable.NewRow());
            }
        }

        var row = StreamAwbHeadersTable.Rows[index];
        row["Header"] = header;
    }

    public int AddTrack(uint streamAwbId, uint streamAwbPortNo, int sampleCount, bool headphoneTrack)
    {
        var waveform = WaveformTable.NewRow();
        waveform["NumChannels"] = (byte)2;
        waveform["LoopFlag"] = (byte)1;
        waveform["NumSamples"] = (uint)sampleCount;
        waveform["ExtensionData"] = (ushort)65535;
        waveform["StreamAwbPortNo"] = (ushort)streamAwbPortNo;
        waveform["StreamAwbId"] = (ushort)streamAwbId;
        WaveformTable.Rows.Add(waveform);
        var waveformId = WaveformTable.Rows.Count - 1;

        var synthId = SynthTable.Rows.Count;
        var synth = SynthTable.NewRow();
        var referenceItems = new byte[4];
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)1).Reverse().ToArray(), 0, referenceItems, 0, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)waveformId).Reverse().ToArray(), 0, referenceItems, 2, 2);
        synth["ReferenceItems"] = referenceItems;
        synth["ControlWorkArea1"] = (ushort)synthId;
        synth["ControlWorkArea2"] = (ushort)synthId;
        // synth["VoiceLimitGroupName"] = "";
        SynthTable.Rows.Add(synth);

        var trackEvent = TrackEventTable.NewRow();
        // TODO: Figure out what the commands do/are
        var command = new byte[10];
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)2000).Reverse().ToArray(), 0, command, 0, 2);
        command[2] = 4;
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)2).Reverse().ToArray(), 0, command, 3, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)synthId).Reverse().ToArray(), 0, command, 5, 2);
        command[7] = 0;
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)0).Reverse().ToArray(), 0, command, 8, 2);
        trackEvent["Command"] = command;
        TrackEventTable.Rows.Add(trackEvent);
        var trackEventId = TrackEventTable.Rows.Count - 1;

        var track = TrackTable.NewRow();
        track["CommandIndex"] = (short)(headphoneTrack ? 1 : 0);
        track["EventIndex"] = (short)trackEventId;
        // track["TargetName"] = "";
        // track["TargetAcbName"] = "";
        TrackTable.Rows.Add(track);
        return TrackTable.Rows.Count - 1;
    }

    public uint AddCue(string name, int[] trackIds, int length)
    {
        var sequence = SequenceTable.NewRow();
        sequence["NumTracks"] = (short)trackIds.Length;
        var trackIndex = new byte[trackIds.Length * 2];
        for (int i = 0; i < trackIds.Length; i++) Buffer.BlockCopy(BitConverter.GetBytes((ushort)trackIds[i]).Reverse().ToArray(), 0, trackIndex, i * 2, 2);
        sequence["TrackIndex"] = trackIndex;
        sequence["CommandIndex"] = (short)1;
        SequenceTable.Rows.Add(sequence);
        var sequenceId = SequenceTable.Rows.Count - 1;

        uint cueId = lastCueID + 1;
        lastCueID += 1;
        var cue = CueTable.NewRow();
        cue["CueId"] = cueId;
        cue["ReferenceIndex"] = (short)sequenceId;
        cue["Length"] = (int)length;
        CueTable.Rows.Add(cue);

        var cueName = CueNameTable.NewRow();
        cueName["CueName"] = name;
        cueName["CueIndex"] = (short)(CueTable.Rows.Count - 1);
        CueNameTable.Rows.Add(cueName);

        return cueId;
    }
    
    public byte[] GetCueFile()
    {
        // Sort CueNames before serializing
        CueNameTable.Rows.Sort(new CueNameComparer());

        // Serialize the child tables back into the root table
        MainTable.Rows[0]["WaveformTable"] = WaveformTable.Save();
        MainTable.Rows[0]["SynthTable"] = SynthTable.Save();
        MainTable.Rows[0]["TrackEventTable"] = TrackEventTable.Save();
        MainTable.Rows[0]["TrackTable"] = TrackTable.Save();
        MainTable.Rows[0]["SequenceTable"] = SequenceTable.Save();
        MainTable.Rows[0]["CueTable"] = CueTable.Save();
        MainTable.Rows[0]["CueNameTable"] = CueNameTable.Save();
        MainTable.Rows[0]["StreamAwbHash"] = StreamAwbTable.Save();
        MainTable.Rows[0]["StreamAwbAfs2Header"] = StreamAwbHeadersTable.Save();

        // Extend AwbTocWork, seems to be used as some internal scratch buffer in memory after loading? (lol)
        var tocWork = MainTable.Rows[0]["StreamAwbTocWork"] as byte[];
        Array.Resize(ref tocWork, tocWork.Length + 172 * 24); // TODO: seems to be 172 * song count from checks, but need to do comparisons between game vers
        MainTable.Rows[0]["StreamAwbTocWork"] = tocWork;

        // Serialize root table
        MainTable.WriterSettings = CriTableWriterSettings.Adx2Settings;
        return MainTable.Save();
    }

    public void Save(string path)
    {
        var acbContent = GetCueFile();

        // Add prefix necessary for the asset
        List<byte> acbExtra = new List<byte>();
        acbExtra.AddRange(BitConverter.GetBytes((uint)0));                 // Flags
        acbExtra.AddRange(BitConverter.GetBytes((uint)acbContent.Length)); // Element count
        acbExtra.AddRange(BitConverter.GetBytes((uint)acbContent.Length)); // Size on disk?
        acbExtra.AddRange(BitConverter.GetBytes((uint)0x40C));             // Offset of this in combined asset (.uasset + .uexp)
        acbExtra.AddRange(BitConverter.GetBytes((uint)0));                 // ?
        acbExtra.AddRange(acbContent);

        // Re-pack the asset
        var cueSheet = Asset.Exports[0] as NormalExport;
        cueSheet.Extras = acbExtra.ToArray();
        (cueSheet["NumSlots"] as IntPropertyData).Value = StreamAwbTable.Rows.Count;
        Asset.Write(path);
    }
}

class CueNameComparer : Comparer<CriRow>
{
    public override int Compare(CriRow x, CriRow y)
    {
        var xName = x.GetValue<string>("CueName");
        var yName = y.GetValue<string>("CueName");

        return xName.CompareTo(yName);
    }
}
