using System.Text.Json;

namespace CrestApps;

public static class JSOptions
{
    public readonly static JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public readonly static JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
    };
}
