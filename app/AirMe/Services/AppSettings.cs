using System.IO;
using System.Text.Json;

namespace AirMe.Services;

/// <summary>Ajustes de AirMe, guardados en %APPDATA%\AirMe\settings.json.</summary>
public sealed class AppSettings
{
    public string ReceiverName { get; set; } = "AirMe";
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool FullscreenOnConnect { get; set; } = false;
    public bool RequirePin { get; set; } = false;
    public string Pin { get; set; } = "";

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AirMe");

    private static string FilePath => Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (loaded != null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
        }
        catch
        {
            // archivo dañado: se vuelve a los valores por defecto
        }
        var settings = new AppSettings();
        settings.Normalize();
        return settings;
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public void Normalize()
    {
        ReceiverName = string.IsNullOrWhiteSpace(ReceiverName) ? "AirMe" : ReceiverName.Trim();
        if (ReceiverName.Length > 40) ReceiverName = ReceiverName[..40];
        if (Pin.Length != 4 || !Pin.All(char.IsAsciiDigit) || Pin == "0000")
        {
            Pin = Random.Shared.Next(1000, 10000).ToString();
        }
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    /// <summary>¿Cambió algo que exige reiniciar el motor?</summary>
    public bool EngineOptionsDiffer(AppSettings other) =>
        ReceiverName != other.ReceiverName ||
        FullscreenOnConnect != other.FullscreenOnConnect ||
        RequirePin != other.RequirePin ||
        (RequirePin && Pin != other.Pin);
}
