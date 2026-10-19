using System.CommandLine;
using System.Text;
using MercuryModder.Assets;
using MercuryModder.Helpers;
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

public class TablePatchCommand : ICommand
{
    public Command Build()
    {
        var cmd = new Command("tablepatch", "Load table patches.")
        {
            new Option<DirectoryInfo>("--gameDir") {
                Description = "Path to the game base directory (WindowsNoEditor)",
                Required = true
            },
            new Option<DirectoryInfo>("--patches") {
                Description = "Where patches are located",
                Required = true
            },
            new Option<DirectoryInfo>("--output") {
                Description = "Where to output patched tables",
                Required = true
            },
        };
        
        cmd.SetAction(Command);
        
        return cmd;
    }


    public static void Command(ParseResult result)
    {
        DirectoryInfo gameDir = result.GetValue<DirectoryInfo>("--gameDir");
        DirectoryInfo patchesDir = result.GetValue<DirectoryInfo>("--patches");
        DirectoryInfo outputDir = result.GetValue<DirectoryInfo>("--output");

        Dictionary<(string Type, string Name), UAsset> assetCache = new();
        
        foreach (var patchFile in patchesDir.EnumerateFiles("*.toml", SearchOption.AllDirectories).Order(Comparer<FileInfo>.Create((f1, f2) => f1.FullName.CompareTo(f2.FullName))))
        {
            Console.WriteLine($"Processing {patchFile.FullName}");
            var doc = TomlParser.ParseFile(patchFile.FullName);
            string? tableName = null; 
            string assetType = "";
            if (doc.ContainsKey("table") && doc.ContainsKey("message")) throw new InvalidDataException($"Cannot specify both a data and message table ({patchFile.FullName})");
            if (doc.ContainsKey("table")) {
                tableName = doc.GetString("table");
                assetType = "Table";
            } else if (doc.ContainsKey("message"))
            {
                tableName = doc.GetString("message");
                assetType = "Message";
            } else
            {
                throw new InvalidDataException($"No table or message name specified ({patchFile.FullName})");
            }

            List<RowEdit> edits = new();
            ReadDocument(doc, edits);

            if (!assetCache.TryGetValue((assetType, tableName), out UAsset tableAsset))
            {
                tableAsset = new UAsset($"{gameDir}/Mercury/Content/{assetType}/{tableName}.uasset", true, EngineVersion.VER_UE4_19);
                assetCache.Add((assetType, tableName), tableAsset);
            }
            DataTableExport tableExport = tableAsset[tableName] as DataTableExport;
            List<StructPropertyData> tableData = tableExport.Table.Data;

            foreach (RowEdit edit in edits)
            {
                switch (edit.Action)
                {
                    case RowAction.Patch:
                        StructPropertyData existing = (tableData.Find(row => row.Name.ToString() == $"{edit.Row}")) ?? throw new InvalidDataException($"row {edit.Row} does not exist");

                        foreach (KeyValuePair<string, TomlValue> kv in edit.Data)
                        {
                            PropertyData field = existing[kv.Key]
                                ?? throw new InvalidDataException($"{edit.Row}: no field {kv.Key} in {tableName}");
                            AssignProperty(new PatchContext(tableAsset, tableExport.Table, existing, field), kv.Value);
                        }

                        break;
                    case RowAction.Insert:
                        if (tableData.Exists(row => row.Name.ToString() == $"{edit.Row}"))
                            throw new InvalidDataException($"Row named '{edit.Row}' in '{tableName}' already exists");
                        
                        string? anchor = edit.Before ?? (edit.After == null ? null : edit.After);
                        int index = InsertIndex(tableData, anchor, edit.Before != null);
                        
                        StructPropertyData template = edit.Template != null
                            ? (tableData.Find(row => row.Name.ToString() == $"{edit.Template}"))
                                ?? throw new InvalidDataException($"template row '{edit.Template}' does not exist")
                            : tableData.FirstOrDefault()
                                ?? throw new InvalidDataException("table is empty, nothing to use as a template");
                        
                        StructPropertyData newRow = DeepClone(template);
                        FName rowName = FName.FromString(tableAsset, edit.Row);
                        tableAsset.AddNameReference(rowName.Value);
                        newRow.Name = rowName;

                        List<string> fields = newRow.Value.Select(p => p.Name.ToString()).ToList();
                        foreach (KeyValuePair<string, TomlValue> kv in edit.Data)
                        {
                            fields.Remove(kv.Key);
                            PropertyData field = newRow[kv.Key]
                                ?? throw new InvalidDataException($"{edit.Row}: no field {kv.Key} in {tableName}");
                            AssignProperty(new PatchContext(tableAsset, tableExport.Table, newRow, field), kv.Value);
                        }

                        if (edit.Template == null && fields.Count > 0)
                        {
                            throw new InvalidDataException($"\tNew row {edit.Row} did not overwrite the following fields: {String.Join(",", fields)}");
                        }

                        tableData.Insert(index, newRow);

                        break;
                    case RowAction.Delete:
                        if (!tableData.Exists(row => row.Name.ToString() == $"{edit.Row}"))
                            throw new InvalidDataException($"Row named '{edit.Row}' in '{tableName}' doesn't exist");
                        
                        StructPropertyData target = (tableData.Find(row => row.Name.ToString() == $"{edit.Row}")) ?? throw new InvalidDataException($"row {edit.Row} does not exist");
                        tableData.Remove(target);
                        break;
                    default:
                        throw new NotSupportedException($"action {edit.Action} not supported");
                }
            }
        }

        foreach (var ((assetType, tableName), outAsset) in assetCache)
        {
            Directory.CreateDirectory($"{outputDir}/Mercury/Content/{assetType}/");
            outAsset.Write($"{outputDir}/Mercury/Content/{assetType}/{tableName}.uasset");
        }
    }

    public static void AssignProperty(PatchContext ctx, TomlValue val)
    {
        switch (ctx.Prop)
        {
            case BytePropertyData p:
                p.Value = (byte) Expect<TomlLong>(ctx, val).Value;
                break;
            case Int8PropertyData p:
                p.Value = (sbyte) Expect<TomlLong>(ctx, val).Value;
                break;
            case Int16PropertyData p:
                p.Value = (short) Expect<TomlLong>(ctx, val).Value;
                break;
            case UInt16PropertyData p:
                p.Value = (ushort) Expect<TomlLong>(ctx, val).Value;
                break;
            case IntPropertyData p:
                p.Value = (int) Expect<TomlLong>(ctx, val).Value;
                break;
            case UInt32PropertyData p:
                p.Value = (uint) Expect<TomlLong>(ctx, val).Value;
                break;
            case Int64PropertyData p:
                p.Value = (long) Expect<TomlLong>(ctx, val).Value;
                break;
            case UInt64PropertyData p:
                p.Value = (ulong) Expect<TomlLong>(ctx, val).Value;
                break;
            case FloatPropertyData p:
                p.Value = val switch
                {
                    TomlDouble d => (float) d.Value,
                    TomlLong l => l.Value,
                    _ => throw new InvalidDataException($"{ctx.Row.Name}.{ctx.Prop.Name}: expected number/float but got {val.GetType().Name}")
                };
                break;
            case DoublePropertyData p:
                p.Value = val switch
                {
                    TomlDouble d => d.Value,
                    TomlLong l => l.Value,
                    _ => throw new InvalidDataException($"{ctx.Row.Name}.{ctx.Prop.Name}: expected number/float but got {val.GetType().Name}")
                };
                break;
            case BoolPropertyData p:
                p.Value = Expect<TomlBoolean>(ctx, val).Value;
                break;
            case StrPropertyData p:
                p.Value = FString.FromString(Expect<TomlString>(ctx, val).Value);
                break;
            // NamePropertyData
            // EnumPropertyData
            case ArrayPropertyData p:
                AssignArray(ctx, val);
                break;
            default:
                throw new NotSupportedException($"{ctx.Prop.Name}: assignment to {ctx.Prop.PropertyType} not supported");
        }
    }

    public static void AssignArray(PatchContext ctx, TomlValue val)
    {
        if (val is not TomlArray valArr)
            throw new InvalidDataException($"{ctx.Prop.Name} expected an array");
        
        var items = valArr.ArrayValues;
        ArrayPropertyData arr = ctx.Prop as ArrayPropertyData;

        var proto = items.Count > arr.Value.Length && FindFieldPrototype(ctx, arr) is { } found
            ? DeepClone(found) : null;
        
        var result = new PropertyData[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            PropertyData elem = i < arr.Value.Length
                ? arr.Value[i]
                : DeepClone(proto!);

            AssignProperty(new PatchContext(ctx.Asset, ctx.Table, ctx.Row, elem), items[i]);
            result[i] = elem;
        }

        arr.Value = result;
    }

    public static PropertyData? FindFieldPrototype(PatchContext ctx, ArrayPropertyData arr)
    {
        // Use already placed data as proto
        if (arr.Value.Length > 0) return arr.Value[0];
        
        // Use data from other rows as proto
        foreach (var row in ctx.Table.Data)
        {
            if (row.Value.FirstOrDefault(p => p.Name.Equals(arr.Name)) is ArrayPropertyData other &&
                other.Value.Length > 0)
            {
                return other.Value[0];
            }
        }

        // Create new dummy objects
        FName name = arr.Name;
        return arr.ArrayType.ToString() switch
        {
            "ByteProperty"   => new BytePropertyData(name),
            "IntProperty"    => new IntPropertyData(name),
            "UInt32Property" => new UInt32PropertyData(name),
            "Int64Property"  => new Int64PropertyData(name),
            "UInt64Property" => new UInt64PropertyData(name),
            "FloatProperty"  => new FloatPropertyData(name),
            "DoubleProperty" => new DoublePropertyData(name),
            "BoolProperty"   => new BoolPropertyData(name),
            "StrProperty"    => new StrPropertyData(name),
            // "NameProperty"   => new NamePropertyData(name),
            // "EnumProperty"   => new EnumPropertyData(name),
            var t => throw new InvalidDataException($"{name} array of {t} is empty in every row, so there's no element to copy the type from")
        };
    }

    public static void ReadDocument(TomlDocument document, List<RowEdit> edits)
    {
        string? OptName(TomlTable t, string field) => !t.ContainsKey(field) ? null : t.GetValue(field) switch
        {
            TomlString s => s.Value,
            TomlLong l   => l.Value.ToString(),
            var other    => throw new InvalidDataException($"bad {field} row name: {other}")
        };


        void Read(TomlDocument doc, string key, RowAction action, List<RowEdit> output)
        {
            if (!doc.ContainsKey(key)) return;

            foreach (TomlValue v in doc.GetArray(key))
            {
                if (v is not TomlTable t) throw new InvalidDataException($"{key} must be [[{key}]] entries");

                string row = OptName(t, "row") ?? throw new InvalidDataException($"missing row name");

                string? template = OptName(t, "template");
                string? before = OptName(t, "before");
                string? after = OptName(t, "after");

                if (before != null && after != null) throw new InvalidDataException($"{row}: before and after can't both be set");
                if (action != RowAction.Insert && (before ?? after) != null) throw new InvalidDataException($"{row}: before/after only apply to inserts");

                var fields = t.ContainsKey("data") ? t.GetSubTable("data").Entries : new Dictionary<string, TomlValue>();

                output.Add(new RowEdit(action, row, template, before, after, fields));
            }
        }

        Read(document, "insert", RowAction.Insert, edits);
        Read(document, "patch", RowAction.Patch, edits);
        Read(document, "delete", RowAction.Delete, edits);
    }

    static T Expect<T>(PatchContext ctx, TomlValue val) where T : TomlValue
    {
        return val as T ?? throw new InvalidDataException($"{ctx.Row.Name}.{ctx.Prop.Name}: expected {typeof(T).Name} but got {val.GetType().Name} ({val.SerializedValue})");
    }

    static int InsertIndex(List<StructPropertyData> rows, string? anchor, bool before)
    {
        if (anchor == null) return rows.Count;

        int i = rows.FindIndex(r => r.Name.ToString() == anchor);
        if (i < 0) throw new InvalidDataException($"anchor row '{anchor}' does not exist");

        return before ? i : i + 1;
    }

    static T DeepClone<T>(T p) where T : PropertyData
    {
        var c = (T)p.Clone();
        switch (c)
        {
            case ArrayPropertyData a:
                a.Value = a.Value.Select(DeepClone<PropertyData>).ToArray();
                break;
            case StructPropertyData s:
                s.Value = s.Value.Select(DeepClone<PropertyData>).ToList();
                break;
        }
        return c;
    }
}

public enum RowAction
{
    Patch,
    Insert,
    Delete
}

public record RowEdit(RowAction Action, string Row, string? Template, string? Before, string? After, IReadOnlyDictionary<string, TomlValue> Data);
public record PatchContext(UAsset Asset, UDataTable Table, StructPropertyData Row, PropertyData Prop);
