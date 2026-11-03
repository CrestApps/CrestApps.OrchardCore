using CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracking;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Migrations;

/// <summary>
/// Gives the usual agent states a Time Tracker stage the first time the feature runs.
/// </summary>
internal sealed class ContactCenterTimeTrackerMigrations : DataMigration
{
    private readonly ContactCenterTimeStageSeeder _seeder;
    private readonly ISiteService _siteService;

    public ContactCenterTimeTrackerMigrations(ContactCenterTimeStageSeeder seeder, ISiteService siteService)
    {
        _seeder = seeder;
        _siteService = siteService;
    }

    public async Task<int> CreateAsync()
    {
        var site = await _siteService.LoadSiteSettingsAsync();

        site.Put(await _seeder.BuildAsync(site.GetOrCreate<ContactCenterTimeStages>()));
        await _siteService.UpdateSiteSettingsAsync(site);

        return 1;
    }
}
