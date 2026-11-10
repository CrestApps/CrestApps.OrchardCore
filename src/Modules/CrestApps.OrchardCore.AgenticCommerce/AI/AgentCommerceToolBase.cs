using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AgenticCommerce.AI;

/// <summary>
/// The base of the store's commerce AI tools.
/// </summary>
public abstract class AgentCommerceToolBase : AIFunction
{
    /// <summary>
    /// The id of the store's own AI assistant, which its checkouts are kept under.
    /// </summary>
    public const string AssistantPlatformId = "site-assistant";

    /// <summary>
    /// The answer of the cart tools when they run outside a visitor's request, so there is no cart to use.
    /// </summary>
    protected const string NoVisitorMessage = "The cart can only be used by a visitor of the store's pages.";

    /// <summary>
    /// The JSON options the tools answer with.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object> { ["Strict"] = false };

    /// <summary>
    /// Returns a string argument, or <see langword="null"/>.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    protected static string Read(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element ? (element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()) : value.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Returns a JSON schema.
    /// </summary>
    /// <param name="schema">The schema's text.</param>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);
}
