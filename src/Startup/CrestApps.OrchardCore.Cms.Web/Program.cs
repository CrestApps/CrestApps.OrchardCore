using CrestApps.Core;
using CrestApps.OrchardCore.Cms.Web;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OrchardCore.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNLogHost();

// The CrestApps metrics follow Orchard Core's own option for tagging metrics with the tenant, so one setting decides it
// for every metric. Spans and logs carry the tenant either way.
CrestAppsOrchardCoreTelemetry.IncludeTenantName = builder.Configuration.GetValue("OrchardCore:OpenTelemetry:Metrics:IncludeTenantName", true);

// Telemetry is collected only when it is exported, so a site without a collector creates no spans or measurements.
// The Aspire AppHost sets OTEL_EXPORTER_OTLP_ENDPOINT for the projects it runs.
var exportTelemetry = builder.Configuration.HasOtlpEndpoint();

// The sources and meters of our own code. 'X.*' does not match 'X' itself, so both are listed.
string[] ownSources =
[
    CrestAppsTelemetry.SourceName,
    CrestAppsTelemetry.SourceName + ".*",
    CrestAppsOrchardCoreTelemetry.SourceNameWildcard,
    "AICompletionService",
];

builder.Services
    .AddContactCenterProcessLiveness()
    .AddOrchardCms(orchardCoreBuilder =>
    {
        if (exportTelemetry)
        {
            // Collects the traces, metrics and logs of Orchard Core (with ASP.NET Core, HttpClient and the runtime), and
            // the telemetry of our own code: CrestApps.Core and the CrestApps modules. Other libraries' telemetry, such
            // as an SDK's, is collected only when it is added to 'OrchardCore:OpenTelemetry:Tracing:Sources' or
            // 'Metrics:Meters'. AICompletionService is the source the AI chat clients report on when the AI "Enable
            // OpenTelemetry" setting is on.
            orchardCoreBuilder.AddOpenTelemetry(openTelemetry => openTelemetry
                .WithTracing(tracing => tracing.AddSource(ownSources))
                .WithMetrics(metrics => metrics.AddMeter(ownSources)));
        }

        orchardCoreBuilder
            // Enable WAL + a busy timeout on every SQLite connection so the app's concurrent writers do not fail
            // with "database is locked". Applied to all tenants; a no-op on non-SQLite providers.
            .AddSqliteConnectionTuning()
            // Installs the tenant hierarchy guards in every tenant. They do nothing for a tenant that is not a parent
            // or a child tenant.
            .AddTenantHierarchy();
    });

var app = builder.Build();

// Liveness answers "should this process be restarted", so it must be answered by the process itself, ahead of
// Orchard Core. A tenant-scoped probe returns 404 whenever the tenant is disabled, renamed, or fails to start,
// and an orchestrator reads 404 as a probe failure — restarting a healthy process for a tenant-level problem.
app.UseContactCenterProcessLiveness();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseOrchardCore();

await app.RunAsync();
