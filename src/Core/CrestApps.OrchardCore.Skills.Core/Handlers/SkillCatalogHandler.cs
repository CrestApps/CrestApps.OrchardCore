using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Skills.Core.Handlers;

/// <summary>
/// Stamps skills, trims their names and keeps the names unique whatever their case.
/// </summary>
internal sealed class SkillCatalogHandler : CatalogEntryHandlerBase<Skill>
{
    private readonly SkillStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public SkillCatalogHandler(
        SkillStore store,
        IClock clock,
        IStringLocalizer<SkillCatalogHandler> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<Skill> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task CreatingAsync(CreatingContext<Skill> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<Skill> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidatingContext<Skill> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);

        if (string.IsNullOrEmpty(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(Skill.Name)]));

            return;
        }

        var existing = await _store.FindByNameAsync(context.Model.Name, cancellationToken);

        if (existing is not null && existing.ItemId != context.Model.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["Another skill is already named '{0}'.", context.Model.Name], [nameof(Skill.Name)]));
        }
    }

    private static void Tidy(Skill skill)
    {
        skill.Name = skill.Name?.Trim();
        skill.Category = string.IsNullOrWhiteSpace(skill.Category) ? null : skill.Category.Trim();
        skill.RequiredCertificationId = string.IsNullOrWhiteSpace(skill.RequiredCertificationId) ? null : skill.RequiredCertificationId;
    }
}
