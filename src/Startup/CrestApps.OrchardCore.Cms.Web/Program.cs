using CrestApps.Core.OpenTelemetry;
using CrestApps.OrchardCore.Cms.Web;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OrchardCore.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNLogHost();

// The CrestApps metrics carry no tenant tag unless the host opts in, since it multiplies the series a collector
// stores by the number of tenants. Spans and logs carry the tenant either way.
CrestAppsOrchardCoreTelemetry.IncludeTenantName = builder.Configuration.GetValue<bool>("OrchardCore:CrestApps:OpenTelemetry:Metrics:IncludeTenantName");

// Telemetry is collected only when it is exported, so a site without a collector creates no spans or measurements.
// The Aspire AppHost sets OTEL_EXPORTER_OTLP_ENDPOINT for the projects it runs.
var exportTelemetry = builder.Configuration.HasOtlpEndpoint();

// The Azure OpenAI chat client calls the OpenAI SDK directly, which reports its own gen_ai telemetry once this switch
// is on.
if (exportTelemetry)
{
    AppContext.SetSwitch("OpenAI.Experimental.EnableOpenTelemetry", true);
}

builder.Services
    .AddContactCenterProcessLiveness()
    .AddOrchardCms(orchardCoreBuilder =>
    {
        if (exportTelemetry)
        {
            // Collects the traces, metrics and logs of Orchard Core, of CrestApps.Core and of the CrestApps modules,
            // and exports them over OTLP.
            orchardCoreBuilder.AddOpenTelemetry(openTelemetry => openTelemetry
                .WithTracing(tracing => tracing.AddSource(CrestAppsOrchardCoreTelemetry.SourceNameWildcard))
                .WithMetrics(metrics => metrics.AddMeter(CrestAppsOrchardCoreTelemetry.SourceNameWildcard))
                .AddCrestAppsOpenTelemetry());
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
