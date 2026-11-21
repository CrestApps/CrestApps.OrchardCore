using System.Globalization;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A localizer for control tests that returns the text with its arguments filled in.
/// </summary>
/// <typeparam name="T">The type the localizer is for.</typeparam>
public sealed class ControlsTestLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name]
        => new(name, name);

    public LocalizedString this[string name, params object[] arguments]
        => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => [];
}
