using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class DialerProfileHandler : CatalogEntryHandlerBase<DialerProfile>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock used to stamp audit times.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerProfileHandler(
        IClock clock,
        IStringLocalizer<DialerProfileHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<DialerProfile> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<DialerProfile> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<DialerProfile> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(DialerProfile.Name)]));
        }

        return Task.CompletedTask;
    }
}
