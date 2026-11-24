using CrestApps.OrchardCore.AI.Agent.AuditTrail;
using CrestApps.OrchardCore.AI.Agent.Communications;
using CrestApps.OrchardCore.AI.Agent.Notifications;
using CrestApps.OrchardCore.AI.Agent.OpenId;
using CrestApps.OrchardCore.AI.Agent.RateLimits;
using CrestApps.OrchardCore.AI.Agent.Secrets;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Agent.Security;

/// <summary>
/// The OpenID, rate limit, secret, audit trail, notification, test email and test SMS tools, each with a call that
/// works on the fixture of <see cref="SecurityToolHarness"/>, for the tests that run every tool.
/// </summary>
internal static class SecurityToolCalls
{
    /// <summary>
    /// The names of the tools, for a theory.
    /// </summary>
    public static readonly string[] ToolNames =
    [
        ListOpenIdApplicationsTool.TheName,
        GetOpenIdApplicationTool.TheName,
        ListOpenIdScopesTool.TheName,
        ListRateLimitPoliciesTool.TheName,
        SetRateLimitPolicyEnabledTool.TheName,
        ListSecretsTool.TheName,
        ListAuditTrailEventsTool.TheName,
        GetAuditTrailEventTool.TheName,
        ListNotificationsTool.TheName,
        MarkNotificationsTool.TheName,
        SendTestEmailTool.TheName,
        SendTestSmsTool.TheName,
    ];

    /// <summary>
    /// Gets the name of the permission the admin action of a tool asks for.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>The name of the permission.</returns>
    public static string GetPermission(string toolName)
    {
        return toolName switch
        {
            ListOpenIdApplicationsTool.TheName or
            GetOpenIdApplicationTool.TheName => "ManageApplications",
            ListOpenIdScopesTool.TheName => "ManageScopes",
            ListRateLimitPoliciesTool.TheName or
            SetRateLimitPolicyEnabledTool.TheName => "ManageRateLimits",
            ListSecretsTool.TheName => "ViewSecrets",
            ListAuditTrailEventsTool.TheName or
            GetAuditTrailEventTool.TheName => "ViewAuditTrail",
            ListNotificationsTool.TheName or
            MarkNotificationsTool.TheName => "ManageNotifications",
            SendTestEmailTool.TheName => "ManageEmailSettings",
            SendTestSmsTool.TheName => "ManageSmsSettings",
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "The tool has no permission in the test."),
        };
    }

    /// <summary>
    /// Creates a tool with arguments that make it do its work on the fixture: where a record holds a secret, the
    /// call acts on that record.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>The tool and its arguments.</returns>
    public static (AIFunction Tool, Dictionary<string, object> Arguments) Create(string toolName)
    {
        return toolName switch
        {
            ListOpenIdApplicationsTool.TheName => (new ListOpenIdApplicationsTool(), []),
            GetOpenIdApplicationTool.TheName => (new GetOpenIdApplicationTool(), new() { ["id"] = "app-backend" }),
            ListOpenIdScopesTool.TheName => (new ListOpenIdScopesTool(), []),
            ListRateLimitPoliciesTool.TheName => (new ListRateLimitPoliciesTool(), []),
            SetRateLimitPolicyEnabledTool.TheName => (new SetRateLimitPolicyEnabledTool(), new() { ["policyIds"] = new[] { "policy-draft" }, ["enabled"] = true }),
            ListSecretsTool.TheName => (new ListSecretsTool(), []),
            ListAuditTrailEventsTool.TheName => (new ListAuditTrailEventsTool(), []),
            GetAuditTrailEventTool.TheName => (new GetAuditTrailEventTool(), new() { ["eventId"] = "event-user-updated" }),
            ListNotificationsTool.TheName => (new ListNotificationsTool(), []),
            MarkNotificationsTool.TheName => (new MarkNotificationsTool(), new() { ["action"] = "read", ["notificationIds"] = new[] { "note-1" } }),
            SendTestEmailTool.TheName => (new SendTestEmailTool(), []),
            SendTestSmsTool.TheName => (new SendTestSmsTool(), new() { ["phoneNumber"] = "+15551234567", ["provider"] = "Twilio" }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "The tool has no test arguments."),
        };
    }
}
