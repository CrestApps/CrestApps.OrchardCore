using CrestApps.OrchardCore.Learning.Credentials.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Credentials.Drivers;

/// <summary>
/// The editor of the form that awards a credential by hand: the credential and the person, and why.
/// </summary>
internal sealed class AwardCredentialDisplayDriver : DisplayDriver<AwardCredentialViewModel>
{
    internal readonly IStringLocalizer S;

    public AwardCredentialDisplayDriver(IStringLocalizer<AwardCredentialDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AwardCredentialViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<AwardCredentialViewModel>("AwardCredentialRecipient_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Credential and Person"], "10")),
            Initialize<AwardCredentialViewModel>("AwardCredentialReason_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Reason"], "20")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AwardCredentialViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.DefinitionId,
            m => m.UserId,
            m => m.Reason);

        return Edit(model, context);
    }

    /// <inheritdoc/>
    protected override void BuildPrefix(AwardCredentialViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(AwardCredentialViewModel source, AwardCredentialViewModel model)
    {
        model.DefinitionId = source.DefinitionId;
        model.UserId = source.UserId;
        model.Reason = source.Reason;
        model.Definitions = source.Definitions;
    }
}
