using System.Text.Json;
using CrestApps.AI.Models;

namespace CrestApps.Mvc.Web.Services;

/// <summary>
/// Manages persistence of <see cref="DefaultAIDeploymentSettings"/> in a JSON file
/// stored under App_Data. The file is also registered with the ASP.NET Core
/// configuration system (<c>reloadOnChange: true</c>) so that
/// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{T}"/> reflects
/// changes automatically after a save.
/// </summary>
public sealed class JsonFileDeploymentDefaultsService
{
    /// <summary>
    /// The configuration section key used to bind settings from the JSON file.
    /// </summary>
    public const string SectionKey = "DefaultDeployments";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    public JsonFileDeploymentDefaultsService(string appDataPath)
    {
        _filePath = Path.Combine(appDataPath, "ai-deployment-defaults.json");
    }

    /// <summary>
    /// Gets the full file path used for storage.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Reads the current deployment defaults from disk.
    /// Returns a new instance with empty values when the file does not exist.
    /// </summary>
    public async Task<DefaultAIDeploymentSettings> GetAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new DefaultAIDeploymentSettings();
        }

        var json = await File.ReadAllTextAsync(_filePath);

        var wrapper = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);

        if (TryGetSection(wrapper, out var section))
        {
            return section.Deserialize<DefaultAIDeploymentSettings>(_jsonOptions) ?? new DefaultAIDeploymentSettings();
        }

        return new DefaultAIDeploymentSettings();
    }

    /// <summary>
    /// Persists the deployment defaults to disk. The configuration system
    /// picks up the change automatically via <c>reloadOnChange</c>.
    /// </summary>
    public async Task SaveAsync(DefaultAIDeploymentSettings settings)
    {
        var sectionKey = JsonNamingPolicy.CamelCase.ConvertName(SectionKey);
        var wrapper = new Dictionary<string, DefaultAIDeploymentSettings>
        {
            [sectionKey] = settings,
        };

        var json = JsonSerializer.Serialize(wrapper, _jsonOptions);

        await File.WriteAllTextAsync(_filePath, json);
    }

    private static bool TryGetSection(JsonElement wrapper, out JsonElement section)
    {
        if (wrapper.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in wrapper.EnumerateObject())
            {
                if (string.Equals(property.Name, SectionKey, StringComparison.OrdinalIgnoreCase))
                {
                    section = property.Value;
                    return true;
                }
            }
        }

        section = default;
        return false;
    }
}
