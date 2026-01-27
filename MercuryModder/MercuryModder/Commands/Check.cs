using MercuryModder.Helpers;

namespace MercuryModder.Commands;

public class Check
{
    // Used both as dir names and for genre indexing
    static string[] GENRES = new string[] { "Anipop", "Vocaloid", "Touhou", "2_5D", "Variety", "Original", "TanoC" };

    public static void Command(DirectoryInfo trackDir, bool printInfo)
    {
        foreach (var genre in GENRES)
        {
            var genreId = Array.IndexOf(GENRES, genre);
            var genreDir = Path.Combine(trackDir.ToString(), genre);

            if (!Directory.Exists(genreDir))
            {
                Console.WriteLine($"Genre directory {genreDir} does not exist");
                continue;
            }

            foreach (var songDir in Directory.GetDirectories(genreDir))
            {
                try
                {
                    var song = Song.Load(songDir);

                    if (!CheckSong(song, out var problems, out var warnings))
                    {
                        if (problems.Length != 0)
                        {
                            Console.WriteLine($"[FAIL] {songDir}");
                        }
                        else if (warnings.Length != 0)
                        {
                            Console.WriteLine($"[WARN] {songDir}");
                        }

                        if (printInfo) PrintSong(song);

                        foreach (var prob in problems) Console.WriteLine($"\t[E] {prob}");
                        foreach (var warn in warnings) Console.WriteLine($"\t[W] {warn}");
                    }
                    else
                    {
                        Console.WriteLine($"[ OK ] {songDir}");
                        if (printInfo) PrintSong(song);
                    }
                }
                catch (ExceptionList e)
                {
                    Console.WriteLine(e);
                    Console.WriteLine($"[FAIL] SaturnData errored while loading song from {songDir}, see System.Exception lines above");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[FAIL] Errored while loading song from {songDir}");
                    Console.WriteLine(e);
                }
            }
        }

        // This portion is kinda meh
        // TODO: Check if the songId is a valid song ID?
        // Tho this would require passing in the game dir for the check which is not ideal...
        var infernoDir = Path.Combine(trackDir.ToString(), "Inferno");
        if (Directory.Exists(infernoDir))
        {
            foreach (var songDir in Directory.GetDirectories(infernoDir))
            {
                try
                {
                    int songId = Convert.ToInt32(Path.GetFileName(songDir));
                    Song song = Song.Load(songDir);
                    bool hasSeparateAudio = File.Exists($"{song.Directory}/track.wav");

                    if (!CheckInferno(song, out var problems, out var warnings))
                    {
                        if (problems.Length != 0)
                        {
                            Console.WriteLine($"[FAIL] {songDir}");
                        }
                        else if (warnings.Length != 0)
                        {
                            Console.WriteLine($"[WARN] {songDir}");
                        }

                        if (printInfo)
                        {
                            Console.WriteLine($"\tChart Inferno: {song.Inferno.Entry.Level:00.00} - {song.Inferno.Entry.NotesDesigner}");
                            Console.WriteLine($"\tSeparate audio: {hasSeparateAudio}");
                        }

                        foreach (var prob in problems) Console.WriteLine($"\t[E] {prob}");
                        foreach (var warn in warnings) Console.WriteLine($"\t[W] {warn}");
                    } else
                    {
                        Console.WriteLine($"[ OK ] Inferno for song {songId}");
                        if (printInfo)
                        {
                            Console.WriteLine($"\tChart Inferno: {song.Inferno.Entry.Level:00.00} - {song.Inferno.Entry.NotesDesigner}");
                            Console.WriteLine($"\tSeparate audio: {hasSeparateAudio}");
                        }
                    }
                } catch (Exception e)
                {
                    Console.WriteLine($"[FAIL] Errored while loading inferno chart from {songDir}");
                    Console.WriteLine(e);
                }
            }
        }
    }

    public static void PrintSong(Song song)
    {
        Console.WriteLine($"\tLoad type: {song.LoadType}");
        Console.WriteLine($"\tTitle: {song.Info.Title}");
        Console.WriteLine($"\tRubi: {song.Info.Rubi}");
        Console.WriteLine($"\tArtist: {song.Info.Artist}");
        Console.WriteLine($"\tBpm: {song.Info.Bpm}");
        Console.WriteLine($"\tGenre: {song.Info.Genre}");
        Console.WriteLine($"\tPreview: {TimeSpan.FromSeconds(song.Info.PreviewStart):mm\\:ss}-{TimeSpan.FromSeconds(song.Info.PreviewStart + song.Info.PreviewLength):mm\\:ss}");
        if (!song.Normal.Dummy) Console.WriteLine($"\tChart Normal : {song.Normal.Entry.Level:00.00} - {song.Normal.Entry.NotesDesigner} ({song.Normal.FileName})");
        else Console.WriteLine($"\tChart Normal : Missing");
        if (!song.Hard.Dummy) Console.WriteLine($"\tChart Hard   : {song.Hard.Entry.Level:00.00} - {song.Hard.Entry.NotesDesigner} ({song.Hard.FileName})");
        else Console.WriteLine($"\tChart Hard   : Missing");
        if (!song.Expert.Dummy) Console.WriteLine($"\tChart Expert : {song.Expert.Entry.Level:00.00} - {song.Expert.Entry.NotesDesigner} ({song.Expert.FileName})");
        else Console.WriteLine($"\tChart Expert : Missing");
        if (!song.Inferno.Dummy) Console.WriteLine($"\tChart Inferno: {song.Inferno.Entry.Level:00.00} - {song.Inferno.Entry.NotesDesigner} ({song.Inferno.FileName})");
    }

    public static bool CheckSong(Song song, out string[] problems, out string[] warnings)
    {
        var probs = new List<string>();
        var warns = new List<string>();

        if (song.Info.Title is null) probs.Add("Song title is not defined");
        if (song.Info.Title == "") probs.Add("Song title is empty");
        if (song.Info.Rubi is null) probs.Add("Song rubi/reading is not defined");
        if (song.Info.Rubi == "") probs.Add("Song rubi/reading is empty");
        if (song.Info.Artist is null) probs.Add("Song artist is not defined");
        if (song.Info.Artist == "") probs.Add("Song artist is empty");
        if (song.Info.Bpm is null) probs.Add("Song Bpm is not defined");
        if (song.Info.Bpm == "") probs.Add("Song Bpm is empty");
        if (song.Info.Genre is null) probs.Add("Genre is not defined");
        if (song.Info.Genre == "" || Array.IndexOf(GENRES, song.Info.Genre) == -1) probs.Add($"Invalid genre {song.Info.Genre}");

        if (!File.Exists(Path.Join(song.Directory.ToString(), "jacket.png"))) probs.Add("jacket.png is missing");
        // Add size checks?

        if (!File.Exists(Path.Join(song.Directory.ToString(), "track.wav"))) probs.Add("track.wav is missing");

        if (song.Normal.Dummy) warns.Add("Normal diff was not provided");
        if (song.Hard.Dummy) warns.Add("Hard diff was not provided");
        if (song.Expert.Dummy) warns.Add("Expert diff was not provided");
        // if (song.Inferno.Dummy) warns.Add("Inferno diff was not provided"); // Causes a bunch of spam, and it's not necessary
        if (song.Normal.Dummy && song.Hard.Dummy && song.Expert.Dummy && song.Inferno.Dummy) probs.Add("No chart was provided");

        if (song.Normal.Entry.NotesDesigner == "") warns.Add("Normal diff note designer is empty");
        if (song.Hard.Entry.NotesDesigner == "") warns.Add("Hard diff note designer is empty");
        if (song.Expert.Entry.NotesDesigner == "") warns.Add("Expert diff note designer is empty");
        if (song.Inferno.Entry.NotesDesigner == "") warns.Add("Inferno diff note designer is empty");

        if (song.Normal.Entry.Level == 0f) warns.Add("Normal diff has level set to 0");
        if (song.Hard.Entry.Level == 0f) warns.Add("Hard diff has level set to 0");
        if (song.Expert.Entry.Level == 0f) warns.Add("Expert diff has level set to 0");
        if (!song.Inferno.Dummy && song.Inferno.Entry.Level == 0f) warns.Add("Inferno diff has level set to 0");

        problems = probs.ToArray();
        warnings = warns.ToArray();
        return problems.Length == 0 && warnings.Length == 0;
    }

    public static bool CheckInferno(Song song, out string[] problems, out string[] warnings)
    {
        var probs = new List<string>();
        var warns = new List<string>();

        if (song.Inferno.Dummy) probs.Add("Inferno chart not provided");
        if (song.Inferno.Entry.NotesDesigner == "") probs.Add("Inferno diff note designer is empty");
        if (song.Inferno.Entry.Level == 0f) probs.Add("Inferno diff has level set to 0");

        problems = probs.ToArray();
        warnings = warns.ToArray();
        return problems.Length == 0 && warnings.Length == 0;
    }
}
