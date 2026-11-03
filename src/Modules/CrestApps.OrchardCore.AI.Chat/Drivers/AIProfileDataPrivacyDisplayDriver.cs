using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Edits what is kept of an AI profile's conversations, and for how long.
/// </summary>
public sealed class AIProfileDataPrivacyDisplayDriver : DisplayDriver<AIProfile>
{
    private const int MaxRetentionDays = 3650;

    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileDataPrivacyDisplayDriver"/> class.
    /// </summary>
    /// <param name="clock">The clock the change of storage mode is dated with.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileDataPrivacyDisplayDriver(
        IClock clock,
        IStringLocalizer<AIProfileDataPrivacyDisplayDriver> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileDataPrivacyViewModel>("AIProfileDataPrivacy_Edit", model =>
        {
            var settings = profile.GetSettings<AIProfileDataPrivacySettings>();
            model.StorageMode = settings.StorageMode;
            model.RetentionDays = settings.RetentionDays;
            model.NamesToKeep = string.Join(Environment.NewLine, settings.NamesToKeep ?? []);
        }).Location("Content:20#Data Processing & Metrics;12");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileDataPrivacyViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!Enum.IsDefined(model.StorageMode))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.StorageMode), S["Choose what is kept of a conversation."]);
        }

        if (model.RetentionDays is < 1 or > MaxRetentionDays)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RetentionDays), S["Keep conversations between 1 and {0} days, or leave it empty to keep them.", MaxRetentionDays]);
        }

        profile.AlterSettings<AIProfileDataPrivacySettings>(settings =>
        {
            if (settings.StorageMode != model.StorageMode)
            {
                settings.StorageModeSinceUtc = _clock.UtcNow;
            }

            settings.StorageMode = model.StorageMode;
            settings.RetentionDays = model.RetentionDays;
            settings.NamesToKeep = SplitNames(model.NamesToKeep);
        });

        return Edit(profile, context);
    }

    /// <summary>
    /// Splits the names entered one per line or separated by commas, without blanks or repeats.
    /// </summary>
    /// <param name="names">The names as entered.</param>
    /// <returns>The names.</returns>
    internal static List<string> SplitNames(string names)
    {
        if (string.IsNullOrWhiteSpace(names))
        {
            return [];
        }

        return names
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
