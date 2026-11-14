using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Liquid;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Handlers;

/// <summary>
/// Binds imported values onto, stamps audit times on, and validates an <see cref="MessageTemplate"/>.
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
    public override Task InitializingAsync(InitializingContext<MessageTemplate> context, CancellationToken cancellationToken = default)
    {
        // A recipe or deployment plan hands the entry's JSON to the manager; the editor hands none.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
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
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<MessageTemplate> context, CancellationToken cancellationToken = default)
    {
        var template = context.Model;

        template.Scope = string.IsNullOrWhiteSpace(template.Scope) ? null : template.Scope.Trim();

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(MessageTemplate.Name)]));
        }

        if (string.IsNullOrWhiteSpace(template.Body))
        {
            context.Result.Fail(new ValidationResult(S["A template body is required."], [nameof(MessageTemplate.Body)]));
        }
        else if (template.FillsPlaceholders && !RestrictedLiquid.TryValidate(template.Body, out var error))
        {
            // A body that doesn't parse would put the parser's message in every agent's composer.
            context.Result.Fail(new ValidationResult(S["The placeholders can't be read: {0}", error], [nameof(MessageTemplate.Body)]));
        }

        // Any scope is kept, also one this site doesn't register (a recipe from a site with Tickets, say), within the
        // length its index column holds.
        if (template.Scope?.Length > MessagingStorage.TemplateScopeLength)
        {
            context.Result.Fail(new ValidationResult(S["The scope can be at most {0} characters long.", MessagingStorage.TemplateScopeLength], [nameof(MessageTemplate.Scope)]));
        }

        return Task.CompletedTask;
    }
}
