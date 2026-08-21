using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Sms.Handlers;

/// <summary>
/// Stamps audit times and validates an <see cref="SmsNumberRoute"/>.
/// </summary>
internal sealed class SmsNumberRouteHandler : CatalogEntryHandlerBase<SmsNumberRoute>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNumberRouteHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock used to stamp audit times.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SmsNumberRouteHandler(
        IClock clock,
        IStringLocalizer<SmsNumberRouteHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<SmsNumberRoute> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<SmsNumberRoute> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<SmsNumberRoute> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(SmsNumberRoute.Name)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.DialedNumber))
        {
            context.Result.Fail(new ValidationResult(S["A dialed number is required."], [nameof(SmsNumberRoute.DialedNumber)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.TargetId))
        {
            context.Result.Fail(new ValidationResult(S["A target is required."], [nameof(SmsNumberRoute.TargetId)]));
        }

        return Task.CompletedTask;
    }
}
