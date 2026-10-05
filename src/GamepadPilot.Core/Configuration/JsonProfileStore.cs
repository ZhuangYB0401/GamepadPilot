using System.Text.Json;
using System.Text.Json.Serialization;
using GamepadPilot.Core.Services;
using GamepadPilot.Core.Domain;

namespace GamepadPilot.Core.Configuration;

public sealed class JsonProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonProfileStore(string? configPath = null)
    {
        ConfigPath = configPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GamepadPilot",
            "config.json");
    }

    public string ConfigPath { get; }

    public AppConfiguration Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return DefaultConfigurationFactory.Create();
        }

        try
        {
            var configuration = JsonSerializer.Deserialize<AppConfiguration>(File.ReadAllText(ConfigPath), Options)
                ?? throw new InvalidDataException("配置文件为空。");
            ConfigurationValidator.Normalize(configuration);
            return configuration;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            BackupCorruptFile();
            return DefaultConfigurationFactory.Create();
        }
    }

    public void Save(AppConfiguration configuration)
    {
        ConfigurationValidator.Normalize(configuration);
        var directory = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = ConfigPath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(configuration, Options));
        File.Move(tempPath, ConfigPath, true);
    }

    public void Export(AppConfiguration configuration, string path)
    {
        ConfigurationValidator.Normalize(configuration);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(configuration, Options));
    }

    public AppConfiguration Import(string path)
    {
        var configuration = JsonSerializer.Deserialize<AppConfiguration>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("导入文件为空。");
        ConfigurationValidator.Normalize(configuration);
        return configuration;
    }

    private void BackupCorruptFile()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(ConfigPath)!;
            var backup = Path.Combine(directory, $"config.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(ConfigPath, backup, true);
        }
        catch
        {
        }
    }
}
