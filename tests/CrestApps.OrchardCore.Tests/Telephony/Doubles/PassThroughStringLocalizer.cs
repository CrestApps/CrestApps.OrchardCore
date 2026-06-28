using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A pass-through <see cref="IStringLocalizer{T}"/> that returns the supplied name as the value.
/// </summary>
/// <typeparam name="T">The localized resource type.</typeparam>
internal sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
