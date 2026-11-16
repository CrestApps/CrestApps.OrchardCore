using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// One way to be an approver: the name of an approver rule provider (<c>IApproverRuleProvider</c>), such as
/// Permission or Manager, and its value.
/// </summary>
public sealed class ApproverRule
{
    /// <summary>
    /// Gets or sets the name of the approver rule provider, one of <see cref="ApproverRuleKinds"/> or one a module
    /// adds. A rule stored before providers were open is read from its old number (0 Permission, 1 Role, 2 Manager,
    /// 3 Staff).
    /// </summary>
    [JsonConverter(typeof(ApproverRuleKindJsonConverter))]
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets what the provider reads, such as a permission name, a role name, or user or staff member ids
    /// separated by commas; empty for a provider that needs none, such as the manager.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Returns a copy.
    /// </summary>
    /// <returns>The copy.</returns>
    public ApproverRule Clone()
        => new() { Kind = Kind, Value = Value };
}

/// <summary>
/// The names of the approver rule providers Approvals ships.
/// </summary>
public static class ApproverRuleKinds
{
    /// <summary>
    /// Anyone who holds a permission (the value is the permission name).
    /// </summary>
    public const string Permission = "Permission";

    /// <summary>
    /// Anyone in a role (the value is the role name).
    /// </summary>
    public const string Role = "Role";

    /// <summary>
    /// Named users (the value is their user ids, separated by commas).
    /// </summary>
    public const string Users = "Users";

    /// <summary>
    /// A manager of the staff member the request is about, through the staff hierarchy (with Staff on).
    /// </summary>
    public const string Manager = "Manager";

    /// <summary>
    /// Named staff members (the value is their ids, separated by commas; with Staff on).
    /// </summary>
    public const string Staff = "Staff";
}

/// <summary>
/// Reads an approver rule's kind written as a name, or as the number of the enumeration it replaced, and writes the
/// name.
/// </summary>
public sealed class ApproverRuleKindJsonConverter : JsonConverter<string>
{
    private static readonly string[] _legacy = [ApproverRuleKinds.Permission, ApproverRuleKinds.Role, ApproverRuleKinds.Manager, ApproverRuleKinds.Staff];

    /// <inheritdoc />
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt32(out var number) => number >= 0 && number < _legacy.Length ? _legacy[number] : number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Null => null,
            _ => throw new JsonException("An approver rule's kind is a name or a number."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();

            return;
        }

        writer.WriteStringValue(value);
    }
}
