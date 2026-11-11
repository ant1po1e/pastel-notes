using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PastelNotes.Models;

namespace PastelNotes.Services;

public class StorageService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PastelNotes", "notes.json");

    public AppData? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonSerializer.Deserialize<AppData>(File.ReadAllText(FilePath), Options);
        }
        catch
        {
            try { File.Copy(FilePath, FilePath + ".corrupt", true); } catch { /* abaikan */ }
            return null;
        }
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Options));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
