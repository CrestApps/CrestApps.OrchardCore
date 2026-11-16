using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Legacy;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center Business Hours feature: the open-hours check that gates work distribution, entry points,
/// the dialer and automated Omnichannel sends. The hours themselves are Business Hours schedules, kept and edited by
/// the Business Hours module this feature depends on; a queue's or entry point's calendar is a schedule id. It is a
/// standalone feature so that work distribution, the outbound dialer, and automated Omnichannel conversations can each
/// depend on it without depending on one another.
/// </summary>
[Feature(ContactCenterConstants.Feature.BusinessHours)]
public sealed class BusinessHoursStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The calendars this feature kept before the move live in the Contact Center storage collection, which the
        // upgrade reads. Register it here so the feature is self-contained when it is the only Contact Center feature
        // enabled (the base Area feature registers the same collection; the registration is idempotent).
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ContactCenterStorage.CollectionName));

        services
            .AddOpenHours()
            .AddScoped<IBusinessHoursService, DefaultBusinessHoursService>();

        // Omnichannel Activities registers the same gate; this feature registers it too, so the voice handoff and the
        // dialer find it when this feature is enabled without the activities.
        services.TryAddScoped<IBusinessHoursGate, OpenHoursBusinessHoursGate>();

        // Moves the calendars this feature kept to Business Hours schedules with the same ids.
        services
            .AddScoped<BusinessHoursCalendarMove>()
            .AddDataMigration<BusinessHoursCalendarIndexMigrations>();
    }
}

/// <summary>
/// Registers the recipe step that reads the business-hours calendars of older recipes as Business Hours schedules.
/// </summary>
[Feature(ContactCenterConstants.Feature.BusinessHours)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class BusinessHoursRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterBusinessHoursCalendarStep>();
    }
}
