using System.Text.Json;
using MozzartPrintHub.WinForms.Configuration;

namespace MozzartPrintHub.WinForms.Services;

public sealed class AppSettingsService
{
    private readonly string _path;

    public AppSettingsService(string path)
    {
        _path = path;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        var json = File.ReadAllText(_path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }

        var model = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions());
        return model ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions());
        File.WriteAllText(_path, json);
    }

    private static JsonSerializerOptions SerializerOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
}
