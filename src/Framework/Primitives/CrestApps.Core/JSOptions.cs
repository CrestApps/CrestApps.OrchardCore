using System.Text.Json;

namespace CrestApps.Core;

public static class JSOptions
{
    public readonly static JsonSerializerOptions Default = new();

    public readonly static JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public readonly static JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
    };
}
