using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A localizer that returns the text it is given.
/// </summary>
internal sealed class RuntimeLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name]
        => new(name, name);

    public LocalizedString this[string name, params object[] arguments]
        => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => [];
}
