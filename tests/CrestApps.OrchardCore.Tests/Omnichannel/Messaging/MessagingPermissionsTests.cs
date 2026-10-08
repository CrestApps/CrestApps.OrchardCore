using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public sealed class MessagingPermissionsTests
{
    // A permission's list names the permissions that imply it. Every permission once listed the workspace permission
    // there, meaning to say it needed the workspace, so every agent who could use the workspace also saw every
    // conversation, sent group messages and sent during quiet hours.
    [Theory]
    [MemberData(nameof(SupervisorOnlyPermissions))]
    public void UseMessagingWorkspace_DoesNotImplyASupervisorPermission(string permissionName)
    {
        var permission = AllPermissions().Single(candidate => candidate.Name == permissionName);

        Assert.DoesNotContain(GrantingNames(permission), name => name == MessagingPermissions.UseMessagingWorkspace.Name);
    }

    // Using the workspace opens the page; which conversations it shows is the conversation permissions' to say.
    [Fact]
    public void ConversationPermissions_AreNotImpliedByUsingTheWorkspace()
    {
        Assert.DoesNotContain(GrantingNames(MessagingPermissions.ViewQueueConversations), name => name == MessagingPermissions.UseMessagingWorkspace.Name);
        Assert.DoesNotContain(GrantingNames(MessagingPermissions.ViewOwnConversations), name => name == MessagingPermissions.UseMessagingWorkspace.Name);
    }

    // Static fields initialize in declaration order, so a permission declared above one it lists captures a null.
    [Fact]
    public void ImpliedPermissions_AreAllInitialized()
    {
        foreach (var permission in AllPermissions())
        {
            Assert.All(permission.ImpliedBy ?? [], implied => Assert.NotNull(implied));
        }
    }

    // Like ViewContent and ViewOwnContent: the broader conversation permission includes the narrower ones.
    [Fact]
    public void ConversationPermissions_NarrowFromAllToQueueToOwn()
    {
        Assert.Contains(MessagingPermissions.ViewAllConversations.Name, GrantingNames(MessagingPermissions.ViewQueueConversations));
        Assert.Contains(MessagingPermissions.ViewAllConversations.Name, GrantingNames(MessagingPermissions.ViewOwnConversations));
        Assert.Contains(MessagingPermissions.ViewQueueConversations.Name, GrantingNames(MessagingPermissions.ViewOwnConversations));
        Assert.Empty(GrantingNames(MessagingPermissions.ViewAllConversations));
    }

    [Fact]
    public async Task PermissionProvider_RegistersEveryPermission()
    {
        var permissions = await new MessagingPermissionProvider().GetPermissionsAsync();

        Assert.Equal(
            AllPermissions().Select(permission => permission.Name).Order(),
            permissions.Select(permission => permission.Name).Order());
    }

    [Fact]
    public void AgentStereotype_GrantsTheWorkspaceAndTheQueueInbox_ButNothingASupervisorHolds()
    {
        var agent = Stereotype(OmnichannelConstants.AgentRole);

        Assert.Contains(MessagingPermissions.UseMessagingWorkspace, agent.Permissions);
        Assert.Contains(MessagingPermissions.ViewQueueConversations, agent.Permissions);
        Assert.DoesNotContain(MessagingPermissions.ViewAllConversations, agent.Permissions);
        Assert.DoesNotContain(MessagingPermissions.SendGroupMessages, agent.Permissions);
        Assert.DoesNotContain(MessagingPermissions.SendDuringQuietHours, agent.Permissions);
        Assert.DoesNotContain(MessagingPermissions.ManageMessaging, agent.Permissions);
    }

    [Fact]
    public void SupervisorStereotype_GrantsEveryConversationAndGroupMessages()
    {
        var supervisor = Stereotype(OmnichannelConstants.SupervisorRole);

        Assert.Contains(MessagingPermissions.UseMessagingWorkspace, supervisor.Permissions);
        Assert.Contains(MessagingPermissions.ViewAllConversations, supervisor.Permissions);
        Assert.Contains(MessagingPermissions.SendGroupMessages, supervisor.Permissions);
        Assert.Contains(MessagingPermissions.SendDuringQuietHours, supervisor.Permissions);
    }

    public static TheoryData<string> SupervisorOnlyPermissions()
        =>
        [
            MessagingPermissions.ViewAllConversations.Name,
            MessagingPermissions.SendGroupMessages.Name,
            MessagingPermissions.SendDuringQuietHours.Name,
            MessagingPermissions.ManageMessaging.Name,
        ];

    private static PermissionStereotype Stereotype(string name)
        => new MessagingPermissionProvider().GetDefaultStereotypes().Single(stereotype => stereotype.Name == name);

    private static Permission[] AllPermissions()
        =>
        [
            MessagingPermissions.ManageMessaging,
            MessagingPermissions.UseMessagingWorkspace,
            MessagingPermissions.ViewAllConversations,
            MessagingPermissions.ViewQueueConversations,
            MessagingPermissions.ViewOwnConversations,
            MessagingPermissions.SendDuringQuietHours,
            MessagingPermissions.SendGroupMessages,
        ];

    // Every permission that grants the given one, following the implications the way Orchard Core's permission handler
    // does.
    private static HashSet<string> GrantingNames(Permission permission)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<Permission>(permission.ImpliedBy ?? []);

        while (pending.Count > 0)
        {
            var next = pending.Pop();

            if (next is not null && names.Add(next.Name))
            {
                foreach (var implied in next.ImpliedBy ?? [])
                {
                    pending.Push(implied);
                }
            }
        }

        return names;
    }
}
