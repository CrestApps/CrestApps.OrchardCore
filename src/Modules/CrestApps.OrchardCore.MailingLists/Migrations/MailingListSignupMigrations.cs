using CrestApps.OrchardCore.MailingLists.SignupWidget;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.MailingLists.Migrations;

/// <summary>
/// Adds the mailing list sign-up part and the <c>MailingListSignup</c> widget type.
/// </summary>
internal sealed class MailingListSignupMigrations : DataMigration
{
    /// <summary>
    /// The widget content type.
    /// </summary>
    public const string WidgetType = "MailingListSignup";

    private readonly IContentDefinitionManager _contentDefinitionManager;

    public MailingListSignupMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(MailingListSignupPart), part => part
            .Attachable()
            .WithDisplayName("Mailing List Sign-up")
            .WithDescription("A newsletter sign-up form that adds visitors to one or more mailing lists."));

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(WidgetType) is null)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(WidgetType, type => type
                .WithDisplayName("Mailing List Sign-up")
                .Stereotype("Widget")
                .WithPart(nameof(MailingListSignupPart)));
        }

        return 1;
    }
}
