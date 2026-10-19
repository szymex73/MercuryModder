using System.CommandLine;
using System.Globalization;
using System.Text;
using MercuryModder.Assets;
using MercuryModder.Helpers;
using Newtonsoft.Json;
using SaturnData.Notation.Core;
using SaturnData.Notation.Serialization;
using SkiaSharp;
using SonicAudioLib.Archives;
using Tomlet;
using Tomlet.Attributes;
using Tomlet.Models;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace MercuryModder.Commands;

public class TableDiffCommand : ICommand
{
    public Command Build()
    {
        var cmd = new Command("tablediff", "Load table patches.")
        {
            new Option<FileInfo>("--old") {
                Description = "Old table",
                Required = true
            },
            new Option<FileInfo>("--new") {
                Description = "New table",
                Required = true
            }
        };
        
        cmd.SetAction(Command);
        
        return cmd;
    }


    public static void Command(ParseResult result)
    {
        FileInfo oldAssetPath = result.GetValue<FileInfo>("--old");
        FileInfo newAssetPath = result.GetValue<FileInfo>("--new");

        UAsset oldAsset = new UAsset(oldAssetPath.FullName, true, EngineVersion.VER_UE4_19);
        UAsset newAsset = new UAsset(newAssetPath.FullName, true, EngineVersion.VER_UE4_19);

        DataTableExport oldExport = oldAsset.Exports.OfType<DataTableExport>().Single();
        string tableName = oldExport.ObjectName.ToString();
        DataTableExport newExport = newAsset[tableName] as DataTableExport;

        TableDiff diff = DiffTableAssets(tableName, oldExport.Table, newExport.Table);

        if (diff.Patches.Count == 0 && diff.Inserts.Count == 0 && !diff.Reordered && diff.Removed.Count == 0) return;
        Console.WriteLine(ToToml(diff, newAssetPath.FullName.Contains("Message/") ? "message" : "table"));
    }

    public static TableDiff DiffTableAssets(string table, UDataTable a, UDataTable b)
    {
        var aRows = a.Data.ToDictionary(r => r.Name.ToString());
        var bNames = b.Data.Select(r => r.Name.ToString()).ToList();
        var bSet = bNames.ToHashSet();

        var inserts = new List<RowInsert>();
        var patches = new List<RowPatch>();

        for (int i = 0; i < b.Data.Count; i++)
        {
            string name = bNames[i];
            var fields = RowToPlainValues(b.Data[i]);

            if (!aRows.TryGetValue(name, out var aRow))
            {
                string? after = i > 0 ? bNames[i - 1] : null;
                string? before = i == 0 ? bNames.Skip(1).FirstOrDefault(aRows.ContainsKey) : null;

                inserts.Add(new RowInsert(name, before, after, fields));
                continue;
            }

            var aFields = RowToPlainValues(aRow);
            var changes = new List<FieldChange>();
            foreach (var (field, value) in fields)
            {
                if (!aFields.TryGetValue(field, out var old))
                {
                    Console.Error.WriteLine($"{name}: field {field} missing from old table, struct difference");
                    changes.Add(new FieldChange(field, null, value));
                    continue;
                }

                if (!PlainValueEquals(old, value)) changes.Add(new FieldChange(field, old, value));
            }

            if (changes.Count > 0) patches.Add(new RowPatch(name, changes));
        }

        var removed = a.Data.Select(r => r.Name.ToString()).Where(n => !bSet.Contains(n)).ToList();

        bool reordered = !a.Data.Select(r => r.Name.ToString()).Where(bSet.Contains).SequenceEqual(bNames.Where(aRows.ContainsKey));

        return new TableDiff(table, inserts, patches, removed, reordered);
    }

    public static Dictionary<string, object?> RowToPlainValues(StructPropertyData row)
    {
        var result = new Dictionary<string, object?>();

        foreach (var group in row.Value.GroupBy(p => p.Name.ToString()))
        {
            var props = group.ToList();

            if (props.Count == 1)
            {
                result[group.Key] = ToPlainValue(props[0]);
            }
            else if (props.Select(p => p.ArrayIndex).Distinct().Count() == props.Count)
            {
                result[group.Key] = props.OrderBy(p => p.ArrayIndex).Select(ToPlainValue).ToList();
            }
            else
            {
                Console.Error.WriteLine($"warning: {row.Name}: '{group.Key}' is serialized {props.Count} times, using the last one");
                result[group.Key] = ToPlainValue(props[^1]);
            }
        }

        return result;
    }

    public static object? ToPlainValue(PropertyData p)
    {
        return p switch {
            BytePropertyData v => (long) v.Value,
            Int8PropertyData v => (long) v.Value,
            Int16PropertyData v => (long) v.Value,
            UInt16PropertyData v => (long) v.Value,
            IntPropertyData v => (long) v.Value,
            UInt32PropertyData v => (long) v.Value,
            Int64PropertyData v => (long) v.Value,
            UInt64PropertyData v => (long) v.Value,
            FloatPropertyData v => v.Value,
            DoublePropertyData v => v.Value,
            BoolPropertyData v => v.Value,
            StrPropertyData v => v.Value?.Value ?? "",
            // NamePropertyData
            // EnumPropertyData
            ArrayPropertyData v => v.Value.Select(ToPlainValue).ToList(),
            _ => new Unsupported(p.PropertyType.ToString(), JsonConvert.SerializeObject(p, UAsset.jsonSettings))
        };
    }

    public static bool PlainValueEquals(object? a, object? b)
    {
        return (a, b) switch {
            (List<object?> x, List<object?> y) => x.Count == y.Count && x.Zip(y).All(t => PlainValueEquals(t.First, t.Second)),
            (Dictionary<string, object?> x, Dictionary<string, object?> y) => x.Count == y.Count && x.All(kv => y.TryGetValue(kv.Key, out var yv) && PlainValueEquals(kv.Value, yv)),
            _ => Equals(a, b),
        };
    }

    // TOML helpers
    public static string ToToml(TableDiff diff, string kind = "table")
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{kind} = {EscapeString(diff.Table)}");

        if (diff.Reordered) sb.AppendLine($"# NOTE: rows reordered in new table ver");

        foreach (var removed in diff.Removed)
        {
            sb.AppendLine();
            sb.AppendLine("[[delete]]");
            sb.AppendLine($"row = {EscapeString(removed)}");
        }

        foreach (var patch in diff.Patches)
        {
            sb.AppendLine();
            sb.AppendLine("[[patch]]");
            sb.AppendLine($"row = {EscapeString(patch.Row)}");
            sb.AppendLine("[patch.data]");
            foreach (var c in patch.Changes)
            {
                if (ContainsUnsupported(c.New))
                {
                    sb.AppendLine($"# {c.Field} has unsupported type");
                    continue;
                }
                sb.Append($"{Key(c.Field)} = {StringValue(c.New)}");
                if (c.Old is null) sb.Append(" # is missing in old row");
                else if (!(c.Old is List<object?> or Dictionary<string, object?>)) sb.Append($" # was {StringValue(c.Old)}");
                sb.AppendLine();
            }
        }

        foreach (var ins in diff.Inserts)
        {
            sb.AppendLine();
            sb.AppendLine("[[insert]]");
            sb.AppendLine($"row = {EscapeString(ins.Row)}");
            if (ins.Before != null) sb.AppendLine($"before = {EscapeString(ins.Before)}");
            if (ins.After != null) sb.AppendLine($"after = {EscapeString(ins.After)}");
            sb.AppendLine("[insert.data]");

            foreach (var (field, value) in ins.Fields)
            {
                if (ContainsUnsupported(value))
                {
                    sb.AppendLine($"# {field} has unsupported type");
                    continue;
                }
                sb.Append($"{Key(field)} = {StringValue(value)}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    public static bool ContainsUnsupported(object? v) {
        return v switch {
            Unsupported => true,
            List<object?> l => l.Any(ContainsUnsupported),
            _ => false
        };
    }

    public static string Key(string k) {
        return k.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') ? k : EscapeString(k);
    }


    public static string StringValue(object? o)
    {
        return o switch {
            null => "\"\"",
            bool v => v ? "true" : "false",
            long v => v.ToString(CultureInfo.InvariantCulture),
            float v => FloatValue(v.ToString("R", CultureInfo.InvariantCulture), v),
            double v => FloatValue(v.ToString("R", CultureInfo.InvariantCulture), v),
            string v => EscapeString(v),
            List<object?> { Count: 0 } => "[]",
            List<object?> v => $"[ {string.Join(", ", v.Select(StringValue))} ]",
            _ => throw new NotSupportedException($"can't write {o.GetType().Name} to TOML")
        };
    }

    public static string FloatValue(string s, double d)
    {
        if (double.IsNaN(d)) return "nan";
        if (double.IsPositiveInfinity(d)) return "inf";
        if (double.IsNegativeInfinity(d)) return "-inf";
        if (s.Contains(".") || s.Contains("E")) return s;
        return s + ".0";
    }

    static string EscapeString(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
            sb.Append(c switch
            {
                '"'  => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                < ' ' or '\x7f' => $"\\u{(int)c:X4}",
                _ => c.ToString()
            });
        return sb.Append('"').ToString();
    }
}

public record FieldChange(string Field, object? Old, object? New);
public record RowPatch(string Row, List<FieldChange> Changes);
public record RowInsert(string Row, string? Before, string? After, Dictionary<string, object?> Fields);
public record TableDiff(string Table, List<RowInsert> Inserts, List<RowPatch> Patches, List<string> Removed, bool Reordered);
public record Unsupported(string Type, string Fingerprint);
