using System.Text.Json;

namespace DragonIdle;

/// <summary>Player preferences shared by all save slots, stored next to the saves.</summary>
public class Settings
{
    public static readonly int[] AutosaveChoices = { 0, 15, 30, 60, 120 };

    /// <summary>Seconds between autosaves; 0 turns autosave off.</summary>
    public int AutosaveSeconds { get; set; } = 30;
    public bool Muted { get; set; }

    public bool Autosave => AutosaveSeconds > 0;

    static string FilePath => Path.Combine(Game.SaveDir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { /* fall back to defaults */ }
        return new Settings();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); } catch { /* best effort */ }
    }

    public void CycleAutosave()
    {
        int i = Array.IndexOf(AutosaveChoices, AutosaveSeconds);
        AutosaveSeconds = AutosaveChoices[(i + 1) % AutosaveChoices.Length];
        Save();
    }

    public static string Describe(int seconds) => seconds switch
    {
        0 => "OFF",
        < 60 => $"{seconds}s",
        _ => $"{seconds / 60}m",
    };
}
