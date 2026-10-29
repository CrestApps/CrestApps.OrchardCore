using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Core.Integrations;

/// <summary>
/// A contact in a CRM.
/// </summary>
public sealed class AICrmContact
{
    /// <summary>Gets or sets the CRM's id, empty for a new contact.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the first name.</summary>
    public string FirstName { get; set; }

    /// <summary>Gets or sets the last name.</summary>
    public string LastName { get; set; }

    /// <summary>Gets or sets the email address.</summary>
    public string Email { get; set; }

    /// <summary>Gets or sets the phone number.</summary>
    public string Phone { get; set; }

    /// <summary>Gets or sets other fields, by the CRM's own field name.</summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A call or conversation logged on a CRM contact.
/// </summary>
public sealed class AICrmActivity
{
    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the notes, such as the summary and outcome.</summary>
    public string Notes { get; set; }

    /// <summary>Gets or sets when it happened.</summary>
    public DateTime OccurredUtc { get; set; }

    /// <summary>Gets or sets how long it lasted.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Gets or sets a value indicating whether the customer called in.</summary>
    public bool Inbound { get; set; }
}

/// <summary>
/// A CRM the AI looks contacts up in and logs conversations to.
/// </summary>
public interface IAICrmClient
{
    /// <summary>
    /// Finds a contact by phone number.
    /// </summary>
    /// <param name="phone">The number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The contact, or <see langword="null"/>.</returns>
    Task<AICrmContact> FindByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a contact, or updates it when it has an id.
    /// </summary>
    /// <param name="contact">The contact.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The contact's id.</returns>
    Task<string> SaveAsync(AICrmContact contact, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a call on a contact.
    /// </summary>
    /// <param name="contactId">The contact.</param>
    /// <param name="activity">The call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The logged activity's id.</returns>
    Task<string> LogCallAsync(string contactId, AICrmActivity activity, CancellationToken cancellationToken = default);
}

/// <summary>
/// HubSpot CRM, through its v3 objects API.
/// </summary>
public sealed class HubSpotCrmClient : IAICrmClient
{
    private const string BaseUrl = "https://api.hubapi.com/crm/v3/objects/";

    // HubSpot's own association between a call and a contact.
    private const int CallToContactAssociationTypeId = 194;

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HubSpotCrmClient"/> class.
    /// </summary>
    /// <param name="httpClient">A client that carries the private app token.</param>
    public HubSpotCrmClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<AICrmContact> FindByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var body = new JsonObject
        {
            ["filterGroups"] = new JsonArray(
                Filter("phone", phone.Trim()),
                Filter("mobilephone", phone.Trim())),
            ["properties"] = new JsonArray("firstname", "lastname", "email", "phone", "mobilephone"),
            ["limit"] = 1,
        };

        using var response = await _httpClient.PostAsJsonAsync(BaseUrl + "contacts/search", body, cancellationToken);
        var json = await AIIntegrationHttp.ReadAsync(response, "HubSpot", cancellationToken);

        if (!json.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
        {
            return null;
        }

        var contact = results[0];
        var properties = contact.GetProperty("properties");

        return new AICrmContact
        {
            Id = AIIntegrationHttp.String(contact, "id"),
            FirstName = AIIntegrationHttp.String(properties, "firstname"),
            LastName = AIIntegrationHttp.String(properties, "lastname"),
            Email = AIIntegrationHttp.String(properties, "email"),
            Phone = AIIntegrationHttp.String(properties, "phone") ?? AIIntegrationHttp.String(properties, "mobilephone"),
        };
    }

    /// <inheritdoc/>
    public async Task<string> SaveAsync(AICrmContact contact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);

        var properties = new JsonObject();
        Set(properties, "firstname", contact.FirstName);
        Set(properties, "lastname", contact.LastName);
        Set(properties, "email", contact.Email);
        Set(properties, "phone", contact.Phone);

        foreach (var (name, value) in contact.Fields)
        {
            Set(properties, name, value);
        }

        using var response = string.IsNullOrEmpty(contact.Id)
            ? await _httpClient.PostAsJsonAsync(BaseUrl + "contacts", new JsonObject { ["properties"] = properties }, cancellationToken)
            : await _httpClient.PatchAsJsonAsync($"{BaseUrl}contacts/{Uri.EscapeDataString(contact.Id)}", new JsonObject { ["properties"] = properties }, cancellationToken);
        var json = await AIIntegrationHttp.ReadAsync(response, "HubSpot", cancellationToken);

        return AIIntegrationHttp.String(json, "id") ?? contact.Id;
    }

    /// <inheritdoc/>
    public async Task<string> LogCallAsync(string contactId, AICrmActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var body = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["hs_timestamp"] = activity.OccurredUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["hs_call_title"] = activity.Title,
                ["hs_call_body"] = activity.Notes,
                ["hs_call_duration"] = ((long)activity.Duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
                ["hs_call_direction"] = activity.Inbound ? "INBOUND" : "OUTBOUND",
                ["hs_call_status"] = "COMPLETED",
            },
            ["associations"] = new JsonArray(new JsonObject
            {
                ["to"] = new JsonObject { ["id"] = contactId },
                ["types"] = new JsonArray(new JsonObject { ["associationCategory"] = "HUBSPOT_DEFINED", ["associationTypeId"] = CallToContactAssociationTypeId }),
            }),
        };

        using var response = await _httpClient.PostAsJsonAsync(BaseUrl + "calls", body, cancellationToken);
        var json = await AIIntegrationHttp.ReadAsync(response, "HubSpot", cancellationToken);

        return AIIntegrationHttp.String(json, "id");
    }

    private static JsonObject Filter(string property, string value)
        => new()
        {
            ["filters"] = new JsonArray(new JsonObject { ["propertyName"] = property, ["operator"] = "EQ", ["value"] = value }),
        };

    private static void Set(JsonObject properties, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
        {
            properties[name.Trim()] = value.Trim();
        }
    }
}

/// <summary>
/// Salesforce, through its REST API: SOQL to find a contact, sObjects to save it and log a completed call task.
/// </summary>
public sealed class SalesforceCrmClient : IAICrmClient
{
    /// <summary>
    /// The REST API version used.
    /// </summary>
    public const string ApiVersion = "v61.0";

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="SalesforceCrmClient"/> class.
    /// </summary>
    /// <param name="httpClient">A client that carries the connection's token.</param>
    /// <param name="instanceUrl">The org's instance address.</param>
    public SalesforceCrmClient(HttpClient httpClient, string instanceUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceUrl);

        _httpClient = httpClient;
        _baseUrl = $"{instanceUrl.TrimEnd('/')}/services/data/{ApiVersion}/";
    }

    /// <inheritdoc/>
    public async Task<AICrmContact> FindByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var quoted = Escape(phone.Trim());
        var soql = $"SELECT Id, FirstName, LastName, Email, Phone FROM Contact WHERE Phone = '{quoted}' OR MobilePhone = '{quoted}' LIMIT 1";

        using var response = await _httpClient.GetAsync($"{_baseUrl}query?q={Uri.EscapeDataString(soql)}", cancellationToken);
        var json = await AIIntegrationHttp.ReadAsync(response, "Salesforce", cancellationToken);

        if (!json.TryGetProperty("records", out var records) || records.GetArrayLength() == 0)
        {
            return null;
        }

        var record = records[0];

        return new AICrmContact
        {
            Id = AIIntegrationHttp.String(record, "Id"),
            FirstName = AIIntegrationHttp.String(record, "FirstName"),
            LastName = AIIntegrationHttp.String(record, "LastName"),
            Email = AIIntegrationHttp.String(record, "Email"),
            Phone = AIIntegrationHttp.String(record, "Phone"),
        };
    }

    /// <inheritdoc/>
    public async Task<string> SaveAsync(AICrmContact contact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);

        var body = new JsonObject();
        Set(body, "FirstName", contact.FirstName);

        // A contact needs a last name; a caller known only by number gets one that says so.
        Set(body, "LastName", string.IsNullOrWhiteSpace(contact.LastName) && string.IsNullOrEmpty(contact.Id) ? contact.Phone ?? "Unknown" : contact.LastName);
        Set(body, "Email", contact.Email);
        Set(body, "Phone", contact.Phone);

        foreach (var (name, value) in contact.Fields)
        {
            Set(body, name, value);
        }

        if (string.IsNullOrEmpty(contact.Id))
        {
            using var created = await _httpClient.PostAsJsonAsync(_baseUrl + "sobjects/Contact", body, cancellationToken);
            var json = await AIIntegrationHttp.ReadAsync(created, "Salesforce", cancellationToken);

            return AIIntegrationHttp.String(json, "id");
        }

        using var updated = await _httpClient.PatchAsJsonAsync($"{_baseUrl}sobjects/Contact/{Uri.EscapeDataString(contact.Id)}", body, cancellationToken);
        await AIIntegrationHttp.ReadAsync(updated, "Salesforce", cancellationToken);

        return contact.Id;
    }

    /// <inheritdoc/>
    public async Task<string> LogCallAsync(string contactId, AICrmActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var body = new JsonObject
        {
            ["WhoId"] = contactId,
            ["Subject"] = string.IsNullOrWhiteSpace(activity.Title) ? "Call" : activity.Title,
            ["Description"] = activity.Notes,
            ["Status"] = "Completed",
            ["TaskSubtype"] = "Call",
            ["CallType"] = activity.Inbound ? "Inbound" : "Outbound",
            ["CallDurationInSeconds"] = (int)activity.Duration.TotalSeconds,
            ["ActivityDate"] = activity.OccurredUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        using var response = await _httpClient.PostAsJsonAsync(_baseUrl + "sobjects/Task", body, cancellationToken);
        var json = await AIIntegrationHttp.ReadAsync(response, "Salesforce", cancellationToken);

        return AIIntegrationHttp.String(json, "id");
    }

    /// <summary>
    /// Escapes a value for a SOQL string literal.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The escaped value.</returns>
    public static string Escape(string value)
        => (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    private static void Set(JsonObject body, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
        {
            body[name.Trim()] = value.Trim();
        }
    }
}

/// <summary>
/// Makes the CRM client of a connection.
/// </summary>
public sealed class AIIntegrationClients
{
    private readonly AIIntegrationTokens _tokens;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIIntegrationClients"/> class.
    /// </summary>
    /// <param name="tokens">Signs the clients in.</param>
    public AIIntegrationClients(AIIntegrationTokens tokens)
    {
        _tokens = tokens;
    }

    /// <summary>
    /// Makes a connection's CRM client.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The client.</returns>
    public async Task<IAICrmClient> CreateCrmAsync(AIIntegrationConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var http = await _tokens.CreateClientAsync(connection, cancellationToken);

        return connection.Provider switch
        {
            AIIntegrationProviders.HubSpot => new HubSpotCrmClient(http),
            AIIntegrationProviders.Salesforce => new SalesforceCrmClient(http, connection.InstanceUrl),
            _ => throw new InvalidOperationException($"A {connection.Provider} connection is not a CRM."),
        };
    }
}
