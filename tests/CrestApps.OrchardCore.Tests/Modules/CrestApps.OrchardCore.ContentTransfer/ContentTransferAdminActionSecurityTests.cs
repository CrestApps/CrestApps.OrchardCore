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

    /// <summary>
    /// SEC-A20 m: bulk Remove needs the delete permission on every list: the import list, the export list and the export
    /// dashboard.
    /// </summary>
    [Theory]
    [InlineData(nameof(AdminController.ListPOST))]
    [InlineData(nameof(AdminController.ExportPOST))]
    [InlineData(nameof(AdminController.ExportDashboardPOST))]
    public async Task BulkRemove_WithoutTheDeletePermission_IsForbidden(string action)
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

        var options = new ListContentTransferEntryOptions { BulkAction = ContentTransferEntryBulkAction.Remove };
        var result = action switch
        {
            nameof(AdminController.ExportPOST) => await controller.ExportPOST(options, ["entry-1"]),
            nameof(AdminController.ExportDashboardPOST) => await controller.ExportDashboardPOST(options, ["entry-1"]),
            _ => await controller.ListPOST(options, ["entry-1"]),
        };

        Assert.IsType<ForbidResult>(result);
    }
}
