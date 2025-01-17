using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.OpenAI.Core;

public sealed class GetWeatherFunction : AIFunction
{
    public override AIFunctionMetadata Metadata { get; }

    public GetWeatherFunction()
    {
        Metadata = new AIFunctionMetadata("get_weather")
        {
            Description = "Gets the weather",
            Parameters =
            [
                new AIFunctionParameterMetadata("location")
                {
                    Description = "The location to get the weather for",
                    IsRequired = true,
                    ParameterType = typeof(string),
                },
            ],
            ReturnParameter = new AIFunctionReturnParameterMetadata
            {
                Description = "The weather",
                ParameterType = typeof(string),
            },
        };
    }
    protected override Task<object> InvokeCoreAsync(IEnumerable<KeyValuePair<string, object>> arguments, CancellationToken cancellationToken)
    {
        // Here you can access the arguments that were defined in Metadata.Parameters above.
        return Task.FromResult<object>(Random.Shared.NextDouble() > 0.5 ? "It's sunny" : "It's raining");
    }
}
