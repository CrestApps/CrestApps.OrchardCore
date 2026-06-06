using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentFields.Settings;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Title.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Defines database migrations for the Migrations module.
/// </summary>
public sealed class ContactMethodMigrations : DataMigration
{
    private const int _batchSize = 100;
    private const string _defaultRegionCode = "US";

    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactMethodMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public ContactMethodMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
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
                .OfType("PhoneField")
                .WithDisplayName("Number")
                .WithSettings(new PhoneFieldSettings()
                {
                    Required = true,
                })
            )
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
            .WithDisplayName("Email Address")
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
            .WithDisplayName("Phone Number")
            .Creatable()
            .Stereotype(OmnichannelConstants.Sterotypes.ContactMethod)
            .WithPart<TitlePart>(part => part
                .WithPosition("1")
                .WithSettings(new TitlePartSettings()
                {
                    Options = TitlePartOptions.GeneratedHidden,
                    Pattern = "{{ Model.ContentItem.Content." + OmnichannelConstants.ContentParts.PhoneNumberInfo + ".Type.Text | append: ': ' | append: Model.ContentItem.Content." + OmnichannelConstants.ContentParts.PhoneNumberInfo + ".Number.PhoneNumber }}",
                })
            )
            .WithPart(OmnichannelConstants.ContentParts.PhoneNumberInfo, part => part.WithPosition("5"))
        );

        return 2;
    }

    /// <summary>
    /// Migrates the Number field from TextField to PhoneField and schedules background
    /// data migration for existing phone number records.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.PhoneNumberInfo, part => part
            .RemoveField("Number"));

        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.PhoneNumberInfo, part => part
            .WithField("Number", field => field
                .OfType("PhoneField")
                .WithDisplayName("Number")
                .WithPosition("1")
                .WithSettings(new PhoneFieldSettings
                {
                    Required = true,
                })));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(OmnichannelConstants.ContentTypes.PhoneNumber, type => type
            .WithPart<TitlePart>(part => part
                .WithPosition("1")
                .WithSettings(new TitlePartSettings()
                {
                    Options = TitlePartOptions.GeneratedHidden,
                    Pattern = "{{ Model.ContentItem.Content." + OmnichannelConstants.ContentParts.PhoneNumberInfo + ".Type.Text | append: ': ' | append: Model.ContentItem.Content." + OmnichannelConstants.ContentParts.PhoneNumberInfo + ".Number.PhoneNumber }}",
                })));

        ShellScope.AddDeferredTask(MigratePhoneNumberDataAsync);

        return 2;
    }

    private static async Task MigratePhoneNumberDataAsync(ShellScope scope)
    {
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        var phoneNumberService = scope.ServiceProvider.GetRequiredService<IPhoneNumberService>();
        var contentDefinitionManager = scope.ServiceProvider.GetRequiredService<IContentDefinitionManager>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ContactMethodMigrations>>();

        var contentTypes = await GetContentTypesWithPhoneNumberInfoPartAsync(contentDefinitionManager);

        if (contentTypes.Length == 0)
        {
            return;
        }

        var migratedCount = 0;
        var skippedCount = 0;
        var page = 0;

        while (true)
        {
            List<ContentItem> batch;

            await using (var session = store.CreateSession())
            {
                batch = (await session.Query<ContentItem, ContentItemIndex>(index =>
                    index.ContentType.IsIn(contentTypes))
                    .OrderBy(index => index.DocumentId)
                    .Skip(page * _batchSize)
                    .Take(_batchSize)
                    .ListAsync())
                    .ToList();
            }

            if (batch.Count == 0)
            {
                break;
            }

            var batchMigrated = 0;

            await using (var session = store.CreateSession())
            {
                foreach (var contentItem in batch)
                {
                    try
                    {
                        if (!TryMigratePhoneNumberContent(contentItem, phoneNumberService))
                        {
                            skippedCount++;

                            continue;
                        }

                        await session.SaveAsync(contentItem);
                        batchMigrated++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to migrate phone number for content item '{ContentItemId}' (version '{ContentItemVersionId}').",
                            contentItem.ContentItemId,
                            contentItem.ContentItemVersionId);
                        skippedCount++;
                    }
                }

                await session.SaveChangesAsync();
            }

            migratedCount += batchMigrated;
            page++;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Phone number data migration completed. Migrated: {MigratedCount}, Skipped: {SkippedCount}.",
                migratedCount,
                skippedCount);
        }
    }

    private static async Task<string[]> GetContentTypesWithPhoneNumberInfoPartAsync(IContentDefinitionManager contentDefinitionManager)
    {
        var typeDefinitions = await contentDefinitionManager.ListTypeDefinitionsAsync();

        return typeDefinitions
            .Where(type => type.Parts.Any(part =>
                string.Equals(part.PartDefinition.Name, OmnichannelConstants.ContentParts.PhoneNumberInfo, StringComparison.Ordinal)))
            .Select(type => type.Name)
            .ToArray();
    }

    private static bool TryMigratePhoneNumberContent(ContentItem contentItem, IPhoneNumberService phoneNumberService)
    {
        var partNode = contentItem.Content[OmnichannelConstants.ContentParts.PhoneNumberInfo] as JsonObject;

        if (partNode is null)
        {
            return false;
        }

        var numberNode = partNode["Number"] as JsonObject;

        if (numberNode is null)
        {
            return false;
        }

        // Already migrated if PhoneNumber property exists.
        if (numberNode["PhoneNumber"] is not null)
        {
            return false;
        }

        var textValue = numberNode["Text"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(textValue))
        {
            return false;
        }

        if (!phoneNumberService.IsValidNumber(textValue, _defaultRegionCode))
        {
            return false;
        }

        if (!phoneNumberService.TryFormatToE164(textValue, _defaultRegionCode, out var e164Number))
        {
            return false;
        }

        var regionCode = phoneNumberService.GetRegionCode(e164Number) ?? _defaultRegionCode;
        var countryCode = phoneNumberService.GetCountryCode(regionCode);
        var nationalNumber = e164Number;

        // Strip the leading '+' and country calling code to get the national number.
        if (countryCode > 0)
        {
            var prefix = $"+{countryCode}";

            if (nationalNumber.StartsWith(prefix, StringComparison.Ordinal))
            {
                nationalNumber = nationalNumber.Substring(prefix.Length);
            }
        }

        // Replace the old TextField structure with PhoneField properties.
        numberNode.Remove("Text");
        numberNode["PhoneNumber"] = e164Number;
        numberNode["CountryCode"] = regionCode;
        numberNode["NationalNumber"] = nationalNumber;

        return true;
    }
}
