using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace CrestApps.OrchardCore.OpenAI.Services;

internal static class AIFunctionExtensions
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ChatTool ToChatTool(this AIFunction function)
    {
        var parameters = BinaryData.FromObjectAsJson(function.JsonSchema, _jsonSerializerOptions);

        return ChatTool.CreateFunctionTool(function.Name, function.Description, parameters);
    }
}
