using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Creates the index tables of the AI webhook records, and moves the subscriptions and deliveries those records hold
/// into the Webhooks building block, which sends AI events now.
/// </summary>
internal sealed class AIWebhookIndexMigrations : DataMigration
{
    // The AI-only permission the Webhooks building block replaced, and the permission that opens the Webhooks page.
    private static readonly Dictionary<string, string[]> _permissionCarryOver = new(StringComparer.Ordinal)
    {
        ["ManageAIWebhooks"] = ["ManageCommerceWebhooks"],
    };

    private readonly YesSqlStoreOptions _options;
    private readonly AIWebhookImporter _importer;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookIndexMigrations"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    /// <param name="importer">Moves the AI webhook records into the Webhooks building block.</param>
    public AIWebhookIndexMigrations(IOptions<YesSqlStoreOptions> options, AIWebhookImporter importer)
    {
        _options = options.Value;
        _importer = importer;
    }

    /// <summary>
    /// Creates the subscription and delivery tables. A new tenant has no AI webhook records to move.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await AIWebhookIndexSchema.CreateAsync(SchemaBuilder, _options);

        return 4;
    }

    /// <summary>
    /// Moves the AI webhook subscriptions and pending deliveries into the Webhooks building block. It only adds records
    /// and settles the old ones, so the step is additive.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await _importer.ImportAsync();

        return 2;
    }

    /// <summary>
    /// AI webhooks are managed on the shared Webhooks page now, which the Manage webhooks permission opens. Every role
    /// that could manage AI webhooks is granted it, so nobody loses the screen.
    /// </summary>
    /// <returns>The migration version.</returns>
    public static int UpdateFrom2()
    {
        // The roles are documents; they are written once this step's own transaction has committed.
        MigrationTelemetry.Defer(nameof(AIWebhookIndexMigrations), scope => RolePermissionMigrations.GrantAsync(
            scope.ServiceProvider,
            _permissionCarryOver,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<AIWebhookIndexMigrations>()));

        return 3;
    }

    /// <summary>
    /// Copies the delivered and failed AI deliveries into the Webhooks delivery log, so their history shows on the shared
    /// Deliveries page as it did on the AI delivery pages. The old rows are kept, so the step is additive.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await _importer.ImportHistoryAsync();

        return 4;
    }
}
