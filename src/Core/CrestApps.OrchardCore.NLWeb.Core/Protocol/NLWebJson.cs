using System.Text.Encodings.Web;
using System.Text.Json;

namespace CrestApps.OrchardCore.NLWeb.Protocol;

/// <summary>
/// The JSON options of NLWeb responses.
/// </summary>
public static class NLWebJson
{
    /// <summary>
    /// Gets the options NLWeb responses are written with. Responses are served as <c>application/json</c> or
    /// <c>text/event-stream</c>, never inside HTML, so text such as accented letters and quotes is written as it is
    /// instead of as <c>\u</c> escapes.
    /// </summary>
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
