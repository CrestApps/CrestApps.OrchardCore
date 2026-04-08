namespace CrestApps.Core.AI.Models;

public sealed class DefaultAIOptions
{
    public float? Temperature { get; set; } = 0;

    public int? MaxOutputTokens { get; set; }

    public float? TopP { get; set; } = 1;

    public float? FrequencyPenalty { get; set; } = 0;

    public float? PresencePenalty { get; set; } = 0;

    public int PastMessagesCount { get; set; } = 10;

    public int MaximumIterationsPerRequest { get; set; } = 10;

    public int AbsoluteMaximumIterationsPerRequest { get; set; } = 100;

    public bool EnableOpenTelemetry { get; set; }

    public bool EnableDistributedCaching { get; set; } = true;

    public DefaultAIOptions Normalize()
    {
        if (AbsoluteMaximumIterationsPerRequest <= 0)
        {
            AbsoluteMaximumIterationsPerRequest = 100;
        }

        if (MaximumIterationsPerRequest <= 0)
        {
            MaximumIterationsPerRequest = 10;
        }

        MaximumIterationsPerRequest = Math.Min(MaximumIterationsPerRequest, AbsoluteMaximumIterationsPerRequest);

        return this;
    }

    public DefaultAIOptions ApplySiteOverrides(GeneralAIOptions settings)
    {
        var options = new DefaultAIOptions
        {
            Temperature = Temperature,
            MaxOutputTokens = MaxOutputTokens,
            TopP = TopP,
            FrequencyPenalty = FrequencyPenalty,
            PresencePenalty = PresencePenalty,
            PastMessagesCount = PastMessagesCount,
            MaximumIterationsPerRequest = MaximumIterationsPerRequest,
            AbsoluteMaximumIterationsPerRequest = AbsoluteMaximumIterationsPerRequest,
            EnableOpenTelemetry = EnableOpenTelemetry,
            EnableDistributedCaching = EnableDistributedCaching,
        }.Normalize();

        if (settings is null)
        {
            return options;
        }

        if (settings.OverrideMaximumIterationsPerRequest)
        {
            options.MaximumIterationsPerRequest = settings.MaximumIterationsPerRequest;
        }

        if (settings.OverrideEnableDistributedCaching)
        {
            options.EnableDistributedCaching = settings.EnableDistributedCaching;
        }

        if (settings.OverrideEnableOpenTelemetry)
        {
            options.EnableOpenTelemetry = settings.EnableOpenTelemetry;
        }

        return options.Normalize();
    }
}
