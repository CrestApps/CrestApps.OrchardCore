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
        SaveOpenIdApplicationTool.TheName,
        DeleteOpenIdApplicationTool.TheName,
        ListOpenIdScopesTool.TheName,
        SaveOpenIdScopeTool.TheName,
        DeleteOpenIdScopeTool.TheName,
        ListRateLimitPoliciesTool.TheName,
        SaveRateLimitPolicyTool.TheName,
        EnableRateLimitPolicyTool.TheName,
        DisableRateLimitPolicyTool.TheName,
        DeleteRateLimitPolicyTool.TheName,
        ListSecretsTool.TheName,
        SaveSecretTool.TheName,
        DeleteSecretTool.TheName,
        MoveSecretsTool.TheName,
        ListAuditTrailEventsTool.TheName,
        GetAuditTrailEventTool.TheName,
        ListNotificationsTool.TheName,
        MarkNotificationsTool.TheName,
        DeleteNotificationsTool.TheName,
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
            GetOpenIdApplicationTool.TheName or
            SaveOpenIdApplicationTool.TheName or
            DeleteOpenIdApplicationTool.TheName => "ManageApplications",
            ListOpenIdScopesTool.TheName or
            SaveOpenIdScopeTool.TheName or
            DeleteOpenIdScopeTool.TheName => "ManageScopes",
            ListRateLimitPoliciesTool.TheName or
            SaveRateLimitPolicyTool.TheName or
            EnableRateLimitPolicyTool.TheName or
            DisableRateLimitPolicyTool.TheName or
            DeleteRateLimitPolicyTool.TheName => "ManageRateLimits",
            ListSecretsTool.TheName => "ViewSecrets",
            SaveSecretTool.TheName or
            DeleteSecretTool.TheName or
            MoveSecretsTool.TheName => "ManageSecrets",
            ListAuditTrailEventsTool.TheName or
            GetAuditTrailEventTool.TheName => "ViewAuditTrail",
            ListNotificationsTool.TheName or
            MarkNotificationsTool.TheName or
            DeleteNotificationsTool.TheName => "ManageNotifications",
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
            SaveOpenIdApplicationTool.TheName => (new SaveOpenIdApplicationTool(), new() { ["id"] = "app-backend", ["displayName"] = "Backend API" }),
            DeleteOpenIdApplicationTool.TheName => (new DeleteOpenIdApplicationTool(), new() { ["id"] = "app-backend", ["clientId"] = "backend-service" }),
            ListOpenIdScopesTool.TheName => (new ListOpenIdScopesTool(), []),
            SaveOpenIdScopeTool.TheName => (new SaveOpenIdScopeTool(), new() { ["id"] = "scope-orders", ["description"] = "Reads orders." }),
            DeleteOpenIdScopeTool.TheName => (new DeleteOpenIdScopeTool(), new() { ["id"] = "scope-orders", ["name"] = "orders.read" }),
            ListRateLimitPoliciesTool.TheName => (new ListRateLimitPoliciesTool(), []),
            SaveRateLimitPolicyTool.TheName => (new SaveRateLimitPolicyTool(), new()
            {
                ["name"] = "Search API",
                ["scope"] = "endpoint",
                ["path"] = "/api/find",
                ["limiters"] = """[ { "type": "fixedWindow", "permitLimit": 10, "windowSeconds": 60 } ]""",
            }),
            EnableRateLimitPolicyTool.TheName => (new EnableRateLimitPolicyTool(), new() { ["policyIds"] = new[] { "policy-draft" } }),
            DisableRateLimitPolicyTool.TheName => (new DisableRateLimitPolicyTool(), new() { ["policyIds"] = new[] { "policy-contact" } }),
            DeleteRateLimitPolicyTool.TheName => (new DeleteRateLimitPolicyTool(), new() { ["policyIds"] = new[] { "policy-draft" } }),
            ListSecretsTool.TheName => (new ListSecretsTool(), []),
            SaveSecretTool.TheName => (new SaveSecretTool(), new() { ["name"] = "Smtp.Password", ["description"] = "The password of the mail account." }),
            DeleteSecretTool.TheName => (new DeleteSecretTool(), new() { ["name"] = "Smtp.Password" }),
            MoveSecretsTool.TheName => (new MoveSecretsTool(), new() { ["sourceStore"] = "Database", ["destinationStore"] = "AzureKeyVault" }),
            ListAuditTrailEventsTool.TheName => (new ListAuditTrailEventsTool(), []),
            GetAuditTrailEventTool.TheName => (new GetAuditTrailEventTool(), new() { ["eventId"] = "event-user-updated" }),
            ListNotificationsTool.TheName => (new ListNotificationsTool(), []),
            MarkNotificationsTool.TheName => (new MarkNotificationsTool(), new() { ["action"] = "read", ["notificationIds"] = new[] { "note-1" } }),
            DeleteNotificationsTool.TheName => (new DeleteNotificationsTool(), new() { ["notificationIds"] = new[] { "note-1" } }),
            SendTestEmailTool.TheName => (new SendTestEmailTool(), []),
            SendTestSmsTool.TheName => (new SendTestSmsTool(), new() { ["phoneNumber"] = "+15551234567", ["provider"] = "Twilio" }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "The tool has no test arguments."),
        };
    }
}
