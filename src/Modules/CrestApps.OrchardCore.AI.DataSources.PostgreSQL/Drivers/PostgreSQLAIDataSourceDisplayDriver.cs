using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Drivers;

internal sealed class PostgreSQLAIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSQLAIDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PostgreSQLAIDataSourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<PostgreSQLAIDataSourceDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (!string.Equals(
            GetSourceType(dataSource),
            AIDataSourceSourceTypes.PostgreSQL,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<EditPostgreSQLAIDataSourceViewModel>("PostgreSQLAIDataSource_Edit", model =>
        {
            var metadata = dataSource.GetOrCreate<PostgreSQLSourceMetadata>();
            model.TableName = metadata.TableName;
            model.HasConnectionString = !string.IsNullOrWhiteSpace(metadata.ConnectionString);
        }).Location("Content:11");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (!string.Equals(
            GetSourceType(dataSource),
            AIDataSourceSourceTypes.PostgreSQL,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new EditPostgreSQLAIDataSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var existingMetadata = dataSource.GetOrCreate<PostgreSQLSourceMetadata>();

        if (string.IsNullOrWhiteSpace(model.TableName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TableName), S["PostgreSQL table name is required."]);
        }

        if (string.IsNullOrWhiteSpace(existingMetadata.ConnectionString) &&
            string.IsNullOrWhiteSpace(model.ConnectionString))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionString), S["PostgreSQL connection string is required."]);
        }

        var protector = _dataProtectionProvider.CreateProtector(AIDataSourceProtectionConstants.SourceSecretPurpose);

        dataSource.Put(new PostgreSQLSourceMetadata
        {
            TableName = model.TableName?.Trim(),
            ConnectionString = string.IsNullOrWhiteSpace(model.ConnectionString)
                ? existingMetadata.ConnectionString
                : protector.Protect(model.ConnectionString),
        });

        return Edit(dataSource, context);
    }

    private static string GetSourceType(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return string.IsNullOrWhiteSpace(dataSource.Source)
            ? AIDataSourceSourceTypes.SearchIndexProfile
            : dataSource.Source;
    }
}
