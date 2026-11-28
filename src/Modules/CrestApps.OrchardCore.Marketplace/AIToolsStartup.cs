using CrestApps.Core.AI;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Marketplace.AI;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Marketplace;

/// <summary>
/// Registers the marketplace AI tools when the AI feature is on: <c>getVendor</c>, and vendors as a read-only kind of the
/// generic record tools (<c>listRecords</c>, <c>getRecord</c>). Vendors are not saved through the record tools: their
/// editor is the vendors controller's own form, not a display driver the tools can replay.
/// </summary>
[Feature(MarketplaceConstants.Feature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetVendorTool>(GetVendorTool.TheName)
            .WithTitle(S["Get Vendor"])
            .WithDescription(S["Returns one vendor with its totals per currency (held, available, paid out), its recent earnings per order and its payouts with any failure message. Use it to answer what the store owes a vendor."])
            .WithCategory(S["Marketplace"])
            .Selectable();

        services.AddAIRecordKind<Vendor>("vendor", kind => kind
            .WithTitle(S["Vendor"])
            .WithDescription(S["a marketplace vendor that sells through the store: name, email, status (active or suspended), commission percent (empty uses the store's), country, the user names that manage it and whether its payout account is set up; search matches the start of the name"])
            .WithPermission(MarketplacePermissions.ManageMarketplace)
            .ReadOnly()
            .FindUsing((serviceProvider, id, cancellationToken) => serviceProvider.GetRequiredService<IVendorStore>().FindByIdAsync(id, cancellationToken))
            .ListUsing(ListVendorsAsync)
            .WithProjection(GetVendorTool.Describe));
    }

    // As the vendors page lists them: by name, the search matching the start of the name.
    private static async ValueTask<PageResult<Vendor>> ListVendorsAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Max(1, query.PageSize);
        var (count, vendors) = await services.GetRequiredService<IVendorStore>().PageAsync(query.Text, (page - 1) * pageSize, pageSize, cancellationToken);

        return new PageResult<Vendor>
        {
            Count = count,
            Entries = vendors,
        };
    }
}
