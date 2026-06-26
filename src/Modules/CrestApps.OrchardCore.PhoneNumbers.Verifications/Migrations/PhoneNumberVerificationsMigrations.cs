using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Migrations;

/// <summary>
/// Defines the content part definition and the SQL index used by the Phone Number Verifications module.
/// </summary>
internal sealed class PhoneNumberVerificationsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationsMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public PhoneNumberVerificationsMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates the content part definition and the verification index table.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(PhoneNumberVerificationsConstants.VerificationPartName, part => part
            .Attachable()
            .WithDisplayName("Phone Number Verification")
            .WithDescription("Stores phone number verification data and the normalized provider response on a content item.")
        );

        await SchemaBuilder.CreateMapIndexTableAsync<PhoneNumberVerificationPartIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("PhoneNumber", column => column.WithLength(50))
            .Column<string>("NormalizedPhoneNumber", column => column.WithLength(30))
            .Column<bool>("IsVerified", column => column.NotNull().WithDefault(false))
            .Column<int>("VerificationStatus")
            .Column<string>("VerificationProvider", column => column.WithLength(64))
            .Column<DateTime>("LastVerifiedUtc")
            .Column<DateTime>("NextVerificationDueUtc")
            .Column<string>("CountryCode", column => column.WithLength(2))
            .Column<string>("Carrier", column => column.WithLength(128))
            .Column<bool>("IsMobile", column => column.NotNull().WithDefault(false))
            .Column<bool>("IsLandline", column => column.NotNull().WithDefault(false))
            .Column<bool>("IsVoip", column => column.NotNull().WithDefault(false))
            .Column<string>("LineStatus", column => column.WithLength(32))
        );

        await SchemaBuilder.AlterIndexTableAsync<PhoneNumberVerificationPartIndex>(table => table
            .CreateIndex("IDX_PhoneNumberVerificationPartIndex_ContentItemId",
                "DocumentId",
                "ContentItemId"
            )
        );

        await SchemaBuilder.AlterIndexTableAsync<PhoneNumberVerificationPartIndex>(table => table
            .CreateIndex("IDX_PhoneNumberVerificationPartIndex_Status",
                "DocumentId",
                "IsVerified",
                "VerificationStatus",
                "NextVerificationDueUtc"
            )
        );

        await SchemaBuilder.AlterIndexTableAsync<PhoneNumberVerificationPartIndex>(table => table
            .CreateIndex("IDX_PhoneNumberVerificationPartIndex_PhoneNumber",
                "DocumentId",
                "PhoneNumber",
                "NormalizedPhoneNumber"
            )
        );

        return 2;
    }

    /// <summary>
    /// Adds the verification index columns introduced after the initial module version.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<PhoneNumberVerificationPartIndex>(table =>
        {
            table.AddColumn<string>("NormalizedPhoneNumber", column => column.WithLength(30));
            table.AddColumn<string>("LineStatus", column => column.WithLength(32));
        });

        return 2;
    }
}
