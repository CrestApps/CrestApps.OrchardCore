using CrestApps.OrchardCore.Learning.Credentials.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Credentials.Drivers;

/// <summary>
/// The editor of the form that revokes an award: the credential and the recipient, and why it is revoked.
/// </summary>
internal sealed class RevokeCredentialDisplayDriver : DisplayDriver<RevokeCredentialViewModel>
{
    internal readonly IStringLocalizer S;

    public RevokeCredentialDisplayDriver(IStringLocalizer<RevokeCredentialDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(RevokeCredentialViewModel model, BuildEditorContext context)
    {
        return Initialize<RevokeCredentialViewModel>("RevokeCredential_Edit", m => Populate(model, m))
            .Location(l => l.Zone("Content", "1").Card(S["Revocation"], "10"));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(RevokeCredentialViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Reason);

        return Edit(model, context);
    }

    /// <inheritdoc/>
    protected override void BuildPrefix(RevokeCredentialViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(RevokeCredentialViewModel source, RevokeCredentialViewModel model)
    {
        model.Reason = source.Reason;
        model.AwardId = source.AwardId;
        model.AchievementName = source.AchievementName;
        model.RecipientName = source.RecipientName;
        model.AlreadyRevoked = source.AlreadyRevoked;
    }
}
