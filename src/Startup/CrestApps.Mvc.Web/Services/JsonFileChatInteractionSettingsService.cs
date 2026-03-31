using System.Text.Json;
using CrestApps.Mvc.Web.Models;

namespace CrestApps.Mvc.Web.Services;

/// <summary>
/// Manages persistence of <see cref="ChatInteractionSettings"/> in a JSON file stored under App_Data.
/// </summary>
public sealed class JsonFileChatInteractionSettingsService
{
    public const string SectionKey = "ChatInteraction";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    public JsonFileChatInteractionSettingsService(string appDataPath)
    {
        _filePath = Path.Combine(appDataPath, "ai-chat-interaction-settings.json");
    }

    public string FilePath => _filePath;

    public async Task<ChatInteractionSettings> GetAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new ChatInteractionSettings();
        }

        var json = await File.ReadAllTextAsync(_filePath);
        var wrapper = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);

        if (TryGetSection(wrapper, out var section))
        {
            return section.Deserialize<ChatInteractionSettings>(_jsonOptions) ?? new ChatInteractionSettings();
        }

        return new ChatInteractionSettings();
    }

    public async Task SaveAsync(ChatInteractionSettings settings)
    {
        var sectionKey = JsonNamingPolicy.CamelCase.ConvertName(SectionKey);
        var wrapper = new Dictionary<string, ChatInteractionSettings>
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
