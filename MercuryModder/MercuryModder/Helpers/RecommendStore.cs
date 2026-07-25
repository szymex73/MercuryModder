using SonicAudioLib.Archives;
using Tomlet;
using Tomlet.Attributes;

namespace MercuryModder.Helpers;

public class RecommendStore
{
    public static List<string> ReadFromFile(string path)
    {
        if (!File.Exists(path)) return new List<string>();
        return TomletMain.To<RecommendConfig>(File.ReadAllText(path)).Songs.ToList();
    }
    
    public static void SaveToFile(List<string> songs, string path)
    {
        var c = new RecommendConfig();
        c.Songs = songs.ToArray();
        File.WriteAllText(path, TomletMain.TomlStringFrom(c));
    }

    internal class RecommendConfig
    {
        [TomlProperty("songs")]
        public string[] Songs { get; set; }
    }
}
