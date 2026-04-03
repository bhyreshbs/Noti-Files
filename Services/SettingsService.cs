using System;
using System.IO;
using System.Text.Json;
using FileActivityNotifier.Models;

namespace FileActivityNotifier.Services;

public class SettingsService
{
    private readonly object _sync = new();

    public AppSettings Current { get; private set; } = AppSettings.CreateDefaults();

    public string SettingsFilePath => GetSettingsFilePath();

    public AppSettings Load()
    {
        lock (_sync)
        {
            var path = SettingsFilePath;

            if (!File.Exists(path))
            {
                Current = AppSettings.CreateDefaults();
                Save(Current);
                return Current;
            }

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.CreateDefaults();
                loaded.Normalize();

                if (!loaded.Validate())
                {
                    loaded = AppSettings.CreateDefaults();
                    loaded.Normalize();
                }

                Current = loaded;
                return Current;
            }
            catch
            {
                Current = AppSettings.CreateDefaults();
                Save(Current);
                return Current;
            }
        }
    }

    public void Save()
    {
        Save(Current);
    }

    public void Save(AppSettings settings)
    {
        lock (_sync)
        {
            settings.Normalize();
            if (!settings.Validate())
            {
                settings = AppSettings.CreateDefaults();
                settings.Normalize();
            }

            var path = SettingsFilePath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            Current = settings;
        }
    }

    private static string GetSettingsFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var notiFilesFolder = Path.Combine(appDataPath, "Noti-Files");
        return Path.Combine(notiFilesFolder, "settings.json");
    }
}
