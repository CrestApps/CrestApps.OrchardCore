using System.Security.Claims;
using CrestApps.Core.ContactCenter.Security;
using CrestApps.Core.Omnichannel.Sms.Portal.Security;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins what each authorization operation means on this host.
/// </summary>
/// <remarks>
/// The suite asks about operations; this host answers with the permissions it has always used. Two
/// things have to hold. Every operation must be mapped, because an unmapped one is denied and the
/// capability quietly disappears. And every mapping must be to the permission that governed the same
/// check before, because a mapping to a more widely held permission is a silent privilege widening
/// that no test would otherwise notice.
/// </remarks>
public sealed class OperationAuthorizationHandlerTests
{
    public static TheoryData<string, string> ContactCenterMappings => new()
    {
        { ContactCenterOperations.SuperviseQueue.Name, ContactCenterPermissions.MonitorContactCenter.Name },
        { ContactCenterOperations.TransferExternally.Name, ContactCenterPermissions.TransferExternally.Name },
        { ContactCenterOperations.TakeAgentWork.Name, ContactCenterPermissions.SignIntoQueues.Name },
        { ContactCenterOperations.MonitorContactCenter.Name, ContactCenterPermissions.MonitorContactCenter.Name },
    };

    public static TheoryData<string, string> SmsPortalMappings => new()
    {
        { SmsPortalOperations.ViewAllConversations.Name, SmsPortalPermissions.ViewAllConversations.Name },
        { SmsPortalOperations.UseSmsPortal.Name, SmsPortalPermissions.UseSmsPortal.Name },
    };

    [Theory]
    [MemberData(nameof(ContactCenterMappings))]
    public void ContactCenterOperation_MapsToItsExistingPermission(string operationName, string expectedPermissionName)
    {
        // Act
        var permission = ContactCenterOperationAuthorizationHandler.GetPermission(operationName);

        // Assert
        Assert.NotNull(permission);
        Assert.Equal(expectedPermissionName, permission.Name);
    }

    [Theory]
    [MemberData(nameof(SmsPortalMappings))]
    public void SmsPortalOperation_MapsToItsExistingPermission(string operationName, string expectedPermissionName)
    {
        // Act
        var permission = SmsPortalOperationAuthorizationHandler.GetPermission(operationName);

        // Assert
        Assert.NotNull(permission);
        Assert.Equal(expectedPermissionName, permission.Name);
    }

    [Fact]
    public void EveryContactCenterOperation_IsMapped()
        => AssertEveryOperationIsMapped(
            typeof(ContactCenterOperations),
            ContactCenterOperationAuthorizationHandler.GetPermission);

    [Fact]
    public void EverySmsPortalOperation_IsMapped()
        => AssertEveryOperationIsMapped(
            typeof(SmsPortalOperations),
            SmsPortalOperationAuthorizationHandler.GetPermission);

    [Fact]
    public async Task AnOperationThisHostDoesNotKnow_IsLeftUnhandled()
    {
        // Arrange
        var handler = new ContactCenterOperationAuthorizationHandler(
            new Lazy<IAuthorizationService>(() => new AlwaysAllowAuthorizationService()));

        var requirement = new OperationAuthorizationRequirement { Name = "SomethingElse" };
        var context = new AuthorizationHandlerContext([requirement], Principal(), resource: null);

        // Act
        await handler.HandleAsync(context);

        // Assert: unhandled means denied, even though the authorization service here allows
        // everything. A stray requirement must not be answered by this handler.
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task WhenThePermissionIsHeld_TheOperationSucceeds()
    {
        // Arrange
        var handler = new ContactCenterOperationAuthorizationHandler(
            new Lazy<IAuthorizationService>(() => new AlwaysAllowAuthorizationService()));

        var context = new AuthorizationHandlerContext(
            [ContactCenterOperations.SuperviseQueue],
            Principal(),
            resource: "queue-1");

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task WhenThePermissionIsNotHeld_TheOperationDoesNotSucceed()
    {
        // Arrange
        var handler = new ContactCenterOperationAuthorizationHandler(
            new Lazy<IAuthorizationService>(() => new AlwaysDenyAuthorizationService()));

        var context = new AuthorizationHandlerContext(
            [ContactCenterOperations.SuperviseQueue],
            Principal(),
            resource: "queue-1");

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// Asserts that every operation the type declares has a permission behind it.
    /// </summary>
    /// <param name="operationsType">The static class declaring the operations.</param>
    /// <param name="resolve">The handler's mapping.</param>
    private static void AssertEveryOperationIsMapped(Type operationsType, Func<string, Permission> resolve)
    {
        var operations = operationsType
            .GetFields()
            .Where(field => field.FieldType == typeof(OperationAuthorizationRequirement))
            .Select(field => (OperationAuthorizationRequirement)field.GetValue(null))
            .ToArray();

        Assert.NotEmpty(operations);

        foreach (var operation in operations)
        {
            Assert.True(
                resolve(operation.Name) is not null,
                $"'{operationsType.Name}.{operation.Name}' has no permission behind it on this host, so every " +
                "caller asking for it is denied and the capability it names is unreachable.");
        }
    }

    private static ClaimsPrincipal Principal()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));

    private sealed class AlwaysAllowAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }

    private sealed class AlwaysDenyAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}
