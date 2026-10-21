using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// The Omnichannel report filter gained a disposition picker that takes several dispositions, a page picker for the
/// reports that page their rows, and report defaults that apply only until the form is first submitted. These pin what
/// the driver stores from the form and what it offers the view.
/// </summary>
public sealed class OmnichannelReportFilterDisplayDriverTests
{
    private const string OtherReportName = "omnichannel-activity-summary";

    [Fact]
    public async Task UpdateAsync_StoresTheDispositionsPickedTogether_AndThePage()
    {
        // Arrange
        var filter = new ReportFilter
        {
            ReportName = DialerCallDetailReportProvider.ReportName,
        };
        var driver = CreateDriver(CreateDialerReport(), "?Source=&DispositionIds=no-answer&DispositionIds=busy&Page=3");
        var posted = new OmnichannelReportFilterViewModel
        {
            DispositionIds = ["no-answer", " busy ", "", "busy"],
            Page = 3,
        };

        // Act
        await driver.UpdateAsync(filter, PostedFormUpdateModel.CreateContext(posted));

        // Assert
        Assert.Equal(["no-answer", "busy"], filter.GetOrDefault<string[]>(OmnichannelReportFilter.DispositionIds));
        Assert.Equal(3, filter.GetOrDefault<int>(OmnichannelReportFilter.Page));
    }

    [Fact]
    public async Task UpdateAsync_WithNoDispositionAndTheFirstPage_StoresNeither()
    {
        // Arrange
        var filter = new ReportFilter
        {
            ReportName = DialerCallDetailReportProvider.ReportName,
        };
        var driver = CreateDriver(CreateDialerReport(), "?Source=&Page=1");
        var posted = new OmnichannelReportFilterViewModel
        {
            DispositionIds = [],
            Page = 1,
        };

        // Act
        await driver.UpdateAsync(filter, PostedFormUpdateModel.CreateContext(posted));

        // Assert
        Assert.False(filter.Properties.ContainsKey(OmnichannelReportFilter.DispositionIds));
        Assert.False(filter.Properties.ContainsKey(OmnichannelReportFilter.Page));
    }

    [Fact]
    public async Task UpdateAsync_WhenTheReportIsOpenedForTheFirstTime_StartsOnTheReportDefaults()
    {
        // Arrange
        // Nothing has been submitted: the request carries none of the filter fields.
        var filter = new ReportFilter
        {
            ReportName = DialerCallDetailReportProvider.ReportName,
        };
        var driver = CreateDriver(CreateDialerReport(), string.Empty);

        // Act
        await driver.UpdateAsync(filter, PostedFormUpdateModel.CreateContext(new OmnichannelReportFilterViewModel()));

        // Assert
        Assert.Equal(ActivitySources.Dialer, filter.GetOrDefault<string>(OmnichannelReportFilter.Source));
    }

    [Fact]
    public async Task UpdateAsync_WhenAllSourcesWasChosen_KeepsAllSourcesInsteadOfTheDefault()
    {
        // Arrange
        // The form was submitted with the Source list on its empty "All sources" option.
        var filter = new ReportFilter
        {
            ReportName = DialerCallDetailReportProvider.ReportName,
        };
        var driver = CreateDriver(CreateDialerReport(), "?Source=");

        // Act
        await driver.UpdateAsync(filter, PostedFormUpdateModel.CreateContext(new OmnichannelReportFilterViewModel()));

        // Assert
        Assert.False(filter.Properties.ContainsKey(OmnichannelReportFilter.Source));
    }

    [Fact]
    public async Task UpdateAsync_ForAReportWithoutDefaults_LeavesTheFilterAsItIs()
    {
        // Arrange
        var filter = new ReportFilter
        {
            ReportName = OtherReportName,
        };
        var driver = CreateDriver(Mock.Of<IReportRunner>(), string.Empty);

        // Act
        await driver.UpdateAsync(filter, PostedFormUpdateModel.CreateContext(new OmnichannelReportFilterViewModel()));

        // Assert
        Assert.False(filter.Properties.ContainsKey(OmnichannelReportFilter.Source));
    }

    [Fact]
    public async Task Edit_ShowsThePagePicker_OnlyForAReportThatPagesItsRows()
    {
        // Arrange
        var pagedFilter = new ReportFilter
        {
            ReportName = DialerCallDetailReportProvider.ReportName,
        };
        var otherFilter = new ReportFilter
        {
            ReportName = OtherReportName,
        };
        var context = PostedFormUpdateModel.CreateContext(new OmnichannelReportFilterViewModel());

        // Act
        var paged = Assert.Single(await DisplayResultModels.BuildAsync<OmnichannelReportFilterViewModel>(
            CreateDriver(CreateDialerReport(), string.Empty).Edit(pagedFilter, context)));
        var other = Assert.Single(await DisplayResultModels.BuildAsync<OmnichannelReportFilterViewModel>(
            CreateDriver(Mock.Of<IReportRunner>(), string.Empty).Edit(otherFilter, context)));

        // Assert
        Assert.True(paged.ShowPage);
        Assert.False(other.ShowPage);
    }

    [Fact]
    public async Task Edit_OffersEveryDisposition_AndKeepsThePickedOnes()
    {
        // Arrange
        var filter = new ReportFilter
        {
            ReportName = DialerCallDetailReportProvider.ReportName,
        };
        filter.Set<string[]>(OmnichannelReportFilter.DispositionIds, ["busy"]);
        filter.Set(OmnichannelReportFilter.Page, 2);
        var context = PostedFormUpdateModel.CreateContext(new OmnichannelReportFilterViewModel());

        // Act
        var model = Assert.Single(await DisplayResultModels.BuildAsync<OmnichannelReportFilterViewModel>(
            CreateDriver(CreateDialerReport(), string.Empty).Edit(filter, context)));

        // Assert
        Assert.Equal(["Busy", "No answer"], model.Dispositions.Select(item => item.Text));
        Assert.Equal(["busy", "no-answer"], model.Dispositions.Select(item => item.Value));
        Assert.Equal(["busy"], model.DispositionIds);
        Assert.Equal(2, model.Page);
    }

    private static DialerCallDetailReportProvider CreateDialerReport()
    {
        return new DialerCallDetailReportProvider(
            Mock.Of<ISession>(),
            Mock.Of<ICatalogManager<OmnichannelCampaign>>(),
            Mock.Of<INamedCatalogManager<OmnichannelDisposition>>(),
            Options.Create(new ActivitySourceOptions()),
            new PassThroughStringLocalizer<DialerCallDetailReportProvider>());
    }

    private static OmnichannelReportFilterDisplayDriver CreateDriver(IReportRunner report, string queryString)
    {
        var campaignManager = new Mock<ICatalogManager<OmnichannelCampaign>>();
        campaignManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var campaignGroupManager = new Mock<ICatalogManager<OmnichannelCampaignGroup>>();
        campaignGroupManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var dispositionManager = new Mock<INamedCatalogManager<OmnichannelDisposition>>();
        dispositionManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelDisposition[]
            {
                new()
                {
                    ItemId = "no-answer",
                    Name = "No answer",
                },
                new()
                {
                    ItemId = "busy",
                    Name = "Busy",
                },
            });

        var reportManager = new Mock<IReportManager>();
        reportManager
            .Setup(manager => manager.GetReport(It.IsAny<string>()))
            .Returns(report);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString(queryString);

        var sourceOptions = new ActivitySourceOptions();
        sourceOptions.AddSource(ActivitySources.Dialer);
        sourceOptions.AddSource(ActivitySources.Manual);

        return new OmnichannelReportFilterDisplayDriver(
            campaignManager.Object,
            campaignGroupManager.Object,
            dispositionManager.Object,
            reportManager.Object,
            new HttpContextAccessor
            {
                HttpContext = httpContext,
            },
            Options.Create(sourceOptions),
            Options.Create(new ActivityChannelOptions()),
            new PassThroughStringLocalizer<OmnichannelReportFilterDisplayDriver>());
    }
}
