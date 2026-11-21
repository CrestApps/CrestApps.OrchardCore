using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// The custom parameters of a launch: the tool's and the course item's, one <c>name=value</c> per line, the item's
/// winning over the tool's. A value that is exactly a supported substitution variable is replaced; a variable the
/// platform does not support, or whose value the tool's privacy level withholds, is sent as it was written, as LTI 1.3
/// Core asks.
/// </summary>
public static class LtiCustomParameters
{
    /// <summary>
    /// The context (course) id.
    /// </summary>
    public const string ContextId = "$Context.id";

    /// <summary>
    /// The context title.
    /// </summary>
    public const string ContextTitle = "$Context.title";

    /// <summary>
    /// The resource link (course item) id.
    /// </summary>
    public const string ResourceLinkId = "$ResourceLink.id";

    /// <summary>
    /// The resource link title.
    /// </summary>
    public const string ResourceLinkTitle = "$ResourceLink.title";

    /// <summary>
    /// The id the tool knows the person by (never the user id).
    /// </summary>
    public const string UserId = "$User.id";

    /// <summary>
    /// The person's full name, when the privacy level allows it.
    /// </summary>
    public const string PersonNameFull = "$Person.name.full";

    /// <summary>
    /// The person's given name, when the privacy level allows it.
    /// </summary>
    public const string PersonNameGiven = "$Person.name.given";

    /// <summary>
    /// The person's family name, when the privacy level allows it.
    /// </summary>
    public const string PersonNameFamily = "$Person.name.family";

    /// <summary>
    /// The person's e-mail address, when the privacy level allows it.
    /// </summary>
    public const string PersonEmailPrimary = "$Person.email.primary";

    /// <summary>
    /// The person's context roles, separated by commas.
    /// </summary>
    public const string MembershipRole = "$Membership.role";

    /// <summary>
    /// Gets the substitution variables Learning supports.
    /// </summary>
    public static IReadOnlyList<string> SupportedVariables { get; } =
    [
        ContextId,
        ContextTitle,
        ResourceLinkId,
        ResourceLinkTitle,
        UserId,
        PersonNameFull,
        PersonNameGiven,
        PersonNameFamily,
        PersonEmailPrimary,
        MembershipRole,
    ];

    /// <summary>
    /// Returns the values of the supported variables for a launch, leaving out those the launch has no value for.
    /// </summary>
    /// <param name="context">The launch.</param>
    /// <returns>The values by variable.</returns>
    public static IReadOnlyDictionary<string, string> VariablesOf(LtiLaunchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var variables = new Dictionary<string, string>(StringComparer.Ordinal);

        void Add(string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                variables[name] = value;
            }
        }

        Add(ContextId, context.CourseId);
        Add(ContextTitle, context.CourseTitle);
        Add(ResourceLinkId, context.ItemId);
        Add(ResourceLinkTitle, context.ItemTitle);
        Add(UserId, context.Subject);
        Add(PersonNameFull, context.Name);
        Add(PersonNameGiven, context.GivenName);
        Add(PersonNameFamily, context.FamilyName);
        Add(PersonEmailPrimary, context.Email);
        Add(MembershipRole, context.Roles is { Count: > 0 } roles ? string.Join(",", roles) : null);

        return variables;
    }

    /// <summary>
    /// Merges and resolves the custom parameters.
    /// </summary>
    /// <param name="toolParameters">The tool's parameters, one <c>name=value</c> per line.</param>
    /// <param name="linkParameters">The course item's parameters, one <c>name=value</c> per line.</param>
    /// <param name="variables">The values of the supported variables for this launch.</param>
    /// <returns>The <c>custom</c> claim; empty when there are no parameters.</returns>
    public static JsonObject Resolve(string toolParameters, string linkParameters, IReadOnlyDictionary<string, string> variables)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (name, value) in Parse(toolParameters))
        {
            merged[name] = value;
        }

        foreach (var (name, value) in Parse(linkParameters))
        {
            merged[name] = value;
        }

        var custom = new JsonObject();

        foreach (var (name, value) in merged)
        {
            custom[name] = value.StartsWith('$') && variables is not null && variables.TryGetValue(value, out var substituted)
                ? substituted
                : value;
        }

        return custom;
    }

    /// <summary>
    /// Reads parameters, ignoring empty lines and lines without a name.
    /// </summary>
    /// <param name="parameters">One <c>name=value</c> per line.</param>
    /// <returns>The names and values, in order; a later line wins over an earlier one.</returns>
    public static IReadOnlyDictionary<string, string> Parse(string parameters)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(parameters))
        {
            return result;
        }

        foreach (var line in parameters.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('=');

            if (separator <= 0)
            {
                continue;
            }

            var name = line.Substring(0, separator).Trim();

            if (name.Length > 0)
            {
                result[name] = line.Substring(separator + 1).Trim();
            }
        }

        return result;
    }
}
