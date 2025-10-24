using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

public sealed class ContactMethodMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ContactMethodMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.EmailInfo, part => part
            .Attachable()
            .Reusable()
            .WithDisplayName("Email Info Part")
            .WithDescription("Provides a way to capture a required email address")
            .WithField("Email", field => field
                .WithPosition("1")
                .OfType("TextField")
                .WithDisplayName("Email")
                .WithEditor("Email")
                .WithSettings(new TextFieldSettings()
                {
                    Required = true,
                })
            )
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.PhoneNumberInfo, part => part
            .Attachable()
            .Reusable()
            .WithDisplayName("Phone Number Info Part")
            .WithDescription("Provides a way to capture required phone number info")
            .WithField("Number", field => field
                .WithPosition("1")
                .OfType("TextField")
                .WithDisplayName("Number")
                .WithEditor("masked")
                .WithSettings(new TextFieldSettings()
                {
                    Required = true,
                }))
            .WithField("Extension", field => field
                .WithPosition("2")
                .OfType("TextField")
                .WithDisplayName("Extension")
                )
            .WithField("Type", field => field
                .WithPosition("3")
                .OfType("TextField")
                .WithDisplayName("Type")
                .WithEditor("PredefinedList")
                .MergeSettings<TextFieldPredefinedListEditorSettings>(settings =>
                {
                    settings.Editor = EditorOption.Dropdown;
                    settings.DefaultValue = string.Empty;
                    settings.Options =
                    [
                        new ListValueOption()
                        {
                            Name = "Home",
                            Value = "Home",
                        },
                        new ListValueOption()
                        {
                            Name = "Cell",
                            Value = "Cell",
                        },
                        new ListValueOption()
                        {
                            Name = "Fax",
                            Value = "Fax",
                        },
                        new ListValueOption()
                        {
                            Name = "Work",
                            Value = "Work",
                        },
                        new ListValueOption()
                        {
                            Name = "Office",
                            Value = "Office",
                        },
                        new ListValueOption()
                        {
                            Name = "Other",
                            Value = "Other",
                        }
                    ];
                })
            )
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync(OmnichannelConstants.ContentTypes.EmailAddress, type => type
            .Creatable()
            .Stereotype(OmnichannelConstants.Sterotypes.ContactMethod)
            .DisplayedAs("Email Address")
            .WithPart("TitlePart", part => part
                .WithPosition("1")
                .WithSettings(new TitlePartSettings()
                {
                    Options = TitlePartOptions.GeneratedHidden,
                    Pattern = "{{ Model.ContentItem.Content." + OmnichannelConstants.ContentParts.EmailInfo + ".Email.Text }}",
                })
            )
            .WithPart(OmnichannelConstants.ContentParts.EmailInfo, part =>
                part.WithPosition("5")
            )
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync(OmnichannelConstants.ContentTypes.PhoneNumber, type => type
            .DisplayedAs("Phone Number")
            .Creatable()
            .Stereotype(OmnichannelConstants.Sterotypes.ContactMethod)
            .WithPart<TitlePart>(part => part
                .WithPosition("1")
                .WithSettings(new TitlePartSettings()
                {
                    Options = TitlePartOptions.GeneratedHidden,
                    Pattern = "{{ Model.ContentItem.Content." + OmnichannelConstants.ContentParts.PhoneNumberInfo + ".Type.Text | append: ': ' | append: Model.ContentItem.Content." + OmnichannelConstants.ContentParts.PhoneNumberInfo + ".Number.Text }}",
                })
            )
            .WithPart(OmnichannelConstants.ContentParts.PhoneNumberInfo, part => part.WithPosition("5"))
        );

        return 1;
    }
}
