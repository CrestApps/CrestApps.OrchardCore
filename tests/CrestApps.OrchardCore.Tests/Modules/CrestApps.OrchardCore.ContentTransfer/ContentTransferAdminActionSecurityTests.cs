using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Controllers;
using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

/// <summary>
/// SEC-A20 m: Delete, ResumeImport and PauseImport answered GET, so a link or an image could trigger them, and the bulk
/// Remove needed only the permission to list entries.
/// </summary>
public sealed class ContentTransferAdminActionSecurityTests
{
    [Theory]
    [InlineData(nameof(AdminController.Delete))]
    [InlineData(nameof(AdminController.ResumeImport))]
    [InlineData(nameof(AdminController.PauseImport))]
    public void ChangingAnEntry_NeedsAPostWithAnAntiforgeryToken(string action)
    {
        var method = typeof(AdminController).GetMethod(action);

        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task BulkRemove_WithoutTheDeletePermission_IsForbidden()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().Any(r => r.Permission.Name == ContentTransferPermissions.DeleteContentTransferEntries.Name)
                    ? AuthorizationResult.Failed()
                    : AuthorizationResult.Success());

        // Only the authorization checks run before the action is refused, so the other dependencies stay unset.
        var controller = (AdminController)RuntimeHelpers.GetUninitializedObject(typeof(AdminController));
        typeof(AdminController).GetField("_authorizationService", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, authorization.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.ListPOST(new ListContentTransferEntryOptions { BulkAction = ContentTransferEntryBulkAction.Remove }, ["entry-1"]);

        Assert.IsType<ForbidResult>(result);
    }
}
