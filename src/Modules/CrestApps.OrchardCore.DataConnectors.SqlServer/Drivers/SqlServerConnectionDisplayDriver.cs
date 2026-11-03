using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.SqlServer.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DataConnectors.SqlServer.Drivers;

/// <summary>
/// Edits the server, database and sign-in of a SQL Server connection. The password is write-only: an empty box keeps
/// the stored one.
/// </summary>
internal sealed class SqlServerConnectionDisplayDriver : DisplayDriver<DataConnection>
{
    private readonly IDataConnectionSecretManager _secretManager;

    internal readonly IStringLocalizer S;

    public SqlServerConnectionDisplayDriver(
        IDataConnectionSecretManager secretManager,
        IStringLocalizer<SqlServerConnectionDisplayDriver> stringLocalizer)
    {
        _secretManager = secretManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(DataConnection connection, BuildEditorContext context)
    {
        if (!Applies(connection))
        {
            return null;
        }

        return Initialize<SqlServerConnectionViewModel>("SqlServerConnection_Edit", model =>
        {
            var settings = connection.GetOrCreate<SqlServerConnectionSettings>();

            model.Server = settings.Server;
            model.Database = settings.Database;
            model.Authentication = settings.Authentication;
            model.UserName = settings.UserName;
            model.Encrypt = settings.Encrypt;
            model.TrustServerCertificate = settings.TrustServerCertificate;
            model.ReadOnlyIntent = settings.ReadOnlyIntent;
            model.ConnectTimeoutSeconds = settings.ConnectTimeoutSeconds;
            model.HasPassword = _secretManager.Has(connection, SqlServerDataConnector.PasswordSecret);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(DataConnection connection, UpdateEditorContext context)
    {
        if (!Applies(connection))
        {
            return null;
        }

        var model = new SqlServerConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Server))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Server), S["The server is required."]);
        }
        else if (model.Server.Contains(';', StringComparison.Ordinal) || model.Server.Contains('=', StringComparison.Ordinal))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Server), S["Enter the server's name only, such as sql.example.com,1433, not a connection string."]);
        }

        if (string.IsNullOrWhiteSpace(model.Database))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Database), S["The database is required."]);
        }

        if (model.Authentication is SqlServerAuthenticationType.SqlLogin or SqlServerAuthenticationType.EntraServicePrincipal && string.IsNullOrWhiteSpace(model.UserName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserName), S["The login or client ID is required."]);
        }

        if (model.ConnectTimeoutSeconds is < 1 or > 300)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectTimeoutSeconds), S["The connect timeout must be between 1 and 300 seconds."]);
        }

        connection.Put(new SqlServerConnectionSettings
        {
            Server = model.Server?.Trim(),
            Database = model.Database?.Trim(),
            Authentication = model.Authentication,
            UserName = model.UserName?.Trim(),
            Encrypt = model.Encrypt,
            TrustServerCertificate = model.TrustServerCertificate,
            ReadOnlyIntent = model.ReadOnlyIntent,
            ConnectTimeoutSeconds = model.ConnectTimeoutSeconds,
        });

        _secretManager.Set(connection, SqlServerDataConnector.PasswordSecret, model.Password);

        if (model.Authentication is SqlServerAuthenticationType.SqlLogin or SqlServerAuthenticationType.EntraServicePrincipal &&
            !_secretManager.Has(connection, SqlServerDataConnector.PasswordSecret))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Password), S["The password or client secret is required."]);
        }

        return Edit(connection, context);
    }

    private static bool Applies(DataConnection connection)
        => string.Equals(connection.Connector, SqlServerDataConnector.Name, StringComparison.OrdinalIgnoreCase);
}
