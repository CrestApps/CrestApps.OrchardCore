using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.Receipts.Migrations;
using Moq;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Receipts;

/// <summary>
/// Receipts now depend on Business Documents. Orchard Core loads a new dependency of an enabled feature but does not
/// record it as enabled, so a site that had Receipts on before must get Business Documents turned on by a migration.
/// </summary>
public sealed class ReceiptsBusinessDocumentsFeatureMigrationsTests
{
    [Fact]
    public async Task NeedsEnabling_OnAnExistingSiteWithoutBusinessDocuments_IsTrue()
    {
        // Act
        var result = await ReceiptsBusinessDocumentsFeatureMigrations.NeedsEnablingAsync(Settings(TenantState.Running), Features("CrestApps.OrchardCore.Receipts"));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task NeedsEnabling_WhenBusinessDocumentsIsAlreadyOn_IsFalse()
    {
        // Act
        var result = await ReceiptsBusinessDocumentsFeatureMigrations.NeedsEnablingAsync(Settings(TenantState.Running), Features("CrestApps.OrchardCore.Receipts", BusinessDocumentsConstants.Features.Area));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task NeedsEnabling_WhileANewSiteIsSetUp_IsFalse()
    {
        // Act
        var result = await ReceiptsBusinessDocumentsFeatureMigrations.NeedsEnablingAsync(Settings(TenantState.Initializing), Features());

        // Assert
        Assert.False(result);
    }

    private static ShellSettings Settings(TenantState state)
        => new()
        {
            Name = "Default",
            State = state,
        };

    private static IShellFeaturesManager Features(params string[] enabled)
    {
        var manager = new Mock<IShellFeaturesManager>();
        manager.Setup(candidate => candidate.GetEnabledFeaturesAsync())
            .ReturnsAsync(enabled.Select(id => Mock.Of<IFeatureInfo>(feature => feature.Id == id)).ToArray());

        return manager.Object;
    }
}
