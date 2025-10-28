namespace MercuryModder.Helpers;

public class DiVEwallHelper
{
    public static byte[] FormatTsv(List<DiVEwallSongEntry> entries)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms);

        writer.WriteLine("id\tname\tartist\tversion\tbpm\tdiffNormal\tdiffHard\tdiffExpert\tdiffInferno\tverAdded\tverRemoved\tinfAdded");

        foreach (var entry in entries)
        {
            // Workaround to have dots as decimal points for floats
            writer.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{entry.id}\t{entry.name}\t{entry.artist}\t{entry.version}\t{entry.bpm}\t{entry.diffNormal:0.0}\t{entry.diffHard:0.0}\t{entry.diffExpert:0.0}\t{entry.diffInferno:0.0}\t{entry.verAdded}\t{entry.verRemoved}\t{entry.infAdded}"));
        }

        writer.Flush();

        return ms.ToArray();
    }

    public struct DiVEwallSongEntry
    {
        public int id;
        public string name;
        public string artist;
        public uint version;
        public string bpm;
        public float diffNormal;
        public float diffHard;
        public float diffExpert;
        public float diffInferno;
        public int verAdded;
        public int verRemoved;
        public int infAdded;
    }
}
