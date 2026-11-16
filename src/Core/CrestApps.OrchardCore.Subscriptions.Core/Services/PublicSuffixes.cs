namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The public suffixes a buyer is most likely to type as a domain of their own: country second levels (<c>co.uk</c>,
/// <c>com.au</c>) and the shared hosting platforms whose customers each get a name under them (<c>github.io</c>,
/// <c>azurewebsites.net</c>). Nobody owns one of these, so a sold site may not answer on it; and since a site's domain
/// also holds the names under and above it, one purchase naming one would hold every name under it.
/// </summary>
/// <remarks>
/// A short built-in list from the Public Suffix List (https://publicsuffix.org), not the whole list: a name under a
/// suffix that is missing here is still held to every other rule, and the operator can reserve more hosts.
/// </remarks>
internal static class PublicSuffixes
{
    private static readonly HashSet<string> _suffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Country second levels.
        "co.uk", "org.uk", "me.uk", "ltd.uk", "plc.uk", "net.uk", "ac.uk", "gov.uk", "sch.uk", "nhs.uk", "police.uk",
        "com.au", "net.au", "org.au", "edu.au", "gov.au", "asn.au", "id.au",
        "co.nz", "net.nz", "org.nz", "govt.nz", "ac.nz", "school.nz",
        "co.jp", "ne.jp", "or.jp", "ac.jp", "go.jp", "ad.jp", "ed.jp", "gr.jp", "lg.jp",
        "co.kr", "or.kr", "ne.kr", "go.kr", "ac.kr",
        "com.cn", "net.cn", "org.cn", "gov.cn", "edu.cn",
        "com.hk", "org.hk", "net.hk", "edu.hk", "gov.hk",
        "com.tw", "org.tw", "net.tw", "edu.tw", "gov.tw",
        "com.sg", "org.sg", "net.sg", "edu.sg", "gov.sg",
        "com.my", "org.my", "net.my", "edu.my", "gov.my",
        "co.in", "net.in", "org.in", "firm.in", "gen.in", "ind.in", "ac.in", "edu.in", "gov.in", "res.in",
        "co.id", "or.id", "web.id", "ac.id", "go.id",
        "co.th", "in.th", "or.th", "ac.th", "go.th",
        "com.ph", "net.ph", "org.ph", "gov.ph", "edu.ph",
        "com.vn", "net.vn", "org.vn", "gov.vn", "edu.vn",
        "com.pk", "net.pk", "org.pk", "gov.pk", "edu.pk",
        "co.il", "org.il", "net.il", "ac.il", "gov.il",
        "com.tr", "net.tr", "org.tr", "gov.tr", "edu.tr",
        "com.sa", "net.sa", "org.sa", "gov.sa", "edu.sa",
        "com.eg", "net.eg", "org.eg", "gov.eg", "edu.eg",
        "co.za", "org.za", "net.za", "gov.za", "ac.za", "web.za",
        "com.ng", "net.ng", "org.ng", "gov.ng", "edu.ng",
        "co.ke", "or.ke", "ne.ke", "go.ke", "ac.ke",
        "com.br", "net.br", "org.br", "gov.br", "edu.br",
        "com.mx", "net.mx", "org.mx", "gob.mx", "edu.mx",
        "com.ar", "net.ar", "org.ar", "gob.ar", "edu.ar",
        "com.co", "net.co", "org.co", "gov.co", "edu.co",
        "com.pe", "net.pe", "org.pe", "gob.pe", "edu.pe",
        "com.ec", "net.ec", "org.ec", "gob.ec", "edu.ec",
        "com.ua", "net.ua", "org.ua", "gov.ua", "edu.ua",
        "com.pl", "net.pl", "org.pl", "gov.pl", "edu.pl",
        "com.es", "nom.es", "org.es", "gob.es", "edu.es",
        "co.at", "or.at", "gv.at", "ac.at",

        // Shared hosting and platform names, each customer's site a name under them.
        "github.io", "githubusercontent.com", "gitlab.io", "bitbucket.io", "readthedocs.io",
        "pages.dev", "workers.dev", "netlify.app", "vercel.app", "now.sh", "surge.sh",
        "herokuapp.com", "herokudns.com", "onrender.com", "fly.dev", "railway.app", "glitch.me", "repl.co",
        "appspot.com", "web.app", "firebaseapp.com", "blogspot.com", "run.app",
        "azurewebsites.net", "azurestaticapps.net", "azureedge.net", "azurefd.net", "cloudapp.net", "cloudapp.azure.com",
        "trafficmanager.net", "blob.core.windows.net", "web.core.windows.net",
        "cloudfront.net", "amazonaws.com", "s3.amazonaws.com", "elasticbeanstalk.com", "awsapps.com",
        "myshopify.com", "wordpress.com", "wixsite.com", "squarespace.com", "webflow.io",
        "ngrok.io", "ngrok-free.app", "ngrok.app", "trycloudflare.com",
        "duckdns.org", "no-ip.org", "dyndns.org", "ddns.net",
    };

    /// <summary>
    /// Whether <paramref name="domain"/> is one of the public suffixes, compared without case.
    /// </summary>
    /// <param name="domain">The domain, without a trailing dot.</param>
    public static bool Contains(string domain)
        => !string.IsNullOrEmpty(domain) && _suffixes.Contains(domain);
}
