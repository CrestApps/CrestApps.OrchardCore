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
    private readonly TimeProvider _timeProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceHandler"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CadenceHandler(
        TimeProvider timeProvider,
        IStringLocalizer<CadenceHandler> stringLocalizer)
    {
        _timeProvider = timeProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<Cadence> context, CancellationToken cancellationToken = default)
    {
        if (context.Model.CreatedUtc == default)
        {
            context.Model.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<Cadence> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _timeProvider.GetUtcNow().UtcDateTime;

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
