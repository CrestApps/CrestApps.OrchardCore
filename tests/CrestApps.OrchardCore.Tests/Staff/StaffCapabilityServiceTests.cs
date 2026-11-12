using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Staff;

/// <summary>
/// Capabilities are kept by admins: modules seed theirs (built in, relabeled or turned off but never deleted or
/// renamed), admins add their own, and staff are found by any of several.
/// </summary>
public sealed class StaffCapabilityServiceTests
{
    [Fact]
    public async Task BuiltInCapabilities_CanBeRelabeledAndTurnedOff_AndResetToTheirDefaults()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync(services => services.AddStaffCapability("Technician", new("Technician"), new("Goes out to jobs."), 20));

        // Act
        var result = await harness.RunAsync(async services =>
        {
            var capabilities = services.GetRequiredService<IStaffCapabilityService>();
            var defaults = await capabilities.FindAsync("technician");
            var errors = await capabilities.SaveAsync(new StaffCapabilityDefinition { Name = "Technician", DisplayName = "Field technician", Position = 5, Enabled = false }, isNew: false);
            var changed = await capabilities.FindAsync("Technician");
            var offered = await capabilities.ListAsync();
            var reset = await capabilities.DeleteAsync("Technician");
            var back = await capabilities.FindAsync("Technician");

            return (defaults, errors, changed, offered, reset, back);
        });

        // Assert
        Assert.Equal("Technician", result.defaults.DisplayName);
        Assert.True(result.defaults.IsBuiltIn);
        Assert.False(result.defaults.IsCustomized);
        Assert.Empty(result.errors);
        Assert.Equal("Field technician", result.changed.DisplayName);
        Assert.False(result.changed.Enabled);
        Assert.True(result.changed.IsCustomized);
        Assert.DoesNotContain(result.offered, capability => capability.Name == "Technician");
        Assert.True(result.reset);
        Assert.True(result.back.IsBuiltIn);
        Assert.True(result.back.Enabled);
        Assert.Equal("Technician", result.back.DisplayName);
    }

    [Fact]
    public async Task AdminsAddTheirOwnCapabilities_WithAUniqueValidKey_AndDeleteThem()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync(services => services.AddStaffCapability("Technician", new("Technician")));

        // Act
        var result = await harness.RunAsync(async services =>
        {
            var capabilities = services.GetRequiredService<IStaffCapabilityService>();
            var added = await capabilities.SaveAsync(new StaffCapabilityDefinition { Name = "Inspector", DisplayName = "Inspector", Description = "Signs off finished jobs." }, isNew: true);
            var duplicate = await capabilities.SaveAsync(new StaffCapabilityDefinition { Name = "technician", DisplayName = "Copy" }, isNew: true);
            var invalid = await capabilities.SaveAsync(new StaffCapabilityDefinition { Name = "9 lives", DisplayName = "Bad" }, isNew: true);
            var inspector = await capabilities.FindAsync("Inspector");
            var deleted = await capabilities.DeleteAsync("Inspector");
            var gone = await capabilities.FindAsync("Inspector");

            return (added, duplicate, invalid, inspector, deleted, gone);
        });

        // Assert
        Assert.Empty(result.added);
        Assert.Single(result.duplicate);
        Assert.Single(result.invalid);
        Assert.False(result.inspector.IsBuiltIn);
        Assert.Equal("Signs off finished jobs.", result.inspector.Description);
        Assert.True(result.deleted);
        Assert.Null(result.gone);
    }

    [Fact]
    public async Task StaffAreFound_ByAnyOfSeveralCapabilities()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var technician = await WorkforceTests.AddWorkerAsync(harness, "Ana Field", staff => staff.Capabilities.Add("Technician"));
        var inspector = await WorkforceTests.AddWorkerAsync(harness, "Ivan Check", staff => staff.Capabilities.Add("Inspector"));
        await WorkforceTests.AddWorkerAsync(harness, "Olga Office");

        // Act
        var (found, picked) = await harness.RunAsync(async services =>
        {
            var (_, items) = await services.GetRequiredService<IStaffMemberManager>().SearchAsync(new StaffQuery { Capabilities = ["Technician", "Inspector"] }, 0, 10);
            var fromPicker = await services.GetRequiredService<IStaffDirectory>().SearchAsync(null, "Technician, Inspector");

            return (items, fromPicker);
        }, commit: false);

        // Assert
        Assert.Equal(new[] { technician.ItemId, inspector.ItemId }.Order(StringComparer.Ordinal), found.Select(staff => staff.ItemId).Order(StringComparer.Ordinal));
        Assert.Equal(new[] { technician.ItemId, inspector.ItemId }.Order(StringComparer.Ordinal), picked.Select(staff => staff.ItemId).Order(StringComparer.Ordinal));
    }
}
