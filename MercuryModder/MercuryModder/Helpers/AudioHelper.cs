using VGAudio;
using VGAudio.Cli;
using VGAudio.Containers.Wave;

namespace MercuryModder.Helpers;

public class AudioHelper
{
    public static byte[] ConvertToHCA(byte[] bytes, FileType encodeType, bool loop) {
        ConvertStatics.SetLoop(loop, 0, 0);

        using (var ms = new MemoryStream(bytes))
        {
            var wavReader = new WaveReader();
            var wavInfo = wavReader.ReadMetadata(ms);

            var options = new Options();
            options.KeyCode = 0;
            options.Loop = loop;

            if(options.Loop) options.LoopEnd = int.MaxValue;
            if(options.Loop) {
                float length = (float) wavInfo.SampleCount / (float) wavInfo.SampleRate;
                ConvertStatics.SetLoop(true, 0, (int) (length * 1000));
            }

            ms.Position = 0;
            
            byte[] track = ConvertStream.ConvertFile(options, ms, encodeType, FileType.Hca);

            return track;
        }
    }

    public static byte[] ConvertToWAV(byte[] bytes, FileType encodeType, bool loop) {
        using (var ms = new MemoryStream(bytes))
        {
            var options = new Options();
            options.KeyCode = 0;
            options.Loop = loop;

            if(options.Loop) options.LoopEnd = int.MaxValue;

            byte[] track = ConvertStream.ConvertFile(options, ms, encodeType, FileType.Wave);

            return track;
        }
    }

    public static byte[] GetHCAFromWAVFile(string wavPath, bool looping = false)
    {
        string hcaPath = Path.ChangeExtension(wavPath, "hca");
        if (!File.Exists(wavPath))
        {
            var wavBytes = File.ReadAllBytes(wavPath);
            byte[] hcaBytes = AudioHelper.ConvertToHCA(wavBytes, VGAudio.Cli.FileType.Wave, looping);
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
                byte[] hcaBytes = AudioHelper.ConvertToHCA(wavBytes, VGAudio.Cli.FileType.Wave, looping);
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
