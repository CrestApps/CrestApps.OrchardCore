using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Xunit;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// SEC-A9: a role given "Manage AI API keys" could create a key with the Administrator role and call every AI API
/// route with full rights. The permission is now security-critical, and an editor may give a key only roles they hold.
/// </summary>
public sealed class AIApiKeyRoleRulesTests
{
    private static readonly string[] _siteRoles = ["Administrator", "Editor", "Author", "Anonymous"];

    [Fact]
    public void ManageAIApiKeys_IsSecurityCritical()
    {
        Assert.True(AIPermissions.ManageAIApiKeys.IsSecurityCritical);
    }

    [Fact]
    public void GetRefusedRoles_ARoleTheEditorDoesNotHold_IsRefused()
    {
        var refused = AIApiKeyRoleRules.GetRefusedRoles(["Administrator"], [], _siteRoles, role => role == "Editor", editorCanManageRoles: false);

        Assert.Equal(["Administrator"], refused);
    }

    [Fact]
    public void GetRefusedRoles_ARoleTheEditorHolds_IsAllowed()
    {
        Assert.Empty(AIApiKeyRoleRules.GetRefusedRoles(["Editor"], [], _siteRoles, role => role == "Editor", editorCanManageRoles: false));
    }

    [Fact]
    public void GetRefusedRoles_AnEditorWhoManagesRoles_MayGiveAnyRole()
    {
        Assert.Empty(AIApiKeyRoleRules.GetRefusedRoles(["Administrator"], [], _siteRoles, _ => false, editorCanManageRoles: true));
    }

    [Fact]
    public void GetRefusedRoles_ARoleTheKeyAlreadyHad_Stays()
    {
        Assert.Empty(AIApiKeyRoleRules.GetRefusedRoles(["Administrator"], ["Administrator"], _siteRoles, _ => false, editorCanManageRoles: false));
    }

    [Theory]
    [InlineData("Anonymous")]
    [InlineData("NoSuchRole")]
    public void GetRefusedRoles_AnonymousOrAnUnknownRole_IsRefusedEvenForARoleManager(string role)
    {
        Assert.Equal([role], AIApiKeyRoleRules.GetRefusedRoles([role], [], _siteRoles, _ => true, editorCanManageRoles: true));
    }
}
