using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

/// <summary>
/// The API key and the API root the Mailchimp calls of a tenant use. The key never appears in <see cref="ToString"/>.
/// </summary>
public sealed partial class MailchimpCredentials
{
    /// <summary>
    /// The API root used when neither a server prefix nor the key names a data center.
    /// </summary>
    public static readonly Uri DefaultBaseAddress = new("https://api.mailchimp.com/3.0/");

    /// <summary>
    /// Initializes a new instance of the <see cref="MailchimpCredentials"/> class.
    /// </summary>
    /// <param name="apiKey">The API key.</param>
    /// <param name="baseAddress">The API root, ending with a slash.</param>
    public MailchimpCredentials(string apiKey, Uri baseAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentNullException.ThrowIfNull(baseAddress);

        ApiKey = apiKey;
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// Gets the API key.
    /// </summary>
    public string ApiKey { get; }

    /// <summary>
    /// Gets the API root, such as <c>https://us21.api.mailchimp.com/3.0/</c>.
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// Returns the credentials of an API key: the API root is the server prefix's data center when one is given, else
    /// the data center the key ends with (<c>-us21</c>), else <see cref="DefaultBaseAddress"/>. A prefix or suffix that is
    /// not a plain data center name (letters, then digits) is never put in a host name.
    /// </summary>
    /// <param name="apiKey">The API key.</param>
    /// <param name="serverPrefix">The server prefix from the settings, or <see langword="null"/>.</param>
    public static MailchimpCredentials Create(string apiKey, string serverPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var key = apiKey.Trim();

        return new MailchimpCredentials(key, ResolveBaseAddress(key, serverPrefix));
    }

    /// <summary>
    /// Returns the API root of an API key and an optional server prefix.
    /// </summary>
    /// <param name="apiKey">The API key.</param>
    /// <param name="serverPrefix">The server prefix from the settings, or <see langword="null"/>.</param>
    public static Uri ResolveBaseAddress(string apiKey, string serverPrefix)
    {
        if (IsValidServerPrefix(serverPrefix))
        {
            return ForDataCenter(serverPrefix.Trim().ToLowerInvariant());
        }

        var dash = apiKey?.LastIndexOf('-') ?? -1;

        if (dash >= 0)
        {
            var suffix = apiKey.Substring(dash + 1).Trim();

            if (IsValidServerPrefix(suffix))
            {
                return ForDataCenter(suffix.ToLowerInvariant());
            }
        }

        return DefaultBaseAddress;
    }

    /// <summary>
    /// Returns whether a value is a plain Mailchimp data center name, such as <c>us21</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    public static bool IsValidServerPrefix(string value)
        => !string.IsNullOrWhiteSpace(value) && DataCenterPattern().IsMatch(value.Trim());

    /// <inheritdoc/>
    public override string ToString()
        => $"Mailchimp credentials for {BaseAddress.Host}";

    private static Uri ForDataCenter(string dataCenter)
        => new($"https://{dataCenter}.api.mailchimp.com/3.0/");

    [GeneratedRegex("^[a-zA-Z]{2,5}[0-9]{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex DataCenterPattern();
}
