using VGAudio;
using VGAudio.Cli;

namespace MercuryModder.Helpers;

public class AudioHelper
{
    public static byte[] ConvertToHCA(byte[] bytes, FileType encodeType, bool loop) {
        ConvertStatics.SetLoop(loop, 0, 0);

        using (var ms = new MemoryStream(bytes))
        {
            var options = new Options();
            options.KeyCode = 0;
            options.Loop = loop;

            if(options.Loop)
                options.LoopEnd = int.MaxValue;

            byte[] track = ConvertStream.ConvertFile(options, ms, encodeType, FileType.Hca);

            //if (convertToType == FileType.Hca && loop)
            //    track = HCA.EncodeLoop(track, loop);

            return track;
        }
    }
}
