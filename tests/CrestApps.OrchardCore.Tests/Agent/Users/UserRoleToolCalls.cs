using CrestApps.OrchardCore.AI.Agent.Roles;
using CrestApps.OrchardCore.AI.Agent.Users;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Agent.Users;

/// <summary>
/// The user and role tools with a call that works on the fixture of <see cref="UserRoleToolHarness"/>, for the tests
/// that run every tool.
/// </summary>
internal static class UserRoleToolCalls
{
    /// <summary>
    /// The names of the tools, for a theory.
    /// </summary>
    public static readonly string[] ToolNames =
    [
        ListUsersTool.TheName,
        GetUserTool.TheName,
        SaveUserTool.TheName,
        SetUserRolesTool.TheName,
        EnableUserTool.TheName,
        DisableUserTool.TheName,
        UnlockUserTool.TheName,
        ConfirmUserEmailTool.TheName,
        DeleteUserTool.TheName,
        DisableUserTwoFactorTool.TheName,
        SaveUserCustomSettingsTool.TheName,
        ListRolesTool.TheName,
        GetRoleDetailsTool.TheName,
        ListPermissionsTool.TheName,
        SaveRoleTool.TheName,
        DeleteRoleTool.TheName,
    ];

    /// <summary>
    /// Creates a tool with arguments that make it act on the user Jane or on a role of the fixture.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>The tool and its arguments.</returns>
    public static (AIFunction Tool, Dictionary<string, object> Arguments) Create(string toolName)
    {
        return toolName switch
        {
            ListUsersTool.TheName => (new ListUsersTool(), []),
            GetUserTool.TheName => (new GetUserTool(), new() { ["userId"] = "user-jane" }),
            SaveUserTool.TheName => (new SaveUserTool(), new() { ["userId"] = "user-jane", ["email"] = "jane.doe@contoso.com" }),
            SetUserRolesTool.TheName => (new SetUserRolesTool(), new() { ["userId"] = "user-jane", ["add"] = new[] { "Author" }, ["remove"] = new[] { "Editor" } }),
            EnableUserTool.TheName => (new EnableUserTool(), new() { ["userIds"] = new[] { "user-sam", "user-jane" } }),
            DisableUserTool.TheName => (new DisableUserTool(), new() { ["userIds"] = new[] { "user-jane" } }),
            UnlockUserTool.TheName => (new UnlockUserTool(), new() { ["userId"] = "user-jane" }),
            ConfirmUserEmailTool.TheName => (new ConfirmUserEmailTool(), new() { ["userId"] = "user-jane" }),
            DeleteUserTool.TheName => (new DeleteUserTool(), new() { ["userId"] = "user-jane", ["userName"] = "jane" }),
            DisableUserTwoFactorTool.TheName => (new DisableUserTwoFactorTool(), new() { ["userId"] = "user-jane" }),
            SaveUserCustomSettingsTool.TheName => (new SaveUserCustomSettingsTool(), new()
            {
                ["userId"] = "user-jane",
                ["settingsType"] = "UserProfile",
                ["settings"] = """{ "UserProfile": { "Department": { "Text": "Finance" } } }""",
            }),
            ListRolesTool.TheName => (new ListRolesTool(), []),
            GetRoleDetailsTool.TheName => (new GetRoleDetailsTool(), new() { ["roleName"] = "Editor" }),
            ListPermissionsTool.TheName => (new ListPermissionsTool(), []),
            SaveRoleTool.TheName => (new SaveRoleTool(), new() { ["roleName"] = "Editor", ["permissions"] = new[] { "PublishContent" } }),
            DeleteRoleTool.TheName => (new DeleteRoleTool(), new() { ["roleName"] = "Editor" }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "The tool has no test arguments."),
        };
    }
}
