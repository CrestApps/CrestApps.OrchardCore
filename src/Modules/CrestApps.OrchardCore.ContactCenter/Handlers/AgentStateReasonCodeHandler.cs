using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class AgentStateReasonCodeHandler : CatalogEntryHandlerBase<AgentStateReasonCode>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock used to stamp audit times.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentStateReasonCodeHandler(
        IClock clock,
        IStringLocalizer<AgentStateReasonCodeHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<AgentStateReasonCode> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return PopulateAsync(context.Model, context.Data);
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<AgentStateReasonCode> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return PopulateAsync(context.Model, context.Data);
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<AgentStateReasonCode> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(AgentStateReasonCode.Name)]));
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AgentStateReasonCode model, JsonNode data)
    {
        var name = data[nameof(AgentStateReasonCode.Name)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(name))
        {
            model.Name = name;
        }

        var description = data[nameof(AgentStateReasonCode.Description)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(description))
        {
            model.Description = description;
        }

        var appliesTo = data[nameof(AgentStateReasonCode.AppliesTo)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(appliesTo) && Enum.TryParse<AgentPresenceStatus>(appliesTo, ignoreCase: true, out var status))
        {
            model.AppliesTo = status;
        }

        var sortOrder = data[nameof(AgentStateReasonCode.SortOrder)]?.GetValue<int?>();

        if (sortOrder.HasValue)
        {
            model.SortOrder = sortOrder.Value;
        }

        var enabled = data[nameof(AgentStateReasonCode.Enabled)]?.GetValue<bool?>();

        if (enabled.HasValue)
        {
            model.Enabled = enabled.Value;
        }

        return Task.CompletedTask;
    }
}
