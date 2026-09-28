using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Handlers;

/// <summary>
/// Stamps audit times and validates an <see cref="MessageTemplate"/>.
/// </summary>
internal sealed class MessageTemplateHandler : CatalogEntryHandlerBase<MessageTemplate>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public MessageTemplateHandler(
        IClock clock,
        IStringLocalizer<MessageTemplateHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<MessageTemplate> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<MessageTemplate> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<MessageTemplate> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(MessageTemplate.Name)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.Body))
        {
            context.Result.Fail(new ValidationResult(S["A template body is required."], [nameof(MessageTemplate.Body)]));
        }

        return Task.CompletedTask;
    }
}
