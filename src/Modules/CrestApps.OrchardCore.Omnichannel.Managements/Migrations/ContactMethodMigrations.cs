using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentFields.Settings;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundJobs;
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
                    InitialCountryMode = InitialCountryMode.CurrentCulture,
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

        return 3;
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
                    InitialCountryMode = InitialCountryMode.CurrentCulture,
                })));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(OmnichannelConstants.ContentTypes.PhoneNumber, type => type
            .WithPart<TitlePart>(part => part
                .WithPosition("1")
                .WithSettings(new TitlePartSettings()
                {
                    Options = TitlePartOptions.GeneratedHidden,
                    Pattern = "{{ Model.ContentItem.Content." + OmnichannelConstants.ContentParts.PhoneNumberInfo + ".Type.Text | append: ': ' | append: Model.ContentItem.Content." + OmnichannelConstants.ContentParts.PhoneNumberInfo + ".Number.PhoneNumber }}",
                })));

        ShellScope.AddDeferredTask(s =>
        {
            return HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("migrate-phone-numbers", async scope =>
            {
                await MigratePhoneNumberDataAsync(scope);
            });
        });

        return 2;
    }

    /// <summary>
    /// Re-runs the phone number backfill for upgraded tenants to recover from earlier failed deferred migrations.
    /// </summary>
    public static int UpdateFrom2()
    {
        ShellScope.AddDeferredTask(s =>
        {
            return HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("migrate-phone-numbers", async scope =>
            {
                await MigratePhoneNumberDataAsync(scope);
            });
        });

        return 3;
    }

    private static async Task MigratePhoneNumberDataAsync(ShellScope scope)
    {
        var contentDefinitionManager = scope.ServiceProvider.GetRequiredService<IContentDefinitionManager>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ContactMethodMigrations>>();

        var contentTypes = await GetContentTypesWithOmnichannelContactPartAsync(contentDefinitionManager);

        if (contentTypes.Length == 0)
        {
            return;
        }

        var migratedCount = 0;
        var skippedCount = 0;
        var documentId = 0L;

        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        var phoneNumberService = scope.ServiceProvider.GetRequiredService<IPhoneNumberService>();

        while (true)
        {
            await using var session = store.CreateSession();

            var batch = await session.Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType.IsIn(contentTypes) && index.DocumentId > documentId)
                .OrderBy(index => index.DocumentId)
                .Take(_batchSize)
                .ListAsync();

            if (!batch.Any())
            {
                break;
            }

            var batchMigrated = 0;

            foreach (var contentItem in batch)
            {
                documentId = Math.Max(documentId, contentItem.Id);

                try
                {
                    if (TryMigrateContactPhoneNumbers(contentItem, phoneNumberService))
                    {
                        await session.SaveAsync(contentItem);
                        await session.SaveChangesAsync();
                        batchMigrated++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to migrate phone numbers for content item '{ContentItemId}' (version '{ContentItemVersionId}').",
                        contentItem.ContentItemId,
                        contentItem.ContentItemVersionId);
                    skippedCount++;
                }
            }

            migratedCount += batchMigrated;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Phone number data migration completed. Migrated: {MigratedCount}, Skipped: {SkippedCount}.",
                migratedCount,
                skippedCount);
        }
    }

    private static async Task<string[]> GetContentTypesWithOmnichannelContactPartAsync(IContentDefinitionManager contentDefinitionManager)
    {
        var typeDefinitions = await contentDefinitionManager.ListTypeDefinitionsAsync();

        return typeDefinitions
            .Where(type => type.Parts.Any(part =>
                string.Equals(part.PartDefinition.Name, OmnichannelConstants.ContentParts.OmnichannelContact, StringComparison.Ordinal)))
            .Select(type => type.Name)
            .ToArray();
    }

    internal static bool TryMigrateContactPhoneNumbers(ContentItem contentItem, IPhoneNumberService phoneNumberService)
    {
        var bagNode = contentItem.Content[OmnichannelConstants.NamedParts.ContactMethods] as JsonObject;

        if (bagNode is null)
        {
            return false;
        }

        var contentItems = bagNode["ContentItems"] as JsonArray;

        if (contentItems is null || contentItems.Count == 0)
        {
            return false;
        }

        var anyMigrated = false;

        foreach (var item in contentItems)
        {
            if (item is not JsonObject innerItem)
            {
                continue;
            }

            var contentType = innerItem["ContentType"]?.GetValue<string>();

            if (!string.Equals(contentType, OmnichannelConstants.ContentTypes.PhoneNumber, StringComparison.Ordinal))
            {
                continue;
            }

            if (TryMigratePhoneNumberField(innerItem, phoneNumberService))
            {
                anyMigrated = true;
            }
        }

        return anyMigrated;
    }

    internal static bool TryMigratePhoneNumberField(JsonObject innerContentItem, IPhoneNumberService phoneNumberService)
    {
        var partNode = innerContentItem[OmnichannelConstants.ContentParts.PhoneNumberInfo] as JsonObject;

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

        if (!TryFormatLegacyPhoneNumber(phoneNumberService, textValue, out var e164Number))
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

    private static string GetFormattingRegionCode(string phoneNumber)
        => !string.IsNullOrWhiteSpace(phoneNumber) && phoneNumber.TrimStart().StartsWith('+')
            ? null
            : _defaultRegionCode;

    private static bool TryFormatLegacyPhoneNumber(
        IPhoneNumberService phoneNumberService,
        string phoneNumber,
        out string e164Number)
    {
        var regionCode = GetFormattingRegionCode(phoneNumber);

        if (phoneNumberService.TryFormatToE164(phoneNumber, regionCode, out e164Number))
        {
            return true;
        }

        if (regionCode is null)
        {
            return false;
        }

        if (phoneNumberService.TryFormatToE164(phoneNumber, "CA", out e164Number))
        {
            return true;
        }

        foreach (var supportedRegion in phoneNumberService.GetSupportedRegions())
        {
            if (string.Equals(supportedRegion, regionCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(supportedRegion, "CA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (phoneNumberService.TryFormatToE164(phoneNumber, supportedRegion, out e164Number))
            {
                return true;
            }
        }

        return TryFormatNorthAmericanPhoneNumber(phoneNumberService, phoneNumber, out e164Number);
    }

    private static bool TryFormatNorthAmericanPhoneNumber(
        IPhoneNumberService phoneNumberService,
        string phoneNumber,
        out string e164Number)
    {
        e164Number = null;

        var digits = string.Concat(phoneNumber.Where(char.IsDigit));

        if (digits.Length == 10)
        {
            digits = $"1{digits}";
        }

        if (digits.Length != 11 || digits[0] != '1')
        {
            return false;
        }

        var candidate = $"+{digits}";

        if (string.IsNullOrWhiteSpace(phoneNumberService.GetRegionCode(candidate)))
        {
            return false;
        }

        e164Number = candidate;

        return true;
    }
}
