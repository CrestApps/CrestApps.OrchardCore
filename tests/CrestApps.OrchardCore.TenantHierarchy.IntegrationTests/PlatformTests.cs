using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Covers what the Default tenant does with the hierarchy: the tree, making parents, the removal guard, parent-wide
/// suspend, resume and removal, and moving a child to another parent.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed class PlatformTests
{
    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public PlatformTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Overview_ShowsParentsWithTheirChildrenAndTheCandidates()
    {
        // Act
        var overview = await InPlatformAsync(platform => Task.FromResult(platform.GetOverview()));

        // Assert
        var firmA = Assert.Single(overview.Parents, parent => parent.Settings.Name == TenantHierarchyFixture.FirmA);
        Assert.Equal("Firm A", firmA.DisplayName);
        Assert.Contains(firmA.Children, child => child.Settings.Name == _fixture.BusinessOne.TenantName);
        Assert.DoesNotContain(firmA.Children, child => child.Settings.Name == _fixture.BusinessFour.TenantName);
        Assert.Contains(overview.Candidates, candidate => candidate.Name == TenantHierarchyFixture.Plain);
        Assert.DoesNotContain(overview.Candidates, candidate => candidate.Name == ShellSettings.DefaultShellName);
    }

    [Fact]
    public async Task ParentDetail_ForAConsistentParent_HasNoWarnings()
    {
        // Act
        var detail = await InPlatformAsync(platform => platform.GetParentDetailAsync(TenantHierarchyFixture.FirmB));

        // Assert
        Assert.Empty(detail.Warnings);
        Assert.All(detail.Children, child => Assert.Empty(child.Warnings));
    }

    [Fact]
    public void MadeParent_HasItsHostItsPolicyAndTheForcedParentFeature()
    {
        // Act
        var firmA = _fixture.Host.GetSettings(TenantHierarchyFixture.FirmA);

        // Assert
        Assert.True(firmA.IsParentTenant());
        Assert.Equal("firma.localhost", firmA.RequestUrlHost);
        Assert.Equal("firma", firmA.GetHierarchySlug());
        Assert.Equal("Firm A", firmA.GetHierarchyDisplayName());
        Assert.Contains(TenantHierarchyConstants.Features.Parent, TenantHierarchySettingsWriter.GetAlwaysEnabledFeatures(firmA));
    }

    [Fact]
    public async Task MakeParent_OnTheDefaultTenantOrAChild_IsRefused()
    {
        // Act
        var (onDefault, _) = await InPlatformAsync(platform => platform.MakeParentAsync(ShellSettings.DefaultShellName, "platform-root", "Root", new ParentTenantPolicy()));
        var (onChild, _) = await InPlatformAsync(platform => platform.MakeParentAsync(_fixture.BusinessOne.TenantName, "nested", "Nested", new ParentTenantPolicy()));

        // Assert
        Assert.False(onDefault.Succeeded);
        Assert.False(onChild.Succeeded);
    }

    [Fact]
    public async Task MakeParent_WithAnInvalidPolicy_ReportsEachProblem()
    {
        // Arrange
        await _fixture.Host.CreateTenantAsync("candidate1", null);

        // Act
        var (result, errors) = await InPlatformAsync(platform => platform.MakeParentAsync("candidate1", "candidate-one", "Candidate", new ParentTenantPolicy
        {
            ChildHostPattern = "*.evil.com",
            DatabaseStrategy = ChildDatabaseStrategy.DatabasePerChild,
            DatabasePool = "missing",
            MaxChildren = -1,
        }));

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(errors.ContainsKey(nameof(ParentTenantPolicy.ChildHostPattern)));
        Assert.True(errors.ContainsKey(nameof(ParentTenantPolicy.DatabasePool)));
        Assert.True(errors.ContainsKey(nameof(ParentTenantPolicy.MaxChildren)));
        Assert.False(_fixture.Host.GetSettings("candidate1").IsParentTenant());
    }

    [Fact]
    public async Task UnmakeParent_WithChildren_IsRefused()
    {
        // Act
        var result = await InPlatformAsync(platform => platform.UnmakeParentAsync(TenantHierarchyFixture.FirmA));

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(_fixture.Host.GetSettings(TenantHierarchyFixture.FirmA).IsParentTenant());
    }

    [Fact]
    public async Task RemovingAParentWithChildren_FromTheTenantsAdmin_IsRefusedBeforeAnyTableIsDropped()
    {
        // Arrange: Default disables firm B, as the Tenants admin would before removing it.
        var firmB = _fixture.Host.GetSettings(TenantHierarchyFixture.FirmB);
        await _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, services =>
            services.GetRequiredService<IShellHost>().UpdateShellSettingsAsync(firmB.AsDisabled()));

        try
        {
            // Act
            var context = await _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, services =>
                services.GetRequiredService<IShellRemovalManager>().RemoveAsync(_fixture.Host.GetSettings(TenantHierarchyFixture.FirmB)));

            // Assert
            Assert.False(context.Success);
            Assert.Contains(ParentRemovalGuard.ErrorMessage, context.ErrorMessage, StringComparison.Ordinal);
            Assert.NotNull(_fixture.Host.GetSettings(TenantHierarchyFixture.FirmB));
        }
        finally
        {
            await _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, services =>
                services.GetRequiredService<IShellHost>().UpdateShellSettingsAsync(_fixture.Host.GetSettings(TenantHierarchyFixture.FirmB).AsRunning()));
        }

        // The registry of firm B was not dropped.
        var entries = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmB, services =>
            services.GetRequiredService<ChildTenantEntryStore>().ListAsync());
        Assert.Contains(entries, entry => entry.EntryId == _fixture.BusinessFour.EntryId);
    }

    [Fact]
    public async Task SuspendAndResumeParent_SuspendsAndResumesItsRunningChildren()
    {
        // Act
        var suspend = await InPlatformAsync(platform => platform.SuspendParentAsync(TenantHierarchyFixture.FirmB));
        var parentSuspended = _fixture.Host.GetSettings(TenantHierarchyFixture.FirmB).IsDisabled();
        var childSuspended = _fixture.Host.GetSettings(_fixture.BusinessFour.TenantName).IsDisabled();

        var resume = await InPlatformAsync(platform => platform.ResumeParentAsync(TenantHierarchyFixture.FirmB));

        // Assert
        Assert.True(suspend.Succeeded);
        Assert.True(parentSuspended);
        Assert.True(childSuspended);
        Assert.True(resume.Succeeded);
        Assert.True(_fixture.Host.GetSettings(TenantHierarchyFixture.FirmB).IsRunning());
        Assert.True(_fixture.Host.GetSettings(_fixture.BusinessFour.TenantName).IsRunning());
        Assert.Null(_fixture.Host.GetSettings(_fixture.BusinessFour.TenantName)[TenantHierarchyConstants.SettingsKeys.SuspendedWithParent]);
    }

    [Fact]
    public async Task MoveChild_ToAnotherParent_MovesItsHostAndRegistryEntry()
    {
        // Arrange
        var entry = await _fixture.CreateChildAsync(TenantHierarchyFixture.FirmA, "Moving Business", "moving");

        // Act
        var result = await InPlatformAsync(platform => platform.MoveChildAsync(entry.TenantName, TenantHierarchyFixture.FirmB));

        // Assert
        Assert.True(result.Succeeded, result.Error);

        var child = _fixture.Host.GetSettings(entry.TenantName);
        Assert.True(child.IsChildOf(_fixture.Host.GetSettings(TenantHierarchyFixture.FirmB)));
        Assert.Equal("moving.firmb.localhost", child.RequestUrlHost);
        Assert.Equal("Firm B", child[TenantHierarchyConstants.SettingsKeys.Category]);

        var inFirmA = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ChildTenantEntryStore>().FindByTenantIdAsync(child.TenantId));
        var inFirmB = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmB, services =>
            services.GetRequiredService<ITenantHierarchyBroker>().ListChildrenAsync());

        Assert.Null(inFirmA);
        var moved = Assert.Single(inFirmB, info => info.Entry.TenantId == child.TenantId);
        Assert.Equal(ChildTenantRuntimeState.Running, moved.State);
        Assert.Equal(child.GetHierarchyEntryId(), moved.Entry.EntryId);

        // Clean up from the new parent.
        await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmB, async services =>
        {
            var manager = services.GetRequiredService<ChildTenantManager>();
            await manager.SuspendAsync(moved.Entry.EntryId);
            await manager.RemoveAsync(moved.Entry.EntryId);
        });
    }

    [Fact]
    public async Task RemoveParent_RemovesEachChildThenTheParent()
    {
        // Arrange: a third parent with one child.
        await _fixture.Host.CreateTenantAsync("firmc", "firmc.localhost");
        await _fixture.Host.SetupTenantAsync("firmc", "Blank", "cora", _fixture.Password);
        var (made, _) = await InPlatformAsync(platform => platform.MakeParentAsync("firmc", "firmc", "Firm C", new ParentTenantPolicy()));
        Assert.True(made.Succeeded, made.Error);
        var child = await _fixture.CreateChildAsync("firmc", "Business Of C", "business-c");

        // Act: a running parent cannot be removed; a suspended one is removed with its children.
        var whileRunning = await InPlatformAsync(platform => platform.RemoveParentAsync("firmc"));
        await InPlatformAsync(platform => platform.SuspendParentAsync("firmc"));
        var removal = await InPlatformAsync(platform => platform.RemoveParentAsync("firmc"));

        // Assert
        Assert.False(whileRunning.Succeeded);
        Assert.True(removal.Succeeded, removal.Error);
        Assert.Null(_fixture.Host.GetSettings("firmc"));
        Assert.Null(_fixture.Host.GetSettings(child.TenantName));
    }

    private Task<T> InPlatformAsync<T>(Func<TenantHierarchyPlatformService, Task<T>> operation)
        => _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, services => operation(services.GetRequiredService<TenantHierarchyPlatformService>()));
}
