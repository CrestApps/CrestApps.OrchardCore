using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Tools.Functions;

namespace CrestApps.OrchardCore.OpenAI.Core.Functions;

public sealed class GetWeatherOpenAIFunction : OpenAIChatFunctionBase
{
    public override string Name => "get_current_weather";

    public override string Description => "Get the current weather in a given location.";

    public GetWeatherOpenAIFunction()
    {
        DefineInputProperty(nameof(GetWeatherArguments.Location), new StringFunctionProperty
        {
            Description = "The city and state, e.g. San Francisco, CA.",
            IsRequired = true,
        });

        DefineInputProperty(nameof(GetWeatherArguments.Unit), new EnumToolProperty<TempScale>
        {
            Description = "The temperature scale used by the location",
            IsRequired = false,
        });

        var returnType = new OpenAIChatFunctionType()
        {
            Type = OpenAIChatFunctionPropertyType.Object,
        };

        returnType.Properties[nameof(WeatherResult.Temperature)] = new StringFunctionProperty()
        {
            Description = "The current temperature in the location.",
        };

        returnType.Properties[nameof(WeatherResult.Condition)] = new StringFunctionProperty()
        {
            Description = "The current weather condition in the location.",
        };

        ReturnType = returnType;
    }

    public override Task<object> InvokeAsync(JsonObject arguments)
    {
        var data = arguments.ToObject<GetWeatherArguments>();

        return Task.FromResult<object>(new WeatherResult
        {
            Temperature = 72.5,
            Condition = "Sunny",
        });
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

public class WeatherResult
{
    public double Temperature { get; set; }

    public string Condition { get; set; }
}
