using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
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
        BinaryData parameters = null;

        if (function.Metadata.Parameters != null)
        {
            var arguments = new AzureFunctionParameters()
            {
                Properties = [],
            };

            foreach (var data in function.Metadata.Parameters)
            {
                arguments.Properties.Add(data.Name, new AzureChatFunctionParameterArgument
                {
                    Type = data.ParameterType.Name.ToLowerInvariant(),
                    Description = data.Description,
                    IsRequired = data.IsRequired,
                    DefaultValue = data.DefaultValue,
                    Values = data.ParameterType.IsEnum
                    ? Enum.GetNames(data.ParameterType)
                    : null,
                });
            }

            arguments.Required = arguments.Properties.Where(x => x.Value.IsRequired).Select(x => x.Key);

            parameters = BinaryData.FromObjectAsJson(arguments, _jsonSerializerOptions);
        }

        return ChatTool.CreateFunctionTool(function.Metadata.Name, function.Metadata.Description, parameters);
    }
}
