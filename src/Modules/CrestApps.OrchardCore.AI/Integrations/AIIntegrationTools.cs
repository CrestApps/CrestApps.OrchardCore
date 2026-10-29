using System.Globalization;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Integrations;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Integrations;

/// <summary>
/// The integration connections an AI profile's tools use.
/// </summary>
public sealed class AIProfileIntegrationSettings
{
    /// <summary>
    /// Gets or sets the CRM connection the profile looks contacts up in.
    /// </summary>
    public string CrmConnectionId { get; set; }
}

/// <summary>
/// The base of the integration tools: finds the conversation's profile and the connection it uses.
/// </summary>
public abstract class AIIntegrationTool : AIFunction
{
    private readonly JsonElement _jsonSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIIntegrationTool"/> class.
    /// </summary>
    /// <param name="properties">The JSON of the schema's properties.</param>
    /// <param name="required">The required properties.</param>
    protected AIIntegrationTool(string properties, string required = "")
    {
        _jsonSchema = JsonSerializer.Deserialize<JsonElement>($$"""{"type":"object","properties":{{{properties}}},"required":[{{required}}],"additionalProperties":false}""");
    }

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object> { ["Strict"] = false };

    /// <summary>
    /// Finds the connection the conversation's profile uses.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="pick">Picks the connection id from the profile's settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connection and the settings, or an explanation for the AI.</returns>
    protected static async Task<(AIIntegrationConnection Connection, AIProfileIntegrationSettings Settings, string Problem)> FindConnectionAsync(
        IServiceProvider services,
        Func<AIProfileIntegrationSettings, string> pick,
        CancellationToken cancellationToken)
    {
        var profile = AIConversationScope.Current?.Profile;
        var settings = profile?.GetSettings<AIProfileIntegrationSettings>() ?? new AIProfileIntegrationSettings();
        var id = pick(settings);

        if (string.IsNullOrEmpty(id))
        {
            return (null, settings, "No connection is set up for this. Tell the customer a person will follow up.");
        }

        var connection = await services.GetRequiredService<IAIIntegrationConnectionStore>().FindByIdAsync(id, cancellationToken);

        return connection is null || !connection.IsConnected
            ? (null, settings, "The connection is not signed in. Tell the customer a person will follow up.")
            : (connection, settings, null);
    }
}

/// <summary>
/// Looks the caller up in the profile's CRM.
/// </summary>
public sealed class FindCrmContactTool : AIIntegrationTool
{
    public const string TheName = "findCrmContact";

    public FindCrmContactTool()
        : base("""
            "phone": { "type": "string", "description": "The phone number to look up, in E.164 form. Defaults to the number the customer is calling from." }
            """)
    {
    }

    public override string Name => TheName;

    public override string Description => "Looks the customer up in the business's CRM by phone number and returns their name and email when found.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var (connection, _, problem) = await FindConnectionAsync(arguments.Services, settings => settings.CrmConnectionId, cancellationToken);

        if (problem is not null)
        {
            return problem;
        }

        var phone = arguments.TryGetFirstString("phone", out var given) && !string.IsNullOrWhiteSpace(given) ? given : AIConversationScope.Current?.CustomerAddress;

        if (string.IsNullOrWhiteSpace(phone))
        {
            return "There is no phone number to look up.";
        }

        var crm = await arguments.Services.GetRequiredService<AIIntegrationClients>().CreateCrmAsync(connection, cancellationToken);
        var contact = await crm.FindByPhoneAsync(phone, cancellationToken);

        return contact is null
            ? "The customer is not in the CRM."
            : JsonSerializer.Serialize(new { Found = true, contact.FirstName, contact.LastName, contact.Email });
    }
}
