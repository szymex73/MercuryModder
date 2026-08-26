using SaturnData.Notation.Core;
using SaturnData.Notation.Serialization;
using Tomlet;
using Tomlet.Attributes;

namespace MercuryModder.Helpers;

public class Song
{
    public DirectoryInfo Directory;
    public SongInfo Info { get; private set; }
    public SongExtra? Extra { get ; private set; }

    public ChartContainer Normal { get; private set; }
    public ChartContainer Hard { get; private set; }
    public ChartContainer Expert { get; private set; }
    public ChartContainer? Inferno { get; private set; }

    public SongLoadType LoadType { get; private set; }

    public Song(DirectoryInfo songDirectory)
    {
        Directory = songDirectory;
    }

    public string GetRelativePath()
    {
        return $"{Directory.Parent.Name}/{Directory.Name}";
    }

    public static Song Load(string songDir)
    {
        var songDirectory = new DirectoryInfo(songDir);

        var merFiles = songDirectory.GetFiles("*.mer");
        var satFiles = songDirectory.GetFiles("*.sat");
        satFiles = satFiles.Concat(songDirectory.GetFiles("*.map")).ToArray();

        if (merFiles.Length != 0 && satFiles.Length != 0) throw new Exception("Song directory can only contain .sat/.map or .mer files, not both");

        if (merFiles.Length != 0) return LoadMer(songDirectory);
        if (satFiles.Length != 0) return LoadSat(songDirectory);

        throw new Exception("Song directory does not contain any chart files");
    }

    public static Song LoadSat(DirectoryInfo songDirectory)
    {
        var extraPath = songDirectory.ToString() + "/extra.toml";
        SongExtra extra = new();
        if (File.Exists(extraPath)) extra = TomletMain.To<SongExtra>(File.ReadAllText(extraPath));


        var nra = new NotationReadArgs();

        ChartContainer normal = ChartContainer.GetDummy(1f);
        ChartContainer hard = ChartContainer.GetDummy(1f);
        ChartContainer expert = ChartContainer.GetDummy(1f);
        ChartContainer inferno = ChartContainer.GetDummy(0f);

        var files = songDirectory.GetFiles("*.sat");
        files = files.Concat(songDirectory.GetFiles("*.map")).ToArray();
        foreach (var file in files.OrderBy(f => f.FullName))
        {
            var cc = new ChartContainer(file.FullName, nra);
            
            if (cc.Entry.Difficulty == Difficulty.Normal) normal = cc;
            if (cc.Entry.Difficulty == Difficulty.Hard) hard = cc;
            if (cc.Entry.Difficulty == Difficulty.Expert) expert = cc;
            if (cc.Entry.Difficulty == Difficulty.Inferno) inferno = cc;
        }

        // TODO: Kinda scuffed but best way to do it for now?
        SongInfo? meta = null;
        if (!normal.Dummy) meta = SongInfo.FromEntry(normal.Entry);
        else if (!hard.Dummy) meta = SongInfo.FromEntry(hard.Entry);
        else if (!expert.Dummy) meta = SongInfo.FromEntry(expert.Entry);
        else if (!inferno.Dummy) meta = SongInfo.FromEntry(inferno.Entry);
        
        if (meta == null)
        {
            throw new Exception($"[LoadSat] No difficulty with metadata provided for {songDirectory}");
        }
        
        meta.Genre = songDirectory.Parent.Name;

        return new Song(songDirectory)
        {
            Info = meta,
            Normal = normal,
            Hard = hard,
            Expert = expert,
            Inferno = inferno,
            LoadType = SongLoadType.LOAD_SAT,
            Extra = extra
        };
    }

    public static Song LoadMer(DirectoryInfo songDirectory)
    {
        var metaPath = songDirectory.ToString() + "/meta.toml";
        if (!File.Exists(metaPath)) throw new Exception("[LoadMer] Song directory does not contain a meta.toml file");
        var meta = TomletMain.To<SongInfo>(File.ReadAllText(metaPath));

        var extraPath = songDirectory.ToString() + "/extra.toml";
        SongExtra extra = new();
        if (File.Exists(extraPath)) extra = TomletMain.To<SongExtra>(File.ReadAllText(extraPath));

        var nra = new NotationReadArgs();

        // Load all difficulties with dummy values and replace with proper charts
        // TODO: Kinda meh but will do for now
        ChartContainer normal = ChartContainer.GetDummy(1f);
        ChartContainer hard = ChartContainer.GetDummy(1f);
        ChartContainer expert = ChartContainer.GetDummy(1f);
        ChartContainer inferno = ChartContainer.GetDummy(0f);
        if (File.Exists($"{songDirectory}/normal.mer")) normal = new ChartContainer($"{songDirectory}/normal.mer", nra);
        if (File.Exists($"{songDirectory}/0.mer")) normal = new ChartContainer($"{songDirectory}/0.mer", nra);
        if (File.Exists($"{songDirectory}/hard.mer")) hard = new ChartContainer($"{songDirectory}/hard.mer", nra);
        if (File.Exists($"{songDirectory}/1.mer")) hard = new ChartContainer($"{songDirectory}/1.mer", nra);
        if (File.Exists($"{songDirectory}/expert.mer")) expert = new ChartContainer($"{songDirectory}/expert.mer", nra);
        if (File.Exists($"{songDirectory}/2.mer")) expert = new ChartContainer($"{songDirectory}/2.mer", nra);
        if (File.Exists($"{songDirectory}/inferno.mer")) inferno = new ChartContainer($"{songDirectory}/inferno.mer", nra);
        if (File.Exists($"{songDirectory}/3.mer")) inferno = new ChartContainer($"{songDirectory}/3.mer", nra);

        meta.Genre = songDirectory.Parent.Name;

        // TODO: there has to be a better way...
        foreach (var diff in meta.Difficulties)
        {
            if (diff.Name == "normal") {
                normal.Entry.Level = (float) diff.Difficulty;
                normal.Entry.NotesDesigner = diff.Designer;
            }
            if (diff.Name == "hard") {
                hard.Entry.Level = (float) diff.Difficulty;
                hard.Entry.NotesDesigner = diff.Designer;
            }
            if (diff.Name == "expert") {
                expert.Entry.Level = (float) diff.Difficulty;
                expert.Entry.NotesDesigner = diff.Designer;
            }
            if (diff.Name == "inferno" && inferno != null) {
                inferno.Entry.Level = (float) diff.Difficulty;
                inferno.Entry.NotesDesigner = diff.Designer;
            }

        }

        return new Song(songDirectory)
        {
            Info = meta,
            Normal = normal,
            Hard = hard,
            Expert = expert,
            Inferno = inferno,
            LoadType = SongLoadType.LOAD_MER,
            Extra = extra
        };
    }

    public class SongInfo
    {
        [TomlProperty("title")]
        public string Title { get; set; }
        [TomlProperty("artist")]
        public string Artist { get; set; }
        [TomlProperty("rubi")]
        public string Rubi { get; set; }
        [TomlProperty("bpm")]
        public string Bpm { get; set; }
        // TODO: Hack for backcompat with prerelease version
        [TomlProperty("genre")]
        public string Genre
        {
            get => _genre;
            set
            {
                if (value == "Anime/POP") _genre = "Anipop";
                else if (value == "HARDCORE TANO*C") _genre = "TanoC";
                else _genre = value;
            }
        }

        public string GenreOffline
        {
            get
            {
                if (_genre == "Vocaloid") return "Variety";
                if (_genre == "2_5D") return "Variety";
                return _genre;
            }
        }
        private string _genre;
        [TomlProperty("preview_start")]
        public float PreviewStart { get; set; }
        [TomlProperty("preview_length")]
        public float PreviewLength { get; set; }

        // TODO: Maybe get rid of this?
        [TomlProperty("difficulties")]
        public ChartDifficulty[]? Difficulties { get; set; }


        public static SongInfo FromEntry(Entry entry)
        {
            return new SongInfo()
            {
                Title = entry.Title,
                Artist = entry.Artist,
                Rubi = entry.Reading,
                Bpm = entry.BpmMessage,
                PreviewStart = entry.PreviewBegin.Time / 1000f,
                PreviewLength = (entry.PreviewEnd.Time - entry.PreviewBegin.Time) / 1000f
            };
        }
    }

    public class ChartDifficulty
    {
        public static string[] DIFFICULTIES = ["normal", "hard", "expert", "inferno"];


        [TomlProperty("name")]
        public string? Name { get; set; }
        [TomlProperty("difficulty")]
        public float? Difficulty { get; set; }
        [TomlProperty("designer")]
        public string? Designer { get; set; }

        public int DifficultyId { get => Array.IndexOf(DIFFICULTIES, Name); }
    }


    public class ChartContainer
    {
        public Chart Chart;
        public Entry Entry;
        public string FileName;
        private bool _dummy = false;
        public bool Dummy { get => _dummy; }

        public ChartContainer(string path, NotationReadArgs nra)
        {
            Chart = NotationSerializer.ToChart(path, nra, out var chartExceptions);
            if (chartExceptions.Count != 0) throw new ExceptionList($"Exceptions occured while loading chart for \"{path}\"", chartExceptions);
            Entry = NotationSerializer.ToEntry(path, nra, out var entryExceptions);
            if (entryExceptions.Count != 0) throw new ExceptionList($"Exceptions occured while loading entry for \"{path}\"", entryExceptions);

            if (path.EndsWith(".mer") && Entry.ChartFormatVersion != ChartFormatVersion.Mer) throw new ExceptionList("Received a Sat format file with a .mer extension", []);

            FileName = Path.GetFileName(path);
            Chart.Build(Entry);
        }

        public ChartContainer(Chart chart, Entry entry)
        {
            Chart = chart;
            Entry = entry;
        }

        public static ChartContainer GetDummy(float difficulty)
        {
            var assembly = typeof(ChartContainer).Assembly;
            Stream resource = assembly.GetManifestResourceStream("MercuryModder.MercuryModder.Resources.dummy.mer");
            List<string> linesList = new List<string>();
            using (StreamReader reader = new StreamReader(resource))
            {
                while (!reader.EndOfStream) linesList.Add(reader.ReadLine());
            }
            string[] lines = linesList.ToArray();

            NotationReadArgs nra = new NotationReadArgs();
            Chart chart = NotationSerializer.ToChart(lines, nra, out var chartExceptions);
            Entry entry = NotationSerializer.ToEntry(lines, nra, out var entryExceptions);

            chart.Build(entry);

            entry.Title = "Dummy chart";
            entry.Artist = "MercuryModder";
            entry.NotesDesigner = "MercuryModder (dummy)";
            entry.Level = difficulty;

            ChartContainer cc = new ChartContainer(chart, entry);
            cc._dummy = true;

            return cc;
        }
    }

    public enum SongLoadType
    {
        LOAD_MER,
        LOAD_SAT
    }

    public enum SongTag
    {
        NONE = 0,
        // Genre
        Original = 1,
        Jpop = 2,
        Anime = 3,
        Vocaloid = 4,
        _25D = 5,
        AniPop = 6,
        Variety = 7,
        Touhou = 8,
        // ?
        SCORE_ROTATION = 1000,
        SCORE_HIGH_SPEED = 1001,
        // Artist
        tanoc = 10000,
        redalice = 10001,
        tpazolite = 10002,
        usao = 10003,
        plight = 10004,
        djgenki = 10005,
        djnoriken = 10006,
        massivenewkrew = 10007,
        djmyosuke = 10008,
        kobaryo = 10009,
        aran = 10010,
        minamotoya = 10011,
        kentavez = 10012,
        noizenecio = 10013,
        srav3r = 10014,
        getty = 10015,
        laur = 10016,
        gram = 10017,
        arufa = 10100,
        vtuber = 10101,
        kizunaai = 10102,
        hololive = 10103,
        cosmo = 10104,
        sakuzyo = 10105,
        camellia = 10106,
        // Collabs
        COLLAB_GROOVE_COASTER = 10200,
        COLLAB_BLEND_S = 10201,
        COLLAB_PRETTY_SERIES = 10202,
        COLLAB_DANMACHI = 10203,
        COLLAB_LANOTA = 10204,
        COLLAB_D4DJ = 10205,
        COLLAB_AZURLANE = 10206,
        COLLAB_ARCAEA = 10207,
        COLLAB_MUSEDASH = 10208,
        DIVE_WITH_YOU = 10209,
        // Boss
        BOSS_XTREME = 20000,
        BOSS_TENSHI = 20001,
    }

    public class SongExtra
    {
        [TomlProperty("release_date")]
        public DateTimeOffset? ReleaseDate { get; set; }
        
        [TomlProperty("inf_release_date")]
        public DateTimeOffset? InfReleaseDate { get; set; }
        
        [TomlProperty("disable_date")]
        public DateTimeOffset? DisableDate { get; set; }

        [TomlProperty("inf_disable_date")]
        public DateTimeOffset? InfDisableDate { get; set; }

        [TomlProperty("tags")]
        public SongTag[] Tags { get; set; } = {};

        [TomlProperty("movie_asset")]
        public string MovieAsset { get; set; } = "-";

        [TomlProperty("init_item")]
        public bool InitItem { get; set; } = true;
        
        [TomlProperty("inf_init_item")]
        public bool InfInitItem { get; set; } = true;

        [TomlProperty("inf_require_purchase")]
        public bool InfRequirePurchase { get; set; } = false;

        [TomlProperty("inf_wp_cost")]
        public int InfWPCost { get; set; } = 0;

        [TomlProperty("original")]
        public bool Original { get; set; } = false;
    }
}
