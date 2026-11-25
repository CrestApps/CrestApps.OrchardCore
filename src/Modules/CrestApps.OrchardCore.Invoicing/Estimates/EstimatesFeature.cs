using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Migrations;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using CrestApps.OrchardCore.Invoicing.Drivers;

namespace CrestApps.OrchardCore.Invoicing.Estimates;

/// <summary>
/// Registers estimates: the services, the index and its migration, the admin pages, the public page, the e-mail and
/// document kinds, and the expiry sweep.
/// </summary>
[Feature(InvoicingConstants.Features.Estimates)]
public sealed class EstimatesStartup : StartupBase
{
    private static readonly LocalizationSource _kindTitle = LocalizationSource.Create<EstimatesStartup>("Estimates");

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddInvoicingEstimates()
            .AddIndexProvider<EstimateIndexProvider>()
            .AddDataMigration<EstimatesMigrations>()
            .AddRecipeExecutionStep<EstimatesStep>()
            .AddDisplayDriver<Estimate, EstimateDisplayDriver>()
            .AddDisplayDriver<EstimateEditViewModel, EstimateEditDisplayDriver>()
            .AddNavigationProvider<EstimatesAdminMenu>()
            .AddBusinessDocumentKind(InvoicingConstants.BusinessDocumentKinds.Estimate, _kindTitle)
            .AddCommerceEmailKind(
                InvoicingConstants.EmailKinds.EstimateSent,
                LocalizationSource.Create<EstimatesStartup>("Estimate sent to its customer"),
                LocalizationSource.Create<EstimatesStartup>("{{ Document }}: the estimate, such as {{ Document.Number }}; {{ Number }}, {{ Total }}, {{ DueDate }} (valid until), {{ ViewUrl }} (to accept or decline it), {{ CustomerName }} and {{ StoreName }}. The PDF is attached."));

        services.AddSingleton<IBackgroundTask, EstimateExpiryBackgroundTask>();
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: InvoicingConstants.RouteNames.EstimateView,
            areaName: InvoicingConstants.Features.Area,
            pattern: "estimates/view/{token}",
            defaults: new { controller = "Estimate", action = "Show" });

        routes.MapAreaControllerRoute(
            name: InvoicingConstants.RouteNames.EstimateView + "Pdf",
            areaName: InvoicingConstants.Features.Area,
            pattern: "estimates/view/{token}/pdf",
            defaults: new { controller = "Estimate", action = "Pdf" });

        routes.MapAreaControllerRoute(
            name: InvoicingConstants.RouteNames.EstimateView + "Accept",
            areaName: InvoicingConstants.Features.Area,
            pattern: "estimates/view/{token}/accept",
            defaults: new { controller = "Estimate", action = "Accept" });

        routes.MapAreaControllerRoute(
            name: InvoicingConstants.RouteNames.EstimateView + "Decline",
            areaName: InvoicingConstants.Features.Area,
            pattern: "estimates/view/{token}/decline",
            defaults: new { controller = "Estimate", action = "Decline" });
    }
}

/// <summary>
/// Expires, once a day, the sent estimates whose expiry date has passed, a day in the site's time zone.
/// </summary>
[BackgroundTask(
    Title = "Estimate Expiry",
    Schedule = "15 * * * *",
    Description = "Marks sent estimates expired once their expiry date has passed.",
    LockTimeout = 5_000,
    LockExpiration = 60_000)]
public sealed class EstimateExpiryBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run =>
        {
            var localClock = serviceProvider.GetRequiredService<ILocalClock>();
            var today = DateOnly.FromDateTime((await localClock.GetLocalNowAsync()).DateTime);

            run.AddItems(await serviceProvider.GetRequiredService<IEstimateCommandService>().ExpireDueAsync(today, cancellationToken), "expired");
        });
}

/// <summary>
/// Adds Commerce > Estimates, beside Invoices.
/// </summary>
public sealed class EstimatesAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", InvoicingConstants.Features.Area },
        { "controller", "EstimatesAdmin" },
        { "action", "Index" },
    };

    internal readonly IStringLocalizer S;

    public EstimatesAdminMenu(IStringLocalizer<EstimatesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Estimates"], S["Estimates"].PrefixPosition("4.1"), estimates => estimates
                    .Id("estimates")
                    .Action("Index", "EstimatesAdmin", _routeValues)
                    .Permission(InvoicingPermissions.ViewInvoices)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The row of an estimate in its admin list, in the List and Grid layouts.
/// </summary>
public sealed class EstimateDisplayDriver : DisplayDriver<Estimate>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public EstimateDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(Estimate estimate, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        var model = new EstimateRowViewModel
        {
            Estimate = estimate,
            BillToName = estimate.BillTo?.Contact?.Name,
            CanEdit = estimate.Status == EstimateStatus.Draft &&
                await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InvoicingPermissions.ManageEstimates),
        };

        return Combine(
            View("Estimate_Fields_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Number:1"),
            View("Estimate_Status_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("Estimate_Meta_SummaryAdmin", model)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Amount:5"),
            View("Estimate_Buttons_SummaryAdmin", model).Location("Actions:5"));
    }
}
