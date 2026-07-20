using System.Text.Json;
using PharmacyScheduler.Core.Models;
using PharmacyScheduler.Core.Services;

namespace PharmacyScheduler.WinForms.Infrastructure;

public sealed class AppDataFileStore
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public AppDataFileStore(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public AppData Load()
    {
        if (!File.Exists(FilePath))
        {
            var sample = SampleDataFactory.Create();
            Save(sample);
            return sample;
        }

        var json = File.ReadAllText(FilePath);
        var data = JsonSerializer.Deserialize<AppData>(json, _options);
        return data ?? SampleDataFactory.Create();
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.Serialize(data, _options);
        File.WriteAllText(FilePath, json);
    }
}
