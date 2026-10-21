using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A localizer factory whose localizers return the source text, formatted with its arguments.
/// </summary>
public sealed class SourceTextLocalizerFactory : IStringLocalizerFactory
{
    /// <inheritdoc />
    public IStringLocalizer Create(Type resourceSource)
        => new SourceTextLocalizer();

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName, string location)
        => new SourceTextLocalizer();

    private sealed class SourceTextLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
