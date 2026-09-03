using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DittoBuddy;

public record CustomReminder(string Time, string Text)
{
    public override string ToString() => $"{Time}   {Text}";
}

public class ReminderSettings
{
    public bool WorkScheduleEnabled { get; set; }
    public string LunchStart { get; set; } = "12:00";
    public string WorkEnd { get; set; } = "18:00";
    public List<CustomReminder> Custom { get; set; } = new();
}

internal static class ReminderStore
{
    private static readonly string FilePath = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "DittoBuddy", "reminders.json");

    public static ReminderSettings Load()
    {
        if (!File.Exists(FilePath)) return new ReminderSettings();
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<ReminderSettings>(json) ?? new ReminderSettings();
        }
        catch
        {
            return new ReminderSettings();
        }
    }

    public static void Save(ReminderSettings settings)
    {
        var dir = System.IO.Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
    }
}
