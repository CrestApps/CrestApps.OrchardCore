using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Handlers;

/// <summary>
/// Stamps audit times and validates an <see cref="SmsTemplate"/>.
/// </summary>
internal sealed class SmsTemplateHandler : CatalogEntryHandlerBase<SmsTemplate>
{
    private readonly TimeProvider _timeProvider;

    internal readonly IStringLocalizer S;

    public SmsTemplateHandler(
        TimeProvider timeProvider,
        IStringLocalizer<SmsTemplateHandler> stringLocalizer)
    {
        _timeProvider = timeProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<SmsTemplate> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<SmsTemplate> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _timeProvider.GetUtcNow().UtcDateTime;

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
