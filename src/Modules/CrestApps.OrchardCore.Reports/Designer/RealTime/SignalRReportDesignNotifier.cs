using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Reports.Designer.RealTime;

/// <summary>
/// Sends the changes of designed reports to the people who have them open, through <see cref="ReportsHub"/>. A change
/// is sent once the current shell scope commits, so a page that reloads because of it sees the change.
/// </summary>
public sealed class SignalRReportDesignNotifier : IReportDesignNotifier
{
    /// <summary>
    /// The client method that receives report changes.
    /// </summary>
    public const string ClientMethod = "ReportDesignChanged";

    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRReportDesignNotifier"/> class.
    /// </summary>
    /// <param name="shellSettings">The current tenant.</param>
    public SignalRReportDesignNotifier(ShellSettings shellSettings)
    {
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public Task ReportDesignChangedAsync(ReportDesignChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var tenantName = _tenantName;

        ShellScope.AddDeferredTask(scope => SendAsync(scope.ServiceProvider.GetRequiredService<IHubContext<ReportsHub>>(), tenantName, change));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a change to the people who have the report open.
    /// </summary>
    /// <param name="hub">The hub context.</param>
    /// <param name="tenantName">The tenant the report belongs to.</param>
    /// <param name="change">The change.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task SendAsync(IHubContext<ReportsHub> hub, string tenantName, ReportDesignChange change)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(change);

        return hub.Clients.Group(ReportsHub.Group(tenantName, change.DesignId)).SendAsync(ClientMethod, new
        {
            kind = change.Kind.ToString(),
            designId = change.DesignId,
            revision = change.Revision,
            versionNumber = change.VersionNumber,
            userId = change.UserId,
            userName = change.UserName,
        });
    }
}
