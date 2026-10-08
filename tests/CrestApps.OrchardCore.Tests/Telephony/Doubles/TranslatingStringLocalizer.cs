using System.Globalization;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A <see cref="IStringLocalizer{T}"/> holding translations for named cultures, which it looks up by the current UI
/// culture at the moment a string is asked for, the way the tenant's localizer does. A string with no translation for
/// the current culture comes back as written.
/// </summary>
/// <typeparam name="T">The localized resource type.</typeparam>
internal sealed class TranslatingStringLocalizer<T> : IStringLocalizer<T>
{
    private readonly Dictionary<(string Culture, string Name), string> _translations = [];

    public TranslatingStringLocalizer<T> Add(string culture, string name, string translation)
    {
        _translations[(culture, name)] = translation;

        return this;
    }

    public LocalizedString this[string name] => Find(name, []);

    public LocalizedString this[string name, params object[] arguments] => Find(name, arguments);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

    private LocalizedString Find(string name, object[] arguments)
    {
        var found = _translations.TryGetValue((CultureInfo.CurrentUICulture.Name, name), out var translation);
        var format = found ? translation : name;

        return new LocalizedString(name, arguments.Length == 0 ? format : string.Format(CultureInfo.CurrentCulture, format, arguments), resourceNotFound: !found);
    }
}
