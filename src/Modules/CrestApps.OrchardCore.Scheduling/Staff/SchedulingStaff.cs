using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Staff.Services;

namespace CrestApps.OrchardCore.Scheduling.Staff;

/// <summary>
/// Whether Scheduling picks the person behind a staff resource among staff.
/// </summary>
public sealed class SchedulingStaffOptions
{
    /// <summary>
    /// Gets or sets whether the staff picker is used.
    /// </summary>
    public bool UseStaffPicker { get; set; }
}

/// <summary>
/// Fills a staff resource from the person's staff record: an empty name and time zone are taken from it, and the person
/// gains the Bookable capability.
/// </summary>
internal sealed class SchedulingResourceStaffHandler : CatalogEntryHandlerBase<SchedulingResource>
{
    public const string BookableCapability = "Bookable";

    private readonly IStaffDirectory _directory;
    private readonly IStaffTimeZoneResolver _timeZones;
    private readonly IStaffAccountService _accounts;

    public SchedulingResourceStaffHandler(
        IStaffDirectory directory,
        IStaffTimeZoneResolver timeZones,
        IStaffAccountService accounts)
    {
        _directory = directory;
        _timeZones = timeZones;
        _accounts = accounts;
    }

    public override Task CreatingAsync(CreatingContext<SchedulingResource> context, CancellationToken cancellationToken = default)
        => FillAsync(context.Model, cancellationToken);

    public override Task UpdatingAsync(UpdatingContext<SchedulingResource> context, CancellationToken cancellationToken = default)
        => FillAsync(context.Model, cancellationToken);

    public override Task ValidatingAsync(ValidatingContext<SchedulingResource> context, CancellationToken cancellationToken = default)
        => FillAsync(context.Model, cancellationToken);

    private async Task FillAsync(SchedulingResource resource, CancellationToken cancellationToken)
    {
        if (resource.Kind != SchedulingResourceKind.Staff || string.IsNullOrEmpty(resource.UserId))
        {
            return;
        }

        var staff = await _directory.FindByUserIdAsync(resource.UserId, cancellationToken)
            ?? await _accounts.EnsureAsync(resource.UserId, resource.Name ?? resource.UserId, [BookableCapability], cancellationToken: cancellationToken);

        if (staff is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(resource.Name))
        {
            resource.Name = staff.DisplayName;
        }

        if (string.IsNullOrWhiteSpace(resource.TimeZoneId))
        {
            resource.TimeZoneId = await _timeZones.GetTimeZoneIdAsync(staff, cancellationToken);
        }

        if (!staff.HasCapability(BookableCapability))
        {
            await _accounts.EnsureAsync(resource.UserId, staff.DisplayName, [BookableCapability], cancellationToken: cancellationToken);
        }
    }
}
