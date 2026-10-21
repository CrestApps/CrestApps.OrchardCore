using CrestApps.OrchardCore.Addresses.Indexes;
using Microsoft.Extensions.Localization;
using YesSql;

namespace CrestApps.OrchardCore.Addresses.Services;

/// <summary>
/// The rule a Country's ISO code follows: two letters, and no other country has it.
/// </summary>
public sealed class CountryCodeValidator
{
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    public CountryCodeValidator(ISession session, IStringLocalizer<CountryCodeValidator> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    /// <summary>
    /// What is wrong with <paramref name="code"/> for the country <paramref name="contentItemId"/>, or
    /// <see langword="null"/> when nothing is.
    /// </summary>
    public async Task<string> GetErrorAsync(string code, string contentItemId)
    {
        code = code?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(code))
        {
            return S["An ISO country code is required."];
        }

        if (code.Length != 2 || !char.IsAsciiLetterUpper(code[0]) || !char.IsAsciiLetterUpper(code[1]))
        {
            return S["The ISO country code must be two letters, for example US or CA."];
        }

        var duplicate = await _session.QueryIndex<CountryIndex>(index =>
                index.Code == code &&
                index.ContentItemId != contentItemId &&
                index.Latest)
            .FirstOrDefaultAsync();

        return duplicate is null ? null : S["A country with the code '{0}' already exists.", code];
    }
}
