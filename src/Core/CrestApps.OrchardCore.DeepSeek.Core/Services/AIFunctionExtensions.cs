using CrestApps.OrchardCore.DeepSeek.Core.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.DeepSeek.Services;

internal static class AIFunctionExtensions
{
    public static DeepSeekChatTool ToChatTool(this AIFunction function)
    {
        var tool = new DeepSeekChatTool();

        if (function.Metadata.Parameters != null)
        {
            var parameters = new DeepSeekChatFunctionParameters()
            {
                Properties = [],
            };

            foreach (var data in function.Metadata.Parameters)
            {
                parameters.Properties.Add(data.Name, new DeepSeekChatFunctionParameterArgument
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

            parameters.Required = parameters.Properties.Where(x => x.Value.IsRequired).Select(x => x.Key);

            tool.Function = new DeepSeekChatFunction
            {
                Name = function.Metadata.Name,
                Description = function.Metadata.Description,
                Parameters = parameters,
            };
        }

        return tool;
    }
}
