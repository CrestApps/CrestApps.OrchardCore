using System.Security.Claims;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.Timeline.ViewComponents;
using CrestApps.OrchardCore.Timeline.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Timeline;

/// <summary>
/// The timeline card: it shows nothing to a user who may read neither kind of entry, only the internal entries to one
/// who may read only those, a button only for each kind the user may add, and older and newer pages that keep the
/// tenant's path base and the page's other query values.
/// </summary>
public sealed class TimelineViewComponentTests
{
    private static readonly RecordRef _order = new("Order", "o1");

    [Fact]
    public async Task AUserWhoMayReadNoEntry_SeesNothing()
    {
        // Act
        var result = await InvokeAsync(canViewPublic: false, canViewInternal: false, canAddNote: false);

        // Assert
        Assert.IsType<ContentViewComponentResult>(result);
    }

    [Fact]
    public async Task AUserWhoMayReadOnlyInternalEntries_SeesOnlyThose_AndOnlyTheButtonsTheyMayUse()
    {
        // Act
        var model = Model(await InvokeAsync(canViewPublic: false, canViewInternal: true, canAddNote: true));

        // Assert
        Assert.All(model.Entries, entry => Assert.Equal(TimelineVisibility.Internal, entry.Entry.Visibility));
        Assert.Equal([TimelineConstants.Kinds.Note], model.Composer.Select(button => button.Kind));
    }

    [Fact]
    public async Task OlderAndNewerPages_KeepThePathBase_AndTheOtherQueryValues()
    {
        // Arrange: 60 entries, the second page asked for.
        var key = TimelineViewComponent.PageKey(_order);

        // Act
        var model = Model(await InvokeAsync(canViewPublic: true, canViewInternal: true, canAddNote: true, entries: 60, query: $"?tab=notes&{key}=2"));

        // Assert
        Assert.Equal(10, model.Entries.Count);
        Assert.Equal(2, model.PageNumber);
        Assert.Null(model.OlderUrl);
        Assert.Equal("/tenant-a/Admin/orders/o1?tab=notes", model.NewerUrl);
        Assert.Equal("/tenant-a/Admin/orders/o1?tab=notes&" + key + "=2", model.ReturnUrl);
    }

    private static TimelineViewModel Model(IViewComponentResult result)
        => Assert.IsType<TimelineViewModel>(Assert.IsType<ViewViewComponentResult>(result).ViewData.Model);

    private static async Task<IViewComponentResult> InvokeAsync(bool canViewPublic, bool canViewInternal, bool canAddNote, int entries = 4, string query = "")
    {
        var timeline = new InMemoryTimelineService();

        for (var index = 0; index < entries; index++)
        {
            await timeline.AddAsync(new TimelineEntry
            {
                Subject = _order,
                Kind = index % 2 == 0 ? TimelineConstants.Kinds.Note : TimelineConstants.Kinds.Comment,
                Visibility = index % 2 == 0 || entries > 4 ? TimelineVisibility.Internal : TimelineVisibility.Public,
                Body = $"Entry {index}",
                CreatedUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(index),
            }, TestContext.Current.CancellationToken);
        }

        var access = new Mock<ITimelineAccessService>();
        access.Setup(service => service.CanViewAsync(It.IsAny<ClaimsPrincipal>(), _order, TimelineVisibility.Public, It.IsAny<CancellationToken>())).ReturnsAsync(canViewPublic);
        access.Setup(service => service.CanViewAsync(It.IsAny<ClaimsPrincipal>(), _order, TimelineVisibility.Internal, It.IsAny<CancellationToken>())).ReturnsAsync(canViewInternal);
        access.Setup(service => service.CanAddAsync(It.IsAny<ClaimsPrincipal>(), _order, TimelineConstants.Kinds.Note, TimelineVisibility.Internal, It.IsAny<CancellationToken>())).ReturnsAsync(canAddNote);
        access.Setup(service => service.CanAddAsync(It.IsAny<ClaimsPrincipal>(), _order, TimelineConstants.Kinds.Comment, TimelineVisibility.Public, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var parties = new Mock<IPartyDirectory>();
        parties.Setup(directory => directory.GetNamesAsync(It.IsAny<IEnumerable<PartyRef>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<PartyRef, string>());

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());

        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(Mock.Of<Microsoft.AspNetCore.Mvc.ViewEngines.ICompositeViewEngine>()).BuildServiceProvider() };
        httpContext.Request.PathBase = "/tenant-a";
        httpContext.Request.Path = "/Admin/orders/o1";
        httpContext.Request.QueryString = new QueryString(query);

        var component = new TimelineViewComponent(timeline, access.Object, parties.Object, authorization.Object, new SourceTextLocalizerFactory(), Options.Create(new TimelineOptions()))
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new ViewContext
                {
                    HttpContext = httpContext,
                    TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
                },
            },
        };

        return await component.InvokeAsync(_order.ToString());
    }
}
