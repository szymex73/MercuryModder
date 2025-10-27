using System.Text;

namespace MercuryModder.Helpers;

// From https://github.com/yellowberryHN/WSongInject
internal class FStringHelper
{
    static readonly UTF8Encoding utf8 = new UTF8Encoding(false);

    public static string Read(BinaryReader reader)
    {
        int size = reader.ReadInt32();
        if (size == -1)
        {
            throw new Exception("FString size was -1");
        }
        var bytes = reader.ReadBytes(size);
        return utf8.GetString(bytes, 0, bytes.Length - 1);
    }

    public static void Write(BinaryWriter writer, string text)
    {
        var bytes = utf8.GetBytes(text);
        writer.Write(bytes.Length + 1);
        writer.Write(bytes, 0, bytes.Length);
        writer.Write('\0');
    }
}