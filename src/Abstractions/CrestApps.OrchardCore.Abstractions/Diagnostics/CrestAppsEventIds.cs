namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// The first <c>EventId</c> of each area's <c>[LoggerMessage]</c> log messages. Each area owns the thousand ids that
/// start at its value, so a log line can be counted and alerted on by its id rather than by its text.
/// </summary>
/// <remarks>
/// CrestApps.Core uses the ids below 10000 (2000s AI pipeline, 3000s chat, 4000s ingestion, 5000s retrieval, 7000s MCP
/// and A2A, 9000s sample hosts), and the phone number verification providers keep the 1000s they already use, so the
/// Orchard Core modules start at 10000 and never share an id with the framework.
/// </remarks>
public static class CrestAppsEventIds
{
    /// <summary>
    /// The shared platform helpers: background tasks, deferred work, locks, retries and the pacer.
    /// </summary>
    public const int Platform = 10000;

    /// <summary>
    /// The AI modules.
    /// </summary>
    public const int AI = 11000;

    /// <summary>
    /// The commerce modules: carts, checkout, orders, payments, subscriptions, webhooks and the modules that sell.
    /// </summary>
    public const int Commerce = 12000;

    /// <summary>
    /// Scheduling, calendars, workforce, skills, work orders, dispatch and geo.
    /// </summary>
    public const int Scheduling = 13000;

    /// <summary>
    /// Reports and the report builder.
    /// </summary>
    public const int Reports = 14000;

    /// <summary>
    /// Data connectors.
    /// </summary>
    public const int DataConnectors = 15000;

    /// <summary>
    /// Content transfer, imports, exports, recipes and the do-not-call registry.
    /// </summary>
    public const int ContentTransfer = 16000;

    /// <summary>
    /// The contact center.
    /// </summary>
    public const int ContactCenter = 17000;

    /// <summary>
    /// Omnichannel messaging, SMS and AI voice.
    /// </summary>
    public const int Messaging = 18000;

    /// <summary>
    /// Telephony and its providers, such as Telnyx and Asterisk.
    /// </summary>
    public const int Telephony = 19000;

    /// <summary>
    /// Migrations, startup and tenant management.
    /// </summary>
    public const int Startup = 20000;
}
