using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Builds one hierarchy that every integration test shares: the platform (Default), two parents with their own
/// children, and an ordinary tenant.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>firma</c> (parent, host <c>firma.localhost</c>) owns <c>Business One</c> and <c>Business Two</c>.</item>
/// <item><c>firmb</c> (parent, host <c>firmb.localhost</c>) owns <c>Business Four</c>.</item>
/// <item><c>plain</c> is an ordinary tenant.</item>
/// </list>
/// </remarks>
public sealed class TenantHierarchyFixture : IAsyncLifetime
{
    /// <summary>
    /// The tenant name of the first parent.
    /// </summary>
    public const string FirmA = "firma";

    /// <summary>
    /// The tenant name of the second parent.
    /// </summary>
    public const string FirmB = "firmb";

    /// <summary>
    /// The tenant name of the ordinary tenant.
    /// </summary>
    public const string Plain = "plain";

    /// <summary>
    /// The administrator of the first parent.
    /// </summary>
    public const string Alice = "alice";

    /// <summary>
    /// A user of the first parent with no role.
    /// </summary>
    public const string Carol = "carol";

    /// <summary>
    /// The administrator of the second parent.
    /// </summary>
    public const string Bob = "bob";

    /// <summary>
    /// Gets the test host.
    /// </summary>
    public TenantHierarchyTestHost Host { get; private set; } = null!;

    /// <summary>
    /// Gets the password every test user has. It is generated for this run.
    /// </summary>
    public string Password { get; } = $"Th-{Guid.NewGuid():N}!aA1";

    /// <summary>
    /// Gets the registry entry of Business One.
    /// </summary>
    public ChildTenantEntry BusinessOne { get; private set; } = null!;

    /// <summary>
    /// Gets the registry entry of Business Two.
    /// </summary>
    public ChildTenantEntry BusinessTwo { get; private set; } = null!;

    /// <summary>
    /// Gets the registry entry of Business Four, which belongs to the second parent.
    /// </summary>
    public ChildTenantEntry BusinessFour { get; private set; } = null!;

    /// <summary>
    /// The base address of the first parent.
    /// </summary>
    public const string FirmAAddress = "http://firma.localhost";

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        Host = await TenantHierarchyTestHost.StartAsync();

        await Host.SetupTenantAsync(ShellSettings.DefaultShellName, "Blank", "platform", Password);
        await Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var features = services.GetRequiredService<IShellFeaturesManager>();
            var available = await features.GetAvailableFeaturesAsync();
            var platform = available.SingleOrDefault(feature => feature.Id == TenantHierarchyConstants.Features.Platform);

            Assert.NotNull(platform);
            await features.EnableFeaturesAsync([platform], force: true);
        });

        foreach (var (name, admin) in new[] { (FirmA, Alice), (FirmB, Bob), (Plain, "paula") })
        {
            await Host.CreateTenantAsync(name, $"{name}.localhost");
            await Host.SetupTenantAsync(name, "Blank", admin, Password);
        }

        await MakeParentAsync(FirmA, "firma", "Firm A");
        await MakeParentAsync(FirmB, "firmb", "Firm B");

        await Host.InTenantAsync(FirmA, async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var result = await userManager.CreateAsync(
                new User
                {
                    UserName = Carol,
                    Email = $"{Carol}@example.invalid",
                    EmailConfirmed = true,
                    IsEnabled = true,
                },
                Password);

            Assert.True(result.Succeeded, string.Join(' ', result.Errors.Select(error => error.Description)));
        });

        BusinessOne = await CreateChildAsync(FirmA, "Business One", "business1");
        BusinessTwo = await CreateChildAsync(FirmA, "Business Two", "business2");
        BusinessFour = await CreateChildAsync(FirmB, "Business Four", "business4");
    }

    /// <summary>
    /// Creates a child tenant in a parent and runs its setup.
    /// </summary>
    /// <param name="parent">The tenant name of the parent.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="slug">The slug.</param>
    public async Task<ChildTenantEntry> CreateChildAsync(string parent, string displayName, string slug)
    {
        var entry = await Host.InTenantAsync(parent, async services =>
        {
            var manager = services.GetRequiredService<ChildTenantManager>();
            var (result, created) = await manager.CreateAsync(new CreateChildTenantRequest
            {
                DisplayName = displayName,
                Slug = slug,
                RecipeName = "Blank",
            });

            Assert.True(result.Succeeded, result.Error);

            return created;
        });

        await Host.InTenantAsync(parent, async services =>
        {
            var result = await services.GetRequiredService<ChildTenantManager>().SetupAsync(entry.EntryId);
            Assert.True(result.Succeeded, result.Error);
        });

        return await Host.InTenantAsync(parent, services => services.GetRequiredService<Core.Services.ChildTenantEntryStore>().FindByEntryIdAsync(entry.EntryId));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Host is not null)
        {
            await Host.DisposeAsync();
        }
    }

    private async Task MakeParentAsync(string tenant, string slug, string displayName)
    {
        await Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var platform = services.GetRequiredService<TenantHierarchyPlatformService>();
            var (result, errors) = await platform.MakeParentAsync(tenant, slug, displayName, new ParentTenantPolicy
            {
                MaxChildren = 5,
            });

            Assert.True(result.Succeeded, $"{result.Error} {string.Join(' ', errors.Values)}");
        });
    }
}
