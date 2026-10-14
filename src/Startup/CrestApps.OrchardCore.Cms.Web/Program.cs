using CrestApps.OrchardCore.Cms.Web;
using CrestApps.OrchardCore.ContactCenter;
using OrchardCore.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNLogHost();

builder.Services
    .AddContactCenterProcessLiveness()
    .AddOrchardCms(orchardCoreBuilder => orchardCoreBuilder
        // Enable WAL + a busy timeout on every SQLite connection so the app's concurrent writers do not fail
        // with "database is locked". Applied to all tenants; a no-op on non-SQLite providers.
        .AddSqliteConnectionTuning()
        // Installs the tenant hierarchy guards in every tenant. They do nothing for a tenant that is not a parent or
        // a child tenant.
        .AddTenantHierarchy());

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
