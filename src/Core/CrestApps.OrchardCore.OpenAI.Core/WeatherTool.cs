using System.ComponentModel;
using CrestApps.OrchardCore.OpenAI.Tools;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.OpenAI.Core;

public sealed class WeatherTool : AIFunction, IOpenAIChatToolDescriptor
{
    public string Name => "get_weather";

    public string Description => "Gets the weather!";

    public AITool Tool => this;

    public override AIFunctionMetadata Metadata { get; }

    public WeatherTool()
    {
        Metadata = new AIFunctionMetadata(Name)
        {
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
        return Task.FromResult<object>(GetWeather());
    }

    //AIFunctionFactory.Create(GetWeather);


    [Description("Gets the weather")]
    static string GetWeather() => Random.Shared.NextDouble() > 0.5 ? "It's sunny" : "It's raining";
}
