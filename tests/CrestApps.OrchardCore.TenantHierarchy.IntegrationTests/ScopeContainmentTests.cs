using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Proves goal G8: a parent reaches only itself and its own children, a child reaches only itself, and an ordinary
/// tenant never reaches a parent or a child, whatever code runs in them.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed class ScopeContainmentTests
{
    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeContainmentTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public ScopeContainmentTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetAllSettings_FromParent_ListsOnlyTheParentAndItsOwnChildren()
    {
        // Arrange
        var firmA = _fixture.Host.GetSettings(TenantHierarchyFixture.FirmA);

        // Act
        var listed = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            Task.FromResult(services.GetRequiredService<IShellHost>().GetAllSettings().ToList()));

        // Assert
        Assert.Contains(listed, settings => settings.Name == TenantHierarchyFixture.FirmA);
        Assert.Contains(listed, settings => settings.Name == _fixture.BusinessOne.TenantName);
        Assert.Contains(listed, settings => settings.Name == _fixture.BusinessTwo.TenantName);
        Assert.All(listed, settings => Assert.True(settings.Name == TenantHierarchyFixture.FirmA || settings.IsChildOf(firmA)));
    }

    [Fact]
    public async Task GetAllSettings_FromChild_ListsOnlyTheChild()
    {
        // Act
        var names = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, services =>
            Task.FromResult(services.GetRequiredService<IShellHost>().GetAllSettings().Select(settings => settings.Name).ToArray()));

        // Assert
        Assert.Equal([_fixture.BusinessOne.TenantName], names);
    }

    [Fact]
    public async Task GetAllSettings_FromOrdinaryTenant_HidesEveryParentAndChild()
    {
        // Act
        var names = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.Plain, services =>
            Task.FromResult(services.GetRequiredService<IShellHost>().GetAllSettings().Select(settings => settings.Name).ToArray()));

        // Assert
        Assert.Contains(TenantHierarchyFixture.Plain, names);
        Assert.Contains(ShellSettings.DefaultShellName, names);
        Assert.DoesNotContain(TenantHierarchyFixture.FirmA, names);
        Assert.DoesNotContain(TenantHierarchyFixture.FirmB, names);
        Assert.DoesNotContain(_fixture.BusinessOne.TenantName, names);
        Assert.DoesNotContain(_fixture.BusinessFour.TenantName, names);
    }

    [Fact]
    public async Task GetAllSettings_FromDefault_ListsEveryTenant()
    {
        // Act
        var names = await _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, services =>
            Task.FromResult(services.GetRequiredService<IShellHost>().GetAllSettings().Select(settings => settings.Name).ToArray()));

        // Assert
        Assert.Contains(TenantHierarchyFixture.FirmA, names);
        Assert.Contains(TenantHierarchyFixture.FirmB, names);
        Assert.Contains(TenantHierarchyFixture.Plain, names);
        Assert.Contains(_fixture.BusinessOne.TenantName, names);
        Assert.Contains(_fixture.BusinessFour.TenantName, names);
    }

    [Theory]
    [InlineData(TenantHierarchyFixture.FirmB)]
    [InlineData(TenantHierarchyFixture.Plain)]
    public async Task TryGetSettings_FromParent_HidesTenantsOutsideItsHierarchy(string target)
    {
        // Act
        var found = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            Task.FromResult(services.GetRequiredService<IShellHost>().TryGetSettings(target, out _)));

        // Assert
        Assert.False(found);
    }

    [Fact]
    public async Task TryGetSettings_FromParent_HidesAnotherParentsChild()
    {
        // Act
        var found = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            Task.FromResult(services.GetRequiredService<IShellHost>().TryGetSettings(_fixture.BusinessFour.TenantName, out _)));

        // Assert
        Assert.False(found);
    }

    [Fact]
    public async Task TryGetSettings_FromChild_HidesItsParentAndSiblings()
    {
        // Act
        var (parent, sibling) = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, services =>
        {
            var shellHost = services.GetRequiredService<IShellHost>();

            return Task.FromResult((shellHost.TryGetSettings(TenantHierarchyFixture.FirmA, out _), shellHost.TryGetSettings(_fixture.BusinessTwo.TenantName, out _)));
        });

        // Assert
        Assert.False(parent);
        Assert.False(sibling);
    }

    [Fact]
    public async Task GetScopeAsync_FromParentOnItsOwnChildOutsideTheBroker_Throws()
    {
        // Arrange
        var child = _fixture.Host.GetSettings(_fixture.BusinessOne.TenantName);

        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            await services.GetRequiredService<IShellHost>().GetScopeAsync(child);
        }));

        // Assert
        Assert.IsType<TenantHierarchyAccessDeniedException>(exception);
    }

    [Theory]
    [InlineData(TenantHierarchyFixture.FirmB)]
    [InlineData(TenantHierarchyFixture.Plain)]
    public async Task GetScopeAsync_FromParentOnAnotherTenant_Throws(string target)
    {
        // Arrange
        var settings = _fixture.Host.GetSettings(target);

        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            await services.GetRequiredService<IShellHost>().GetScopeAsync(settings);
        }));

        // Assert
        Assert.IsType<TenantHierarchyAccessDeniedException>(exception);
    }

    [Fact]
    public async Task ChangingAnotherParentsChild_FromParent_Throws()
    {
        // Arrange
        var victim = _fixture.Host.GetSettings(_fixture.BusinessFour.TenantName);

        // Act
        var update = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<IShellHost>().UpdateShellSettingsAsync(victim)));
        var reload = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<IShellHost>().ReloadShellContextAsync(victim)));
        var remove = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<IShellHost>().RemoveShellSettingsAsync(victim)));

        // Assert
        Assert.IsType<TenantHierarchyAccessDeniedException>(update);
        Assert.IsType<TenantHierarchyAccessDeniedException>(reload);
        Assert.IsType<TenantHierarchyAccessDeniedException>(remove);
        Assert.True(_fixture.Host.GetSettings(_fixture.BusinessFour.TenantName).IsRunning());
    }

    [Fact]
    public async Task CraftedSettings_FromParentClaimingAnotherParentsChild_Throws()
    {
        // Arrange: a settings object that names firm B's child but claims to belong to firm A.
        var firmA = _fixture.Host.GetSettings(TenantHierarchyFixture.FirmA);
        var crafted = new ShellSettings(_fixture.Host.GetSettings(_fixture.BusinessFour.TenantName));
        crafted[TenantHierarchyConstants.SettingsKeys.ParentTenantId] = firmA.TenantId;

        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<IShellHost>().ReloadShellContextAsync(crafted)));

        // Assert
        Assert.IsType<TenantHierarchyAccessDeniedException>(exception);
    }

    [Theory]
    [InlineData(TenantHierarchyFixture.FirmA)]
    [InlineData(TenantHierarchyFixture.Plain)]
    public async Task GetScopeAsync_FromChildOnItsParentOrAnotherTenant_Throws(string target)
    {
        // Arrange
        var settings = _fixture.Host.GetSettings(target);

        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            await services.GetRequiredService<IShellHost>().GetScopeAsync(settings);
        }));

        // Assert
        Assert.IsType<TenantHierarchyAccessDeniedException>(exception);
    }

    [Fact]
    public async Task GetScopeAsync_FromChildOnSibling_Throws()
    {
        // Arrange
        var sibling = _fixture.Host.GetSettings(_fixture.BusinessTwo.TenantName);

        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            await services.GetRequiredService<IShellHost>().GetScopeAsync(sibling);
        }));

        // Assert
        Assert.IsType<TenantHierarchyAccessDeniedException>(exception);
    }

    [Fact]
    public async Task GetScopeAsync_FromOrdinaryTenantOnAChild_Throws()
    {
        // Arrange
        var child = _fixture.Host.GetSettings(_fixture.BusinessOne.TenantName);

        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(TenantHierarchyFixture.Plain, async services =>
        {
            await services.GetRequiredService<IShellHost>().GetScopeAsync(child);
        }));

        // Assert
        Assert.IsType<TenantHierarchyAccessDeniedException>(exception);
    }

    [Fact]
    public async Task GetScopeAsync_FromAnyTenantOnDefault_IsAllowed()
    {
        // Arrange
        var defaultSettings = _fixture.Host.GetSettings(ShellSettings.DefaultShellName);

        // Act
        var opened = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            var scope = await services.GetRequiredService<IShellHost>().GetScopeAsync(defaultSettings);
            var tenant = string.Empty;
            await scope.UsingAsync(shellScope =>
            {
                tenant = shellScope.ShellContext.Settings.Name;

                return Task.CompletedTask;
            });

            return tenant;
        });

        // Assert
        Assert.Equal(ShellSettings.DefaultShellName, opened);
    }
}
