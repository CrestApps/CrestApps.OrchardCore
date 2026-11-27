using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// The editor of a Skills workflow event.
/// </summary>
/// <typeparam name="TActivity">The event type.</typeparam>
public sealed class SkillsEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, SkillsEventActivityViewModel>
    where TActivity : SkillsEventActivity
{
    private readonly ICertificationManager _certificationManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillsEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="certificationManager">The certification manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SkillsEventActivityDisplayDriver(ICertificationManager certificationManager, IStringLocalizer<SkillsEventActivityDisplayDriver<TActivity>> stringLocalizer)
    {
        _certificationManager = certificationManager;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
        => Initialize<SkillsEventActivityViewModel>($"{typeof(TActivity).Name}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Event"], "10"));

    /// <inheritdoc />
    protected override async ValueTask EditActivityAsync(TActivity activity, SkillsEventActivityViewModel model)
    {
        model.CertificationIds = activity.CertificationIds.ToArray();
        model.HasCertificationFilter = activity is not SkillsChangedEvent;
        model.CertificationOptions = (await _certificationManager.GetEnabledAsync())
            .Select(certification => new SelectListItem(certification.Name, certification.ItemId, model.CertificationIds.Contains(certification.ItemId)))
            .ToList();
    }

    /// <inheritdoc />
    protected override void UpdateActivity(SkillsEventActivityViewModel model, TActivity activity)
    {
        activity.CertificationIds = (model.CertificationIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
    }
}
