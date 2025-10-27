using Tomlet;

namespace MercuryModder.Helpers;

public class IDStore
{
    public static Dictionary<string, int> ReadFromFile(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, int>();
        return TomletMain.To<Dictionary<string, int>>(File.ReadAllText(path));
    }
    
    public static void SaveToFile(Dictionary<string, int> dict, string path)
    {
        File.WriteAllText(path, TomletMain.TomlStringFrom(dict));
    }
}
