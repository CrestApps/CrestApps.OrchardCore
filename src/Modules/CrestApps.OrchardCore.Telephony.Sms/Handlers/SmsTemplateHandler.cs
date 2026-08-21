using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Sms.Handlers;

/// <summary>
/// Stamps audit times and validates an <see cref="SmsTemplate"/>.
/// </summary>
internal sealed class SmsTemplateHandler : CatalogEntryHandlerBase<SmsTemplate>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public SmsTemplateHandler(
        IClock clock,
        IStringLocalizer<SmsTemplateHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<SmsTemplate> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<SmsTemplate> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<SmsTemplate> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(SmsTemplate.Name)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.Body))
        {
            context.Result.Fail(new ValidationResult(S["A template body is required."], [nameof(SmsTemplate.Body)]));
        }

        return Task.CompletedTask;
    }
}
