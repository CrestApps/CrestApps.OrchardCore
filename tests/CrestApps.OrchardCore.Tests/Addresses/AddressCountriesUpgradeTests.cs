using CrestApps.OrchardCore.Addresses.Migrations;
using OrchardCore.Data.Migration.Records;

namespace CrestApps.OrchardCore.Tests.Addresses;

/// <summary>
/// A site that ran the Addresses migration of an earlier 3.0 preview keeps its managed countries; a new site doesn't
/// get them.
/// </summary>
public sealed class AddressCountriesUpgradeTests
{
    [Fact]
    public void ASiteThatRanTheOldCountryMigration_IsUpgraded()
    {
        var record = new DataMigrationRecord();
        record.DataMigrations.Add(new DataMigration { DataMigrationClass = typeof(AddressMigrations).FullName, Version = 3 });

        Assert.True(AddressCountriesUpgradeMigrations.HasRunCountriesMigration(record));
    }

    [Fact]
    public void ANewSite_IsNotUpgraded()
    {
        var record = new DataMigrationRecord();
        record.DataMigrations.Add(new DataMigration { DataMigrationClass = "OrchardCore.Contents.Migrations", Version = 1 });

        Assert.False(AddressCountriesUpgradeMigrations.HasRunCountriesMigration(record));
        Assert.False(AddressCountriesUpgradeMigrations.HasRunCountriesMigration(null));
    }
}
