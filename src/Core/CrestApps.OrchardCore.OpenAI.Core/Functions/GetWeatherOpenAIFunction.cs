using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Tools.Functions;

namespace CrestApps.OrchardCore.OpenAI.Core.Functions;

public sealed class GetWeatherOpenAIFunction : OpenAIChatFunctionBase
{
    public override string Name => "get_current_weather";

    public override string Description => "Get the current weather in a given location.";

    public GetWeatherOpenAIFunction()
    {
        DefineProperty("location", new StringFunctionProperty
        {
            Description = "The city and state, e.g. San Francisco, CA.",
            IsRequired = true,
        });

        DefineProperty("unit", new EnumToolProperty<TempScale>
        {
            Description = "The temperature scale used by the location",
            IsRequired = false,
        });

        DefineProperty("date", StringFunctionProperty.DateTime("Current Date"));

        DefineProperty("amount", NumberFunctionProperty.Float("Price amount"));
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
