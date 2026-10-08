using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Covers creating, setting up, editing, suspending, resuming, reloading and removing child tenants, and the rules
/// the parent policy enforces on them.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed class ChildTenantLifecycleTests
{
    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChildTenantLifecycleTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public ChildTenantLifecycleTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void CreatedChild_HasHostControlledSettingsThatNameItsParent()
    {
        // Arrange
        var parent = _fixture.Host.GetSettings(TenantHierarchyFixture.FirmA);

        // Act
        var child = _fixture.Host.GetSettings(_fixture.BusinessOne.TenantName);

        // Assert
        Assert.True(child.IsRunning());
        Assert.True(child.IsChildOf(parent));
        Assert.Equal("business1.firma.localhost", child.RequestUrlHost);
        Assert.Equal(string.Empty, child.RequestUrlPrefix);
        Assert.Equal("Firm A", child[TenantHierarchyConstants.SettingsKeys.Category]);
        Assert.Equal("Business One", child[TenantHierarchyConstants.SettingsKeys.Description]);
        Assert.Equal(_fixture.BusinessOne.EntryId, child.GetHierarchyEntryId());
        Assert.Equal(_fixture.BusinessOne.TenantId, child.TenantId);
        Assert.StartsWith("u_", child.Name, StringComparison.Ordinal);
        Assert.Contains(TenantHierarchyConstants.Features.Child, TenantHierarchySettingsWriter.GetAlwaysEnabledFeatures(child));
    }

    [Fact]
    public void CreatedChild_RegistryEntryIsReady()
    {
        // Assert
        Assert.Equal(ChildTenantStatus.Ready, _fixture.BusinessOne.Status);
        Assert.Equal("business1", _fixture.BusinessOne.Slug);
        Assert.Equal("business1.firma.localhost", _fixture.BusinessOne.Host);
        Assert.False(string.IsNullOrEmpty(_fixture.BusinessOne.BootstrapUserId));
    }

    [Fact]
    public async Task CreatedChild_DisablesTheBootstrapAdministrator()
    {
        // Act
        var bootstrap = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, services =>
            services.GetRequiredService<ISession>().Query<User, UserIndex>(index => index.UserId == _fixture.BusinessOne.BootstrapUserId).FirstOrDefaultAsync());

        // Assert
        Assert.NotNull(bootstrap);
        Assert.False(bootstrap.IsEnabled);
        Assert.StartsWith("hierarchy-bootstrap-", bootstrap.UserName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatedChild_HasTheChildFeatureAndNotTheBlockedFeaturesOfItsRecipe()
    {
        // Act: the Blank recipe asks for OrchardCore.Deployment.Remote, which children may not have.
        var enabled = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
            (await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToList());

        // Assert
        Assert.Contains(TenantHierarchyConstants.Features.Child, enabled);
        Assert.DoesNotContain("OrchardCore.Deployment.Remote", enabled);
        Assert.DoesNotContain(TenantHierarchyConstants.Features.Parent, enabled);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ab")]
    [InlineData("-business")]
    [InlineData("busi ness")]
    [InlineData("busi_ness")]
    [InlineData("xn--bsiness")]
    public async Task ValidateCreate_InvalidOrReservedSlug_IsRefused(string slug)
    {
        // Act
        var errors = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ChildTenantManager>().ValidateCreateAsync(new CreateChildTenantRequest
            {
                DisplayName = "Some business",
                Slug = slug,
                RecipeName = "Blank",
            }));

        // Assert
        Assert.True(errors.ContainsKey(nameof(CreateChildTenantRequest.Slug)));
    }

    [Fact]
    public async Task ValidateCreate_UppercaseSlug_IsNormalizedToLowercase()
    {
        // Act: "Business1" is "business1", which firm A already uses.
        var errors = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ChildTenantManager>().ValidateCreateAsync(new CreateChildTenantRequest
            {
                DisplayName = "Copy",
                Slug = "Business1",
                RecipeName = "Blank",
            }));

        // Assert
        Assert.Equal("This address is already in use.", errors[nameof(CreateChildTenantRequest.Slug)]);
    }

    [Fact]
    public async Task ValidateCreate_SlugOfAnExistingChild_IsRefused()
    {
        // Act
        var errors = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ChildTenantManager>().ValidateCreateAsync(new CreateChildTenantRequest
            {
                DisplayName = "Copy",
                Slug = "business1",
                RecipeName = "Blank",
            }));

        // Assert
        Assert.Equal("This address is already in use.", errors[nameof(CreateChildTenantRequest.Slug)]);
    }

    [Fact]
    public async Task ValidateCreate_SameSlugInAnotherParent_IsAllowed()
    {
        // Act: every parent has its own namespace, so firm B may use a slug firm A uses.
        var errors = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmB, services =>
            services.GetRequiredService<ChildTenantManager>().ValidateCreateAsync(new CreateChildTenantRequest
            {
                DisplayName = "Business One of B",
                Slug = "business1",
                RecipeName = "Blank",
            }));

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidateCreate_UnknownRecipe_IsRefused()
    {
        // Act
        var errors = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ChildTenantManager>().ValidateCreateAsync(new CreateChildTenantRequest
            {
                DisplayName = "Some business",
                Slug = "some-business",
                RecipeName = "DoesNotExist",
            }));

        // Assert
        Assert.True(errors.ContainsKey(nameof(CreateChildTenantRequest.RecipeName)));
    }

    [Fact]
    public async Task Create_WhenTheQuotaIsReached_IsRefused()
    {
        // Arrange: firm B already owns one child; lower its limit to one.
        await SetMaxChildrenAsync(TenantHierarchyFixture.FirmB, 1);

        try
        {
            // Act
            var (result, entry) = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmB, services =>
                services.GetRequiredService<ChildTenantManager>().CreateAsync(new CreateChildTenantRequest
                {
                    DisplayName = "Over the limit",
                    Slug = "over-the-limit",
                    RecipeName = "Blank",
                }));

            // Assert
            Assert.False(result.Succeeded);
            Assert.Null(entry);
            Assert.Contains("limit", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await SetMaxChildrenAsync(TenantHierarchyFixture.FirmB, 5);
        }
    }

    [Fact]
    public async Task Operations_WithAnEntryOfAnotherParent_FailAsNotFoundAndChangeNothing()
    {
        // Arrange: firm B's registry entry identifier, sent to firm A.
        var foreignEntryId = _fixture.BusinessFour.EntryId;

        // Act
        var results = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var broker = services.GetRequiredService<ITenantHierarchyBroker>();
            var manager = services.GetRequiredService<ChildTenantManager>();

            return new
            {
                Child = await broker.GetChildAsync(foreignEntryId),
                Suspend = await manager.SuspendAsync(foreignEntryId),
                Reload = await manager.ReloadAsync(foreignEntryId),
                Remove = await manager.RemoveAsync(foreignEntryId),
                Features = await broker.GetChildFeaturesAsync(foreignEntryId),
                Roles = await broker.GetChildRolesAsync(foreignEntryId),
            };
        });

        // Assert
        Assert.Null(results.Child);
        Assert.False(results.Suspend.Succeeded);
        Assert.False(results.Reload.Succeeded);
        Assert.False(results.Remove.Succeeded);
        Assert.Empty(results.Features);
        Assert.Empty(results.Roles);
        Assert.True(_fixture.Host.GetSettings(_fixture.BusinessFour.TenantName).IsRunning());
    }

    [Fact]
    public async Task SuspendResumeReloadAndRemove_FollowTheTenantsAdminRules()
    {
        // Arrange
        var entry = await _fixture.CreateChildAsync(TenantHierarchyFixture.FirmA, "Lifecycle Business", "lifecycle");
        var tenantName = entry.TenantName;

        // Act + Assert: a running child cannot be removed.
        var removeRunning = await InFirmAAsync(manager => manager.RemoveAsync(entry.EntryId));
        Assert.False(removeRunning.Succeeded);

        Assert.True((await InFirmAAsync(manager => manager.ReloadAsync(entry.EntryId))).Succeeded);
        Assert.True((await InFirmAAsync(manager => manager.SuspendAsync(entry.EntryId))).Succeeded);
        Assert.True(_fixture.Host.GetSettings(tenantName).IsDisabled());

        Assert.True((await InFirmAAsync(manager => manager.ResumeAsync(entry.EntryId))).Succeeded);
        Assert.True(_fixture.Host.GetSettings(tenantName).IsRunning());

        Assert.True((await InFirmAAsync(manager => manager.SuspendAsync(entry.EntryId))).Succeeded);
        var removal = await InFirmAAsync(manager => manager.RemoveAsync(entry.EntryId));
        Assert.True(removal.Succeeded, removal.Error);

        // The tenant, its registry entry and its grants are gone.
        Assert.Null(_fixture.Host.GetSettings(tenantName));
        var remaining = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ChildTenantEntryStore>().FindByEntryIdAsync(entry.EntryId));
        Assert.Null(remaining);
    }

    [Fact]
    public async Task Update_NewSlug_MovesTheChildToItsNewHost()
    {
        // Arrange
        var entry = await _fixture.CreateChildAsync(TenantHierarchyFixture.FirmA, "Renamed Business", "renamed-old");

        // Act
        var (result, errors) = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<ChildTenantManager>().UpdateAsync(entry.EntryId, "Renamed Business Ltd", "renamed-new", "A description"));

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(errors);
        var child = _fixture.Host.GetSettings(entry.TenantName);
        Assert.Equal("renamed-new.firma.localhost", child.RequestUrlHost);
        Assert.Equal("Renamed Business Ltd", child[TenantHierarchyConstants.SettingsKeys.Description]);
        Assert.Equal("renamed-new", child.GetHierarchySlug());

        await RemoveAsync(entry);
    }

    [Fact]
    public async Task RemovalGracePeriod_KeepsTheChildUntilItIsRestoredOrDue()
    {
        // Arrange
        await SetGraceDaysAsync(TenantHierarchyFixture.FirmA, 7);
        var entry = await _fixture.CreateChildAsync(TenantHierarchyFixture.FirmA, "Grace Business", "grace");

        try
        {
            await InFirmAAsync(manager => manager.SuspendAsync(entry.EntryId));

            // Act
            var scheduled = await InFirmAAsync(manager => manager.RemoveAsync(entry.EntryId));
            var pending = await GetEntryAsync(entry.EntryId);
            var resume = await InFirmAAsync(manager => manager.ResumeAsync(entry.EntryId));
            var restored = await InFirmAAsync(manager => manager.RestoreAsync(entry.EntryId));
            var afterRestore = await GetEntryAsync(entry.EntryId);

            // Assert
            Assert.True(scheduled.Succeeded, scheduled.Error);
            Assert.Equal(ChildTenantStatus.PendingRemoval, pending.Status);
            Assert.NotNull(pending.RetainUntilUtc);
            Assert.NotNull(_fixture.Host.GetSettings(entry.TenantName));
            Assert.False(resume.Succeeded);
            Assert.True(restored.Succeeded);
            Assert.Equal(ChildTenantStatus.Ready, afterRestore.Status);
            Assert.Null(afterRestore.RetainUntilUtc);
        }
        finally
        {
            await SetGraceDaysAsync(TenantHierarchyFixture.FirmA, 0);
            await RemoveAsync(entry);
        }
    }

    [Fact]
    public async Task RemoveDue_RemovesAChildWhoseGracePeriodEnded()
    {
        // Arrange
        await SetGraceDaysAsync(TenantHierarchyFixture.FirmA, 1);
        var entry = await _fixture.CreateChildAsync(TenantHierarchyFixture.FirmA, "Due Business", "due");

        try
        {
            await InFirmAAsync(manager => manager.SuspendAsync(entry.EntryId));
            await InFirmAAsync(manager => manager.RemoveAsync(entry.EntryId));

            // Make the retention date pass.
            await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
            {
                var store = services.GetRequiredService<ChildTenantEntryStore>();
                var pending = await store.FindByEntryIdAsync(entry.EntryId);
                pending.RetainUntilUtc = DateTime.UtcNow.AddMinutes(-1);
                await store.SaveAsync(pending);
            });

            // Act
            var removed = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
                services.GetRequiredService<ChildTenantManager>().RemoveDueAsync());

            // Assert
            Assert.Equal(1, removed);
            Assert.Null(_fixture.Host.GetSettings(entry.TenantName));
            Assert.Null(await GetEntryAsync(entry.EntryId));
        }
        finally
        {
            await SetGraceDaysAsync(TenantHierarchyFixture.FirmA, 0);
        }
    }

    [Fact]
    public async Task ActivityLog_RecordsTheLifeOfAChild()
    {
        // Act
        var names = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
            (await services.GetRequiredService<HierarchyAuditLog>().PageAsync(_fixture.BusinessOne.EntryId, 0, 100)).Select(auditEvent => auditEvent.Name).ToList());

        // Assert
        Assert.Contains(HierarchyAuditEventNames.Created, names);
        Assert.Contains(HierarchyAuditEventNames.SetupSucceeded, names);
    }

    private Task<TenantHierarchyResult> InFirmAAsync(Func<ChildTenantManager, Task<TenantHierarchyResult>> operation)
        => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services => operation(services.GetRequiredService<ChildTenantManager>()));

    private Task<ChildTenantEntry> GetEntryAsync(string entryId)
        => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services => services.GetRequiredService<ChildTenantEntryStore>().FindByEntryIdAsync(entryId));

    private async Task RemoveAsync(ChildTenantEntry entry)
    {
        await InFirmAAsync(manager => manager.SuspendAsync(entry.EntryId));
        await InFirmAAsync(manager => manager.RemoveAsync(entry.EntryId));
    }

    private Task SetMaxChildrenAsync(string parent, int maxChildren)
        => UpdatePolicyAsync(parent, policy => policy.MaxChildren = maxChildren);

    private Task SetGraceDaysAsync(string parent, int days)
        => UpdatePolicyAsync(parent, policy => policy.RemovalGraceDays = days);

    private async Task UpdatePolicyAsync(string parent, Action<ParentTenantPolicy> change)
    {
        await _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var settings = _fixture.Host.GetSettings(parent);
            var policy = settings.GetParentPolicy();
            change(policy);
            var (result, _) = await services.GetRequiredService<TenantHierarchyPlatformService>().UpdateParentAsync(parent, settings.GetHierarchyDisplayName(), policy);
            Assert.True(result.Succeeded, result.Error);
        });
    }
}
