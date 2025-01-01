namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class ValidatedOpenAIChatProfileContext : OpenAIChatProfileContextBase
{
    public readonly OpenAIChatProfileValidateResult Result;

    public ValidatedOpenAIChatProfileContext(OpenAIChatProfile profile, OpenAIChatProfileValidateResult result)
        : base(profile)
    {
        Result = result ?? new();
    }
}
