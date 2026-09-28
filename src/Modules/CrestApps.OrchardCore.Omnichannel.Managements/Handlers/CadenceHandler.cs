using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Stamps created/modified timestamps on <see cref="Cadence"/> catalog entries and enforces their storage rules on
/// every write path (editor, recipe, deployment, or a service that writes through the manager).
/// </summary>
internal sealed class CadenceHandler : CatalogEntryHandlerBase<Cadence>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CadenceHandler(
        IClock clock,
        IStringLocalizer<CadenceHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<Cadence> context, CancellationToken cancellationToken = default)
    {
        if (context.Model.CreatedUtc == default)
        {
            context.Model.CreatedUtc = _clock.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<Cadence> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<Cadence> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["A name is required."], [nameof(Cadence.DisplayText)]));
        }

        // A defined-message step must carry the verbiage to send; an AI step composes its own, so its message is optional.
        if (context.Model.Steps is { Count: > 0 } &&
            context.Model.Steps.Any(step => step is not null && !step.IsAiGenerated && string.IsNullOrWhiteSpace(step.Message)))
        {
            context.Result.Fail(new ValidationResult(S["Each defined-message step needs its message text."], [nameof(Cadence.Steps)]));
        }

        return Task.CompletedTask;
    }
}
