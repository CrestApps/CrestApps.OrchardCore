using System.Security.Claims;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Handlers;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportDesignAuthorizationHandlerTests
{
    private static readonly ReportDesign _design = new()
    {
        ItemId = "r1",
        DisplayText = "Revenue",
        OwnerId = "owner",
        SharedUserNames = ["ShareUser"],
        SharedRoles = ["Sales"],
    };

    [Fact]
    public async Task Owner_CanRunTheirReport_EvenWithoutDesignerPermission()
    {
        Assert.True(await AuthorizeAsync(User("owner", "Owner"), ReportDesignerPermissions.ViewAllReportDesigns, _design));
    }

    [Fact]
    public async Task Owner_CanChangeTheirReport_OnlyWithTheDesignerPermission()
    {
        Assert.True(await AuthorizeAsync(User("owner", "Owner"), ReportDesignerPermissions.ManageAllReportDesigns, _design, Design));
        Assert.False(await AuthorizeAsync(User("owner", "Owner"), ReportDesignerPermissions.ManageAllReportDesigns, _design));
    }

    [Fact]
    public async Task SharedUser_CanRunButNotChangeTheReport()
    {
        var user = User("u2", "shareuser");

        Assert.True(await AuthorizeAsync(user, ReportDesignerPermissions.ViewAllReportDesigns, _design, Design));
        Assert.False(await AuthorizeAsync(user, ReportDesignerPermissions.ManageAllReportDesigns, _design, Design));
    }

    [Fact]
    public async Task MemberOfASharedRole_CanRunTheReport()
    {
        Assert.True(await AuthorizeAsync(User("u3", "Someone", "Sales"), ReportDesignerPermissions.ViewAllReportDesigns, _design));
        Assert.False(await AuthorizeAsync(User("u4", "Someone else", "Support"), ReportDesignerPermissions.ViewAllReportDesigns, _design));
    }

    [Fact]
    public async Task AnonymousVisitor_CanRunOnlyAReportSharedWithTheAnonymousRole()
    {
        var publicDesign = _design.Clone();
        publicDesign.SharedRoles = ["Anonymous"];
        var signedInDesign = _design.Clone();
        signedInDesign.SharedRoles = ["Authenticated"];

        Assert.True(await AuthorizeAsync(Anonymous(), ReportDesignerPermissions.ViewAllReportDesigns, publicDesign));
        Assert.False(await AuthorizeAsync(Anonymous(), ReportDesignerPermissions.ViewAllReportDesigns, _design));
        Assert.False(await AuthorizeAsync(Anonymous(), ReportDesignerPermissions.ViewAllReportDesigns, signedInDesign));
        Assert.True(await AuthorizeAsync(User("u5", "Anybody"), ReportDesignerPermissions.ViewAllReportDesigns, signedInDesign));
    }

    [Fact]
    public async Task AnonymousShare_NeverGrantsChanges()
    {
        var publicDesign = _design.Clone();
        publicDesign.SharedRoles = ["Anonymous"];

        Assert.False(await AuthorizeAsync(Anonymous(), ReportDesignerPermissions.ManageAllReportDesigns, publicDesign, Design));
    }

    [Fact]
    public async Task UnrelatedPermission_IsNeverGranted()
    {
        Assert.False(await AuthorizeAsync(User("owner", "Owner"), ReportDesignerPermissions.ShareReportsPublicly, _design, Design));
    }

    [Fact]
    public async Task View_IsReadableByEveryDesigner_AndChangeableByItsOwner()
    {
        var view = new ReportView
        {
            ItemId = "v1",
            OwnerId = "owner",
        };

        Assert.True(await AuthorizeAsync(User("u6", "Designer"), ReportDesignerPermissions.ViewAllReportDesigns, view, Design));
        Assert.False(await AuthorizeAsync(User("u7", "Viewer"), ReportDesignerPermissions.ViewAllReportDesigns, view));
        Assert.False(await AuthorizeAsync(User("u6", "Designer"), ReportDesignerPermissions.ManageAllReportDesigns, view, Design));
        Assert.True(await AuthorizeAsync(User("owner", "Owner"), ReportDesignerPermissions.ManageAllReportDesigns, view, Design));
    }

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal user, Permission permission, object resource, params string[] granted)
    {
        // The handler asks the authorization service whether the user can design, so it is given one that only knows
        // the permissions the test grants.
        var permissions = new FakeAuthorizationService(granted);
        var handler = new ReportDesignAuthorizationHandler(new Lazy<IAuthorizationService>(() => permissions));
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], user, resource);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }
}
