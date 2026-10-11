using System.Globalization;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// An <see cref="IHtmlLocalizer{T}"/> that returns the key unchanged, so a test asserts behavior rather
/// than translation.
/// </summary>
/// <typeparam name="T">The type the localizer is scoped to.</typeparam>
internal sealed class PassThroughHtmlLocalizer<T> : IHtmlLocalizer<T>
{
    public LocalizedHtmlString this[string name]
        => new(name, name);

    public LocalizedHtmlString this[string name, params object[] arguments]
        => new(name, name, false, arguments);

    public LocalizedString GetString(string name)
        => new(name, name, false, null);

    public LocalizedString GetString(string name, params object[] arguments)
        => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments), false, null);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => [];
}
