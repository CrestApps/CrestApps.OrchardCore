using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Functions;

namespace CrestApps.OrchardCore.OpenAI.Core.Functions;

public sealed class GetWeatherFunction : OpenAIChatFunctionBase
{
    public override string Name => "get_current_weather";

    public override string Description => "Get the current weather in a given location.";

    public GetWeatherFunction()
    {
        DefineProperty("location", new StringFunctionProperty
        {
            Description = "The city and state, e.g. San Francisco, CA.",
            IsRequired = true,
        });

        DefineProperty("unit", new EnumFunctionProperty<TempScale>
        {
            Description = "The temperature scale used by the location",
            IsRequired = false,
        });
    }

    public override Task<string> InvokeAsync(JsonObject arguments)
    {
        var value = arguments.ToObject<GetWeatherArguments>();

        // Typically you would call another service here to get the actual temperature.
        // For simplicity, we we will return a static value.

        return Task.FromResult("Temperature: 80F, Condition: Sunny");
    }
}

public enum TempScale
{
    Fahrenheit,
    Celsius,
}

public class GetWeatherArguments
{
    public string Location { get; set; }

    public TempScale Unit { get; set; }
}
