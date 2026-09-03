using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DittoBuddy;

public record ShortcutEntry(string Name, string Path);

internal static class ShortcutStore
{
    private static readonly string FilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DittoBuddy", "shortcuts.json");

    public static List<ShortcutEntry> Load()
    {
        if (!File.Exists(FilePath)) return new List<ShortcutEntry>();
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<ShortcutEntry>>(json) ?? new List<ShortcutEntry>();
        }
        catch
        {
            return new List<ShortcutEntry>();
        }
    }

    public static void Save(List<ShortcutEntry> entries)
    {
        var dir = System.IO.Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(entries));
    }
}
