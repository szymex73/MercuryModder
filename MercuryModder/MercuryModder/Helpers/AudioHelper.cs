using VGAudio;
using VGAudio.Codecs.CriHca;
using VGAudio.Containers;
using VGAudio.Containers.Hca;
using VGAudio.Containers.Wave;
using VGAudio.Formats;
using VGAudio.Formats.CriHca;

namespace MercuryModder.Helpers;

public class AudioHelper
{
    public static byte[] ConvertWAVToHCA(byte[] bytes, bool loop) {
        using (var mis = new MemoryStream(bytes))
        {
            IAudioReader reader = new WaveReader();
            AudioWithConfig audio = reader.ReadWithConfig(mis);
            IAudioFormat format = audio.Audio.GetAllFormats().First();
            
            if (!loop) audio.Audio.SetLoop(false);
            else
            {
                var pcmf = audio.Audio.GetAllFormats().First().WithLoop(true, 0, format.SampleCount);
                audio = new AudioWithConfig(pcmf, audio.Configuration);
            }
            
            IAudioWriter writer = new HcaWriter();
            using (var mos = new MemoryStream())
            {
                writer.WriteToStream(audio.Audio, mos, audio.Configuration);
                return mos.GetBuffer();
            }
        }
    }

    public static byte[] ConvertHCAToWAV(byte[] bytes) {
        using (var mis = new MemoryStream(bytes))
        {
            IAudioReader reader = new HcaReader();
            AudioWithConfig audio = reader.ReadWithConfig(mis);

            IAudioWriter writer = new WaveWriter();

            using (var mos = new MemoryStream())
            {
                writer.WriteToStream(audio.Audio, mos, audio.Configuration);
                return mos.GetBuffer();
            }
        }
    }

    public static byte[] GetHCAFromWAVFile(string wavPath, bool looping = false)
    {
        string hcaPath = Path.ChangeExtension(wavPath, "hca");
        if (!File.Exists(wavPath))
        {
            var wavBytes = File.ReadAllBytes(wavPath);
            byte[] hcaBytes = AudioHelper.ConvertWAVToHCA(wavBytes, looping);
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
                byte[] hcaBytes = AudioHelper.ConvertWAVToHCA(wavBytes, looping);
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
