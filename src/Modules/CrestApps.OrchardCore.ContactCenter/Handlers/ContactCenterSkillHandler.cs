using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class ContactCenterSkillHandler : CatalogEntryHandlerBase<ContactCenterSkill>
{
    private readonly TimeProvider _timeProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillHandler"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider used to stamp audit times.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterSkillHandler(
        TimeProvider timeProvider,
        IStringLocalizer<ContactCenterSkillHandler> stringLocalizer)
    {
        _timeProvider = timeProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<ContactCenterSkill> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<ContactCenterSkill> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<ContactCenterSkill> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        context.Model.ModifiedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<ContactCenterSkill> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(ContactCenterSkill.Name)]));
        }

        return Task.CompletedTask;
    }
}
