using System.Runtime.CompilerServices;
using CrestApps.Core;
using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What Workforce keeps on a staff member for scheduling: the fewest and most hours they may work, and when they can
/// work. It lives in the staff member's properties; the staff record itself belongs to Staff.
/// </summary>
public sealed class WorkforceStaffSettings
{
    /// <summary>
    /// Gets or sets the fewest hours a week to schedule.
    /// </summary>
    public decimal? MinHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most hours a week to schedule.
    /// </summary>
    public decimal? MaxHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most hours a day to schedule.
    /// </summary>
    public decimal? MaxHoursPerDay { get; set; }

    /// <summary>
    /// Gets or sets the weekly windows the person can, can't or prefers to work; empty means any time.
    /// </summary>
    public IList<AvailabilityWindow> Availability { get; set; } = [];
}

/// <summary>
/// Reads and writes Workforce's scheduling values on a staff member as if they were its own properties. Each staff
/// member reads its settings once; changes are written back to its properties at once, and again when it is saved, so
/// changing the availability list in place is kept too.
/// </summary>
public static class WorkforceStaffExtensions
{
    private static readonly ConditionalWeakTable<StaffMember, WorkforceStaffSettings> _settings = [];

    /// <summary>
    /// Gets a staff member's Workforce settings, read once per staff member object.
    /// </summary>
    /// <param name="staff">The staff member.</param>
    /// <returns>The settings, which can be changed in place.</returns>
    public static WorkforceStaffSettings GetWorkforceSettings(this StaffMember staff)
    {
        ArgumentNullException.ThrowIfNull(staff);

        return _settings.GetValue(staff, static member =>
        {
            var settings = member.TryGet<WorkforceStaffSettings>(out var stored) && stored is not null ? stored : new WorkforceStaffSettings();

            settings.Availability ??= [];

            return settings;
        });
    }

    /// <summary>
    /// Writes a staff member's Workforce settings back to its properties.
    /// </summary>
    /// <param name="staff">The staff member.</param>
    public static void FlushWorkforceSettings(this StaffMember staff)
    {
        ArgumentNullException.ThrowIfNull(staff);

        if (_settings.TryGetValue(staff, out var settings))
        {
            staff.Put(settings);
        }
    }

    extension(StaffMember staff)
    {
        /// <summary>
        /// Gets or sets the fewest hours a week to schedule.
        /// </summary>
        public decimal? MinHoursPerWeek
        {
            get => staff.GetWorkforceSettings().MinHoursPerWeek;
            set => Change(staff, settings => settings.MinHoursPerWeek = value);
        }

        /// <summary>
        /// Gets or sets the most hours a week to schedule.
        /// </summary>
        public decimal? MaxHoursPerWeek
        {
            get => staff.GetWorkforceSettings().MaxHoursPerWeek;
            set => Change(staff, settings => settings.MaxHoursPerWeek = value);
        }

        /// <summary>
        /// Gets or sets the most hours a day to schedule.
        /// </summary>
        public decimal? MaxHoursPerDay
        {
            get => staff.GetWorkforceSettings().MaxHoursPerDay;
            set => Change(staff, settings => settings.MaxHoursPerDay = value);
        }

        /// <summary>
        /// Gets or sets the weekly windows the person can, can't or prefers to work; empty means any time.
        /// </summary>
        public IList<AvailabilityWindow> Availability
        {
            get => staff.GetWorkforceSettings().Availability;
            set => Change(staff, settings => settings.Availability = value ?? []);
        }
    }

    private static void Change(StaffMember staff, Action<WorkforceStaffSettings> change)
    {
        var settings = staff.GetWorkforceSettings();

        change(settings);
        staff.Put(settings);
    }
}
