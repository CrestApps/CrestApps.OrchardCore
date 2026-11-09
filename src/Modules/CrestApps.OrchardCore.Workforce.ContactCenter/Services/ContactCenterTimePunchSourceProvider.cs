using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services;

/// <summary>
/// Describes the punch source of the contact center's sign-in and sign-out, so a time clock policy can allow it and
/// timesheets name it.
/// </summary>
public sealed class ContactCenterTimePunchSourceProvider : ITimePunchSourceProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTimePunchSourceProvider"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterTimePunchSourceProvider(IStringLocalizer<ContactCenterTimePunchSourceProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public IEnumerable<TimePunchSourceDescriptor> GetSources()
    {
        yield return new TimePunchSourceDescriptor(
            WorkforceContactCenterConstants.PunchSource,
            S["The contact center"],
            S["the contact center"],
            S["Agents' sign-in and sign-out."]);
    }
}
