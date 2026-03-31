using System.Text.Json;
using CrestApps.Mvc.Web.Models;

namespace CrestApps.Mvc.Web.Services;

public sealed class JsonFilePaginationSettingsService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    public JsonFilePaginationSettingsService(string appDataPath)
    {
        _filePath = Path.Combine(appDataPath, "pagination-settings.json");
    }

    public async Task<PaginationSettings> GetAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new PaginationSettings();
        }

        var json = await File.ReadAllTextAsync(_filePath);

        return JsonSerializer.Deserialize<PaginationSettings>(json, _jsonOptions) ?? new PaginationSettings();
    }

    public async Task SaveAsync(PaginationSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);

        await File.WriteAllTextAsync(_filePath, json);
    }
}
