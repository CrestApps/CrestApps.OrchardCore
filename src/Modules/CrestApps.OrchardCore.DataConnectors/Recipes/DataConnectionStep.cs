using CrestApps.OrchardCore.DataConnectors.Drivers;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.DataConnectors.Recipes;

/// <summary>
/// Creates or updates data connections. A connection is matched by its identifier, then by its technical name.
/// </summary>
internal sealed class DataConnectionStep : NamedRecipeStepHandler
{
    public const string StepKey = "DataConnection";

    public const string EntriesMemberName = "Connections";

    private readonly IDataConnectionStore _connectionStore;
    private readonly IDataConnectorManager _connectorManager;
    private readonly IDataConnectionSecretManager _secretManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectionStep"/> class.
    /// </summary>
    /// <param name="connectionStore">The connection store.</param>
    /// <param name="connectorManager">The registered connectors.</param>
    /// <param name="secretManager">The manager that protects the connections' secrets.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DataConnectionStep(
        IDataConnectionStore connectionStore,
        IDataConnectorManager connectorManager,
        IDataConnectionSecretManager secretManager,
        IClock clock,
        IStringLocalizer<DataConnectionStep> stringLocalizer)
        : base(StepKey)
    {
        _connectionStore = connectionStore;
        _connectorManager = connectorManager;
        _secretManager = secretManager;
        _clock = clock;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var (_, model) in DataConnectorRecipeJson.ReadEntries<DataConnectionRecipeEntry>(context.Step, EntriesMemberName))
        {
            if (model is null)
            {
                context.Errors.Add(S["Each entry of the '{0}' step must be an object.", StepKey]);

                continue;
            }

            var displayText = string.IsNullOrWhiteSpace(model.DisplayText) ? model.Name?.Trim() : model.DisplayText.Trim();
            var name = string.IsNullOrWhiteSpace(model.Name)
                ? (string.IsNullOrEmpty(displayText) ? null : ConnectedDataSetFields.ToFieldName(displayText))
                : model.Name.Trim();

            if (string.IsNullOrEmpty(name) || !DataConnectionDisplayDriver.TechnicalName().IsMatch(name))
            {
                context.Errors.Add(S["The data connection '{0}' needs a technical name that starts with a letter and holds only letters, digits and underscores.", name ?? displayText]);

                continue;
            }

            var connection = string.IsNullOrEmpty(model.ItemId) ? null : await _connectionStore.FindByIdAsync(model.ItemId);
            var byName = await _connectionStore.FindByNameAsync(name);

            if (connection is null)
            {
                connection = byName;
            }
            else if (byName is not null && byName.ItemId != connection.ItemId)
            {
                context.Errors.Add(S["Another data connection already has the technical name '{0}'.", name]);

                continue;
            }

            var connectorName = string.IsNullOrWhiteSpace(model.Connector) ? connection?.Connector : model.Connector.Trim();

            if (string.IsNullOrEmpty(connectorName) || _connectorManager.GetDescriptor(connectorName) is not { } descriptor)
            {
                context.Errors.Add(S["The data connection '{0}' uses the connector '{1}', which isn't enabled.", name, connectorName]);

                continue;
            }

            if (connection is not null && !string.Equals(connection.Connector, descriptor.Name, StringComparison.Ordinal))
            {
                context.Errors.Add(S["The data connection '{0}' uses the connector '{1}'; a recipe can't change it to '{2}'.", name, connection.Connector, descriptor.Name]);

                continue;
            }

            var isNew = connection is null;

            connection ??= new DataConnection
            {
                ItemId = DataConnectorRecipeJson.NewItemId(model.ItemId),
                Connector = descriptor.Name,
                CreatedUtc = model.CreatedUtc ?? _clock.UtcNow,
                Author = model.Author,
            };

            connection.Name = name;
            connection.DisplayText = displayText;
            connection.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

            connection.Properties ??= new Dictionary<string, object>();
            DataConnectorRecipeJson.MergeProperties(connection.Properties, model.Properties);

            foreach (var secret in model.Secrets ?? [])
            {
                if (!string.IsNullOrWhiteSpace(secret.Key))
                {
                    // An empty value keeps the stored secret, as an empty box does in the editor.
                    _secretManager.Set(connection, secret.Key.Trim(), secret.Value);
                }
            }

            if (isNew)
            {
                await _connectionStore.CreateAsync(connection);
            }
            else
            {
                connection.ModifiedUtc = model.ModifiedUtc ?? _clock.UtcNow;
                await _connectionStore.UpdateAsync(connection);
            }
        }
    }
}
