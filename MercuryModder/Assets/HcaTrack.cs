using System.ComponentModel;
using SonicAudioLib.CriMw;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace MercuryModder.Assets;

// TODO: Based on https://github.com/LazyBone152/XV2-Tools/blob/master/Xv2CoreLib/HCA/HcaMetadata.cs

public class HcaTrack
{
    private const ushort ADX_SIGNATURE = 0x80;

    public bool IsValidAudioFile { get; private set; }


    //Fmt
    public byte Channels { get; private set; }
    public ushort SampleRate { get; private set; }
    public int FrameCount { get; private set; }
    public int NumSamples { get; private set; }

    public uint Milliseconds { get { return (uint)(DurationSeconds * 1000.0); } }
    public double DurationSeconds { get { return BlocksToSeconds((uint)FrameCount, SampleRate); } } //Seconds
    public TimeSpan Duration { get { return new TimeSpan(0, 0, 0, (int)DurationSeconds); } }

    //Loop
    public bool HasLoopData { get; private set; }
    public uint LoopStart { get; private set; } //HCA = frames
    public uint LoopEnd { get; private set; } //HCA = frames

    public double LoopStartSeconds { get { return BlocksToSeconds(LoopStart, SampleRate); } }
    public double LoopEndSeconds { get { return BlocksToSeconds(LoopEnd, SampleRate); } }

    public uint LoopStartMs => (uint)((LoopStart * 1024.0) / SampleRate * 1000f);
    public uint LoopEndMs => (uint)((LoopEnd * 1024.0) / SampleRate * 1000f);

    public uint LoopStartSamples { get { return BlocksToSamples(LoopStart); } }
    public uint LoopEndSamples { get { return BlocksToSamples(LoopEnd); } }

    public HcaTrack(byte[] bytes)
    {
        VGAudio.Containers.Hca.HcaReader reader = new VGAudio.Containers.Hca.HcaReader();
        var hca = reader.ParseFile(bytes);

        HasLoopData = hca.Hca.Looping;
        LoopStart = (uint)hca.Hca.LoopStartFrame;
        LoopEnd = (uint)hca.Hca.LoopEndFrame;

        FrameCount = hca.Hca.FrameCount;
        Channels = (byte)hca.Hca.ChannelCount;
        SampleRate = (ushort)hca.Hca.SampleRate;
        NumSamples = hca.Hca.SampleCount;

        IsValidAudioFile = true;
    }

    private static uint BlocksToMs(uint blocks, ushort sampleRate)
    {
        return (uint)((blocks * 1024.0) / sampleRate * 1000f);
    }

    private static double BlocksToSeconds(uint blocks, ushort sampleRate)
    {
        return (blocks * 1024.0) / sampleRate;
    }

    private static uint BlocksToSamples(uint blocks)
    {
        return (uint)(blocks * 1024.0);
    }
}
