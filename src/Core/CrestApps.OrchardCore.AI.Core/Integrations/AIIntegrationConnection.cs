using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.AI.Core.Integrations;

/// <summary>
/// A connection to an outside calendar or CRM that AI tools and workflow tasks use: the app it signs in with and the
/// tokens it holds, encrypted.
/// </summary>
public sealed class AIIntegrationConnection : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the connection's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the provider, from <see cref="AIIntegrationProviders"/>.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the OAuth app's client id.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth app's client secret, encrypted.
    /// </summary>
    public string ProtectedClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the access token, encrypted. For HubSpot and Notion it is the private app or integration token.
    /// </summary>
    public string ProtectedAccessToken { get; set; }

    /// <summary>
    /// Gets or sets the refresh token, encrypted.
    /// </summary>
    public string ProtectedRefreshToken { get; set; }

    /// <summary>
    /// Gets or sets when the access token expires, or <see langword="null"/> when it does not.
    /// </summary>
    public DateTime? AccessTokenExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft Entra tenant, for Microsoft 365. Empty uses <c>common</c>.
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// Gets or sets the Salesforce instance address the token was issued for.
    /// </summary>
    public string InstanceUrl { get; set; }

    /// <summary>
    /// Gets or sets the calendar used, for calendar providers. Empty uses the primary calendar.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the time zone appointments are offered in, such as <c>America/Los_Angeles</c>.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the first hour appointments are offered, from 0 to 23.
    /// </summary>
    public int WorkdayStartHour { get; set; } = 9;

    /// <summary>
    /// Gets or sets the hour appointments stop, from 1 to 24.
    /// </summary>
    public int WorkdayEndHour { get; set; } = 17;

    /// <summary>
    /// Gets or sets who connected it.
    /// </summary>
    public string ConnectedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was connected.
    /// </summary>
    public DateTime? ConnectedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets a value indicating whether it holds a token.
    /// </summary>
    public bool IsConnected => !string.IsNullOrEmpty(ProtectedAccessToken) || !string.IsNullOrEmpty(ProtectedRefreshToken);
}

/// <summary>
/// The providers an integration connection can be to, and how each signs in.
/// </summary>
public static class AIIntegrationProviders
{
    /// <summary>Google Calendar and Google Drive.</summary>
    public const string Google = "Google";

    /// <summary>Microsoft 365: Outlook calendar and SharePoint or OneDrive files.</summary>
    public const string Microsoft365 = "Microsoft365";

    /// <summary>HubSpot CRM, with a private app token.</summary>
    public const string HubSpot = "HubSpot";

    /// <summary>Salesforce CRM.</summary>
    public const string Salesforce = "Salesforce";

    /// <summary>Notion, with an internal integration token.</summary>
    public const string Notion = "Notion";

    /// <summary>
    /// Every provider.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Google, Microsoft365, HubSpot, Salesforce, Notion];

    /// <summary>
    /// Gets whether a provider signs in with OAuth, rather than a token pasted in.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>Whether it does.</returns>
    public static bool UsesOAuth(string provider)
        => provider is Google or Microsoft365 or Salesforce;

    /// <summary>
    /// Gets whether a provider has a calendar.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>Whether it does.</returns>
    public static bool HasCalendar(string provider)
        => provider is Google or Microsoft365;

    /// <summary>
    /// Gets whether a provider is a CRM.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsCrm(string provider)
        => provider is HubSpot or Salesforce;

    /// <summary>
    /// Gets where a provider's users approve the app.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The authorization address.</returns>
    public static string AuthorizeEndpoint(AIIntegrationConnection connection)
        => connection.Provider switch
        {
            Google => "https://accounts.google.com/o/oauth2/v2/auth",
            Microsoft365 => $"https://login.microsoftonline.com/{TenantOf(connection)}/oauth2/v2.0/authorize",
            Salesforce => $"{SalesforceLogin(connection)}/services/oauth2/authorize",
            _ => null,
        };

    /// <summary>
    /// Gets where a provider's tokens are issued.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The token address.</returns>
    public static string TokenEndpoint(AIIntegrationConnection connection)
        => connection.Provider switch
        {
            Google => "https://oauth2.googleapis.com/token",
            Microsoft365 => $"https://login.microsoftonline.com/{TenantOf(connection)}/oauth2/v2.0/token",
            Salesforce => $"{SalesforceLogin(connection)}/services/oauth2/token",
            _ => null,
        };

    /// <summary>
    /// Gets the scopes a provider is asked for: the calendar and files for Google and Microsoft 365, the API for Salesforce.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The scopes, separated by spaces.</returns>
    public static string Scopes(string provider)
        => provider switch
        {
            Google => "https://www.googleapis.com/auth/calendar https://www.googleapis.com/auth/drive.readonly",
            Microsoft365 => "offline_access Calendars.ReadWrite Files.Read.All Sites.Read.All",
            Salesforce => "api refresh_token",
            _ => null,
        };

    private static string TenantOf(AIIntegrationConnection connection)
        => string.IsNullOrWhiteSpace(connection.TenantId) ? "common" : Uri.EscapeDataString(connection.TenantId.Trim());

    // A sandbox signs in at test.salesforce.com; the instance address says which.
    private static string SalesforceLogin(AIIntegrationConnection connection)
        => connection.InstanceUrl?.Contains("sandbox", StringComparison.OrdinalIgnoreCase) == true || connection.InstanceUrl?.Contains("test.salesforce.com", StringComparison.OrdinalIgnoreCase) == true
            ? "https://test.salesforce.com"
            : "https://login.salesforce.com";
}
