using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Models;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.ViewModels;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Drivers;

internal sealed class PostgreSQLAIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly PostgreSQLDataSourceOptions _options;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSQLAIDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="options">The global PostgreSQL data source options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PostgreSQLAIDataSourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PostgreSQLDataSourceOptions> options,
        IStringLocalizer<PostgreSQLAIDataSourceDisplayDriver> stringLocalizer)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _options = options.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (!string.Equals(
            AIDataSourceExtensions.GetSourceType(dataSource),
            AIDataSourceSourceTypes.PostgreSQL,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<EditPostgreSQLAIDataSourceViewModel>("PostgreSQLAIDataSource_Edit", model =>
        {
            var metadata = dataSource.GetOrCreate<PostgreSQLSourceMetadata>();
            model.TableName = metadata.TableName;
            model.HasConnectionString = ProtectedSettingExtensions.HasSecret(metadata.ConnectionString);
            model.HasDefaultConnectionString = HasDefaultConnectionString;
            model.UseDefaultConnectionString = HasDefaultConnectionString && !model.HasConnectionString;
        }).Location("Content:11");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (!string.Equals(
            AIDataSourceExtensions.GetSourceType(dataSource),
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

        // When a connection string is configured globally, the operator can opt into it and no
        // connection string is stored on the data source.
        var useDefaultConnectionString = HasDefaultConnectionString && model.UseDefaultConnectionString;

        string connectionString = null;

        if (!useDefaultConnectionString)
        {
            connectionString = _dataProtectionProvider.ApplySecret(AIDataSourceProtectionConstants.SourceSecretPurpose, existingMetadata.ConnectionString, model.ConnectionString);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionString), HasDefaultConnectionString
                    ? S["Provide a PostgreSQL connection string or use the globally configured one."]
                    : S["PostgreSQL connection string is required."]);
            }
        }

        dataSource.Put(new PostgreSQLSourceMetadata
        {
            TableName = model.TableName?.Trim(),
            ConnectionString = connectionString,
        });

        return Edit(dataSource, context);
    }

    private bool HasDefaultConnectionString
        => !string.IsNullOrWhiteSpace(_options.ConnectionString);
}
