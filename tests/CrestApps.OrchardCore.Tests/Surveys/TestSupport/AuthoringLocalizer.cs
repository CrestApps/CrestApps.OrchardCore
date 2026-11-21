using System.Globalization;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A localizer that returns the text it is given, formatted with its arguments.
/// </summary>
/// <typeparam name="T">The type the localizer serves.</typeparam>
internal sealed class AuthoringLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name]
        => new(name, name);

    public LocalizedString this[string name, params object[] arguments]
        => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => [];
}
