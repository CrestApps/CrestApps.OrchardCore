namespace CrestApps.OrchardCore.Accounts.Models;

/// <summary>
/// The e-mail domains an account can't claim: public mail providers, which everyone shares. A tenant adds its own in its
/// configuration (<c>CrestApps:Accounts:EmailDomains:PublicDomains</c>).
/// </summary>
public sealed class AccountEmailDomainsOptions
{
    /// <summary>
    /// The public mail providers built in.
    /// </summary>
    public static readonly IReadOnlyList<string> BuiltInPublicDomains =
    [
        "gmail.com", "googlemail.com", "outlook.com", "hotmail.com", "live.com", "msn.com", "yahoo.com", "ymail.com",
        "icloud.com", "me.com", "mac.com", "aol.com", "proton.me", "protonmail.com", "pm.me", "gmx.com", "gmx.net",
        "gmx.de", "mail.com", "zoho.com", "yandex.com", "yandex.ru", "mail.ru", "qq.com", "163.com", "126.com",
        "web.de", "t-online.de", "orange.fr", "free.fr", "laposte.net", "hey.com", "fastmail.com", "tutanota.com",
        "comcast.net", "verizon.net", "att.net", "sbcglobal.net", "btinternet.com", "rediffmail.com",
    ];

    /// <summary>
    /// Gets or sets more public domains this tenant refuses, beside <see cref="BuiltInPublicDomains"/>.
    /// </summary>
    public List<string> PublicDomains { get; set; } = [];

    /// <summary>
    /// Returns whether a domain is a public mail provider's.
    /// </summary>
    /// <param name="domain">The domain, normalized.</param>
    /// <returns>Whether it is.</returns>
    public bool IsPublic(string domain)
        => !string.IsNullOrEmpty(domain)
            && (BuiltInPublicDomains.Contains(domain, StringComparer.OrdinalIgnoreCase) || PublicDomains.Contains(domain, StringComparer.OrdinalIgnoreCase));
}
