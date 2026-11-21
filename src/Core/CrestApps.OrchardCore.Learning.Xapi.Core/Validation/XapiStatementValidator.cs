using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Validation;

/// <summary>
/// Validates statements against xAPI 1.0.3, Part Two: every object of a statement, its properties (a property that
/// is not defined, a property in another case and a <see langword="null"/> value are refused), the identifiers of
/// agents and groups, verbs, activities and their definitions, statement references and sub-statements, results,
/// contexts, timestamps, language maps, extensions and attachments. A validation returns why the statement is not
/// valid, or <see langword="null"/>.
/// </summary>
public static partial class XapiStatementValidator
{
    private static readonly string[] _statementProperties = ["id", "actor", "verb", "object", "result", "context", "timestamp", "stored", "authority", "version", "attachments"];
    private static readonly string[] _subStatementProperties = ["objectType", "actor", "verb", "object", "result", "context", "timestamp", "attachments"];
    private static readonly string[] _agentProperties = ["objectType", "name", "mbox", "mbox_sha1sum", "openid", "account"];
    private static readonly string[] _groupProperties = ["objectType", "name", "mbox", "mbox_sha1sum", "openid", "account", "member"];
    private static readonly string[] _accountProperties = ["homePage", "name"];
    private static readonly string[] _verbProperties = ["id", "display"];
    private static readonly string[] _activityProperties = ["objectType", "id", "definition"];
    private static readonly string[] _definitionProperties = ["name", "description", "type", "moreInfo", "extensions", "interactionType", "correctResponsesPattern", "choices", "scale", "source", "target", "steps"];
    private static readonly string[] _componentLists = ["choices", "scale", "source", "target", "steps"];
    private static readonly string[] _componentProperties = ["id", "description"];
    private static readonly string[] _statementRefProperties = ["objectType", "id"];
    private static readonly string[] _resultProperties = ["score", "success", "completion", "response", "duration", "extensions"];
    private static readonly string[] _scoreProperties = ["scaled", "raw", "min", "max"];
    private static readonly string[] _contextProperties = ["registration", "instructor", "team", "contextActivities", "revision", "platform", "language", "statement", "extensions"];
    private static readonly string[] _contextActivityKinds = ["parent", "grouping", "category", "other"];
    private static readonly string[] _attachmentProperties = ["usageType", "display", "description", "contentType", "length", "sha2", "fileUrl"];
    private static readonly string[] _identifiers = ["mbox", "mbox_sha1sum", "openid", "account"];

    private static readonly HashSet<string> _interactionTypes = new(StringComparer.Ordinal)
    {
        "true-false", "choice", "fill-in", "long-fill-in", "matching", "performance", "sequencing", "likert", "numeric", "other",
    };

    /// <summary>
    /// Validates a statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>Why the statement is not valid, or <see langword="null"/>.</returns>
    public static string Validate(JsonNode statement)
        => ValidateStatement(statement, "statement", sub: false);

    /// <summary>
    /// Validates an agent or a group.
    /// </summary>
    /// <param name="agent">The agent or group.</param>
    /// <param name="path">Where it is, for the message.</param>
    /// <param name="requireIdentifier">Whether an anonymous group is refused, as in a query's <c>agent</c> parameter
    /// or a document's agent.</param>
    /// <returns>Why it is not valid, or <see langword="null"/>.</returns>
    public static string ValidateAgent(JsonNode agent, string path, bool requireIdentifier)
    {
        var error = ValidateActor(agent, path);

        if (error is not null)
        {
            return error;
        }

        return requireIdentifier && CountIdentifiers(agent.AsObject()) == 0
            ? $"{path} must be identified by mbox, mbox_sha1sum, openid or account."
            : null;
    }

    /// <summary>
    /// Gets whether the LRS accepts a request with a version header: <c>1.0</c> or <c>1.0.</c> and a patch number.
    /// </summary>
    /// <param name="header">The value of <c>X-Experience-API-Version</c>.</param>
    /// <returns><see langword="true"/> for a 1.0 version.</returns>
    public static bool IsSupportedVersion(string header)
        => !string.IsNullOrEmpty(header) && VersionRegex().IsMatch(header.Trim());

    private static string ValidateStatement(JsonNode node, string path, bool sub)
    {
        if (node is not JsonObject statement)
        {
            return $"{path} must be a JSON object.";
        }

        var error = CheckProperties(statement, sub ? _subStatementProperties : _statementProperties, path);

        if (error is not null)
        {
            return error;
        }

        if (sub)
        {
            if (!IsString(statement["objectType"], "SubStatement"))
            {
                return $"{path}.objectType must be SubStatement.";
            }
        }
        else if (statement["id"] is { } id && (!TryGetString(id, out var idText) || !XapiFormats.IsUuid(idText)))
        {
            return $"{path}.id must be a UUID.";
        }

        if (statement["actor"] is null)
        {
            return $"{path}.actor is required.";
        }

        if (statement["verb"] is null)
        {
            return $"{path}.verb is required.";
        }

        if (statement["object"] is null)
        {
            return $"{path}.object is required.";
        }

        error = ValidateActor(statement["actor"], path + ".actor")
            ?? ValidateVerb(statement["verb"], path + ".verb")
            ?? ValidateObject(statement["object"], path + ".object", sub);

        if (error is not null)
        {
            return error;
        }

        var verbId = statement["verb"]["id"].GetValue<string>();
        var objectType = ObjectTypeOf(statement["object"]);

        if (string.Equals(verbId, XapiConstants.Verbs.Voided, StringComparison.Ordinal) && objectType != "StatementRef")
        {
            return $"{path}.object of a voiding statement must be a StatementRef.";
        }

        if (statement["result"] is { } result && (error = ValidateResult(result, path + ".result")) is not null)
        {
            return error;
        }

        if (statement["context"] is { } context && (error = ValidateContext(context, path + ".context", objectType)) is not null)
        {
            return error;
        }

        if (statement["timestamp"] is { } timestamp && (!TryGetString(timestamp, out var timestampText) || !XapiFormats.TryParseTimestamp(timestampText, out _)))
        {
            return $"{path}.timestamp must be an ISO 8601 timestamp.";
        }

        if (!sub)
        {
            if (statement["stored"] is { } stored && (!TryGetString(stored, out var storedText) || !XapiFormats.TryParseTimestamp(storedText, out _)))
            {
                return $"{path}.stored must be an ISO 8601 timestamp.";
            }

            if (statement["authority"] is { } authority && (error = ValidateActor(authority, path + ".authority")) is not null)
            {
                return error;
            }

            if (statement["version"] is { } version && (!TryGetString(version, out var versionText) || !StatementVersionRegex().IsMatch(versionText)))
            {
                return $"{path}.version must be 1.0.0 or another 1.0 version.";
            }
        }

        if (statement["attachments"] is { } attachments)
        {
            if (attachments is not JsonArray list)
            {
                return $"{path}.attachments must be an array.";
            }

            for (var index = 0; index < list.Count; index++)
            {
                if ((error = ValidateAttachment(list[index], $"{path}.attachments[{index}]")) is not null)
                {
                    return error;
                }
            }
        }

        return null;
    }

    private static string ValidateActor(JsonNode node, string path)
    {
        if (node is not JsonObject agent)
        {
            return $"{path} must be a JSON object.";
        }

        var objectType = agent["objectType"];

        if (objectType is not null && !IsString(objectType, "Agent") && !IsString(objectType, "Group"))
        {
            return $"{path}.objectType must be Agent or Group.";
        }

        var isGroup = IsString(objectType, "Group");
        var error = CheckProperties(agent, isGroup ? _groupProperties : _agentProperties, path)
            ?? ValidateIdentifiers(agent, path);

        if (error is not null)
        {
            return error;
        }

        if (agent["name"] is { } name && !TryGetString(name, out _))
        {
            return $"{path}.name must be a string.";
        }

        var identifiers = CountIdentifiers(agent);

        if (!isGroup)
        {
            return identifiers == 1 ? null : $"{path} must have exactly one of mbox, mbox_sha1sum, openid and account.";
        }

        if (identifiers > 1)
        {
            return $"{path} must have at most one of mbox, mbox_sha1sum, openid and account.";
        }

        var members = agent["member"];

        if (members is null)
        {
            return identifiers == 0 ? $"{path} is an anonymous group, which must list its members." : null;
        }

        if (members is not JsonArray list)
        {
            return $"{path}.member must be an array.";
        }

        if (identifiers == 0 && list.Count == 0)
        {
            return $"{path} is an anonymous group, which must list its members.";
        }

        for (var index = 0; index < list.Count; index++)
        {
            var member = list[index];

            if (member is JsonObject memberObject && IsString(memberObject["objectType"], "Group"))
            {
                return $"{path}.member[{index}] must be an agent, not a group.";
            }

            if ((error = ValidateActor(member, $"{path}.member[{index}]")) is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static int CountIdentifiers(JsonObject agent)
        => _identifiers.Count(identifier => agent[identifier] is not null);

    private static string ValidateIdentifiers(JsonObject agent, string path)
    {
        if (agent["mbox"] is { } mbox && (!TryGetString(mbox, out var mboxText) || !XapiFormats.IsMailbox(mboxText)))
        {
            return $"{path}.mbox must be a mailto: IRI.";
        }

        if (agent["mbox_sha1sum"] is { } sha1 && (!TryGetString(sha1, out var sha1Text) || !XapiFormats.IsSha1(sha1Text)))
        {
            return $"{path}.mbox_sha1sum must be a SHA-1 hash in hex.";
        }

        if (agent["openid"] is { } openId && (!TryGetString(openId, out var openIdText) || !XapiFormats.IsIri(openIdText)))
        {
            return $"{path}.openid must be a URI.";
        }

        if (agent["account"] is { } accountNode)
        {
            if (accountNode is not JsonObject account)
            {
                return $"{path}.account must be a JSON object.";
            }

            var error = CheckProperties(account, _accountProperties, path + ".account");

            if (error is not null)
            {
                return error;
            }

            if (!TryGetString(account["homePage"], out var homePage) || !XapiFormats.IsIrl(homePage))
            {
                return $"{path}.account.homePage must be an IRL.";
            }

            if (!TryGetString(account["name"], out var name) || name.Length == 0)
            {
                return $"{path}.account.name must be a string.";
            }
        }

        return null;
    }

    private static string ValidateVerb(JsonNode node, string path)
    {
        if (node is not JsonObject verb)
        {
            return $"{path} must be a JSON object.";
        }

        var error = CheckProperties(verb, _verbProperties, path);

        if (error is not null)
        {
            return error;
        }

        if (!TryGetString(verb["id"], out var id) || !XapiFormats.IsIri(id))
        {
            return $"{path}.id must be an IRI.";
        }

        return verb["display"] is { } display ? ValidateLanguageMap(display, path + ".display") : null;
    }

    private static string ValidateObject(JsonNode node, string path, bool insideSubStatement)
    {
        if (node is not JsonObject value)
        {
            return $"{path} must be a JSON object.";
        }

        var objectType = value["objectType"];

        if (objectType is not null && !TryGetString(objectType, out _))
        {
            return $"{path}.objectType must be a string.";
        }

        switch (ObjectTypeOf(value))
        {
            case "Activity":
                return ValidateActivity(value, path);
            case "Agent":
            case "Group":
                return ValidateActor(value, path);
            case "StatementRef":
                return ValidateStatementRef(value, path);
            case "SubStatement":
                return insideSubStatement
                    ? $"{path} cannot be a SubStatement inside a SubStatement."
                    : ValidateStatement(value, path, sub: true);
            default:
                return $"{path}.objectType must be Activity, Agent, Group, StatementRef or SubStatement.";
        }
    }

    private static string ValidateStatementRef(JsonObject value, string path)
    {
        var error = CheckProperties(value, _statementRefProperties, path);

        if (error is not null)
        {
            return error;
        }

        if (!IsString(value["objectType"], "StatementRef"))
        {
            return $"{path}.objectType must be StatementRef.";
        }

        return TryGetString(value["id"], out var id) && XapiFormats.IsUuid(id) ? null : $"{path}.id must be a UUID.";
    }

    private static string ValidateActivity(JsonNode node, string path)
    {
        if (node is not JsonObject activity)
        {
            return $"{path} must be a JSON object.";
        }

        var error = CheckProperties(activity, _activityProperties, path);

        if (error is not null)
        {
            return error;
        }

        if (activity["objectType"] is { } objectType && !IsString(objectType, "Activity"))
        {
            return $"{path}.objectType must be Activity.";
        }

        if (!TryGetString(activity["id"], out var id) || !XapiFormats.IsIri(id))
        {
            return $"{path}.id must be an IRI.";
        }

        return activity["definition"] is { } definition ? ValidateDefinition(definition, path + ".definition") : null;
    }

    private static string ValidateDefinition(JsonNode node, string path)
    {
        if (node is not JsonObject definition)
        {
            return $"{path} must be a JSON object.";
        }

        var error = CheckProperties(definition, _definitionProperties, path);

        if (error is not null)
        {
            return error;
        }

        if (definition["name"] is { } name && (error = ValidateLanguageMap(name, path + ".name")) is not null)
        {
            return error;
        }

        if (definition["description"] is { } description && (error = ValidateLanguageMap(description, path + ".description")) is not null)
        {
            return error;
        }

        if (definition["type"] is { } type && (!TryGetString(type, out var typeText) || !XapiFormats.IsIri(typeText)))
        {
            return $"{path}.type must be an IRI.";
        }

        if (definition["moreInfo"] is { } moreInfo && (!TryGetString(moreInfo, out var moreInfoText) || !XapiFormats.IsIrl(moreInfoText)))
        {
            return $"{path}.moreInfo must be an IRL.";
        }

        if (definition["extensions"] is { } extensions && (error = ValidateExtensions(extensions, path + ".extensions")) is not null)
        {
            return error;
        }

        if (definition["interactionType"] is { } interactionType && (!TryGetString(interactionType, out var interactionText) || !_interactionTypes.Contains(interactionText)))
        {
            return $"{path}.interactionType must be one of the interaction types of xAPI.";
        }

        if (definition["correctResponsesPattern"] is { } pattern &&
            (pattern is not JsonArray patterns || patterns.Any(item => !TryGetString(item, out _))))
        {
            return $"{path}.correctResponsesPattern must be an array of strings.";
        }

        foreach (var listName in _componentLists)
        {
            if (definition[listName] is not { } listNode)
            {
                continue;
            }

            if (listNode is not JsonArray list)
            {
                return $"{path}.{listName} must be an array.";
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < list.Count; index++)
            {
                var itemPath = $"{path}.{listName}[{index}]";

                if (list[index] is not JsonObject component)
                {
                    return $"{itemPath} must be a JSON object.";
                }

                if ((error = CheckProperties(component, _componentProperties, itemPath)) is not null)
                {
                    return error;
                }

                if (!TryGetString(component["id"], out var componentId) || componentId.Length == 0)
                {
                    return $"{itemPath}.id must be a string.";
                }

                if (!ids.Add(componentId))
                {
                    return $"{path}.{listName} has the id {componentId} twice.";
                }

                if (component["description"] is { } componentDescription && (error = ValidateLanguageMap(componentDescription, itemPath + ".description")) is not null)
                {
                    return error;
                }
            }
        }

        return null;
    }

    private static string ValidateResult(JsonNode node, string path)
    {
        if (node is not JsonObject result)
        {
            return $"{path} must be a JSON object.";
        }

        var error = CheckProperties(result, _resultProperties, path);

        if (error is not null)
        {
            return error;
        }

        if (result["success"] is { } success && success.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
        {
            return $"{path}.success must be true or false.";
        }

        if (result["completion"] is { } completion && completion.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
        {
            return $"{path}.completion must be true or false.";
        }

        if (result["response"] is { } response && !TryGetString(response, out _))
        {
            return $"{path}.response must be a string.";
        }

        if (result["duration"] is { } duration && (!TryGetString(duration, out var durationText) || !XapiFormats.IsDuration(durationText)))
        {
            return $"{path}.duration must be an ISO 8601 duration.";
        }

        if (result["extensions"] is { } extensions && (error = ValidateExtensions(extensions, path + ".extensions")) is not null)
        {
            return error;
        }

        if (result["score"] is not { } scoreNode)
        {
            return null;
        }

        if (scoreNode is not JsonObject score)
        {
            return $"{path}.score must be a JSON object.";
        }

        if ((error = CheckProperties(score, _scoreProperties, path + ".score")) is not null)
        {
            return error;
        }

        decimal? scaled = null, raw = null, min = null, max = null;

        foreach (var (name, value) in score)
        {
            if (!TryGetNumber(value, out var number))
            {
                return $"{path}.score.{name} must be a number.";
            }

            switch (name)
            {
                case "scaled":
                    scaled = number;
                    break;
                case "raw":
                    raw = number;
                    break;
                case "min":
                    min = number;
                    break;
                default:
                    max = number;
                    break;
            }
        }

        if (scaled is < -1 or > 1)
        {
            return $"{path}.score.scaled must be from -1 to 1.";
        }

        if (min is not null && max is not null && min >= max)
        {
            return $"{path}.score.min must be less than max.";
        }

        if (raw is not null && ((min is not null && raw < min) || (max is not null && raw > max)))
        {
            return $"{path}.score.raw must be from min to max.";
        }

        return null;
    }

    private static string ValidateContext(JsonNode node, string path, string objectType)
    {
        if (node is not JsonObject context)
        {
            return $"{path} must be a JSON object.";
        }

        var error = CheckProperties(context, _contextProperties, path);

        if (error is not null)
        {
            return error;
        }

        if (context["registration"] is { } registration && (!TryGetString(registration, out var registrationText) || !XapiFormats.IsUuid(registrationText)))
        {
            return $"{path}.registration must be a UUID.";
        }

        if (context["instructor"] is { } instructor && (error = ValidateActor(instructor, path + ".instructor")) is not null)
        {
            return error;
        }

        if (context["team"] is { } team)
        {
            if (team is not JsonObject teamObject || !IsString(teamObject["objectType"], "Group"))
            {
                return $"{path}.team must be a group.";
            }

            if ((error = ValidateActor(team, path + ".team")) is not null)
            {
                return error;
            }
        }

        if (context["revision"] is { } revision)
        {
            if (!TryGetString(revision, out _))
            {
                return $"{path}.revision must be a string.";
            }

            if (objectType != "Activity")
            {
                return $"{path}.revision is only allowed when the object is an activity.";
            }
        }

        if (context["platform"] is { } platform)
        {
            if (!TryGetString(platform, out _))
            {
                return $"{path}.platform must be a string.";
            }

            if (objectType != "Activity")
            {
                return $"{path}.platform is only allowed when the object is an activity.";
            }
        }

        if (context["language"] is { } language && (!TryGetString(language, out var languageText) || !XapiFormats.IsLanguageTag(languageText)))
        {
            return $"{path}.language must be an RFC 5646 language tag.";
        }

        if (context["statement"] is { } reference)
        {
            if (reference is not JsonObject referenceObject)
            {
                return $"{path}.statement must be a StatementRef.";
            }

            if ((error = ValidateStatementRef(referenceObject, path + ".statement")) is not null)
            {
                return error;
            }
        }

        if (context["extensions"] is { } extensions && (error = ValidateExtensions(extensions, path + ".extensions")) is not null)
        {
            return error;
        }

        if (context["contextActivities"] is not { } activitiesNode)
        {
            return null;
        }

        if (activitiesNode is not JsonObject activities)
        {
            return $"{path}.contextActivities must be a JSON object.";
        }

        if ((error = CheckProperties(activities, _contextActivityKinds, path + ".contextActivities")) is not null)
        {
            return error;
        }

        foreach (var (kind, value) in activities)
        {
            var kindPath = $"{path}.contextActivities.{kind}";

            if (value is JsonArray list)
            {
                for (var index = 0; index < list.Count; index++)
                {
                    if ((error = ValidateActivity(list[index], $"{kindPath}[{index}]")) is not null)
                    {
                        return error;
                    }
                }
            }
            else if ((error = ValidateActivity(value, kindPath)) is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static string ValidateAttachment(JsonNode node, string path)
    {
        if (node is not JsonObject attachment)
        {
            return $"{path} must be a JSON object.";
        }

        var error = CheckProperties(attachment, _attachmentProperties, path);

        if (error is not null)
        {
            return error;
        }

        if (!TryGetString(attachment["usageType"], out var usageType) || !XapiFormats.IsIri(usageType))
        {
            return $"{path}.usageType must be an IRI.";
        }

        if (attachment["display"] is not { } display)
        {
            return $"{path}.display is required.";
        }

        if ((error = ValidateLanguageMap(display, path + ".display")) is not null)
        {
            return error;
        }

        if (attachment["description"] is { } description && (error = ValidateLanguageMap(description, path + ".description")) is not null)
        {
            return error;
        }

        if (!TryGetString(attachment["contentType"], out var contentType) || contentType.Length == 0)
        {
            return $"{path}.contentType must be an Internet media type.";
        }

        if (!TryGetNumber(attachment["length"], out var lengthValue) || lengthValue < 0 || lengthValue != decimal.Truncate(lengthValue))
        {
            return $"{path}.length must be a whole number of bytes.";
        }

        if (!TryGetString(attachment["sha2"], out var sha2) || !XapiFormats.IsSha2(sha2))
        {
            return $"{path}.sha2 must be a SHA-2 hash in hex.";
        }

        if (attachment["fileUrl"] is { } fileUrl && (!TryGetString(fileUrl, out var fileUrlText) || !XapiFormats.IsIrl(fileUrlText)))
        {
            return $"{path}.fileUrl must be an IRL.";
        }

        return null;
    }

    private static string ValidateLanguageMap(JsonNode node, string path)
    {
        if (node is not JsonObject map)
        {
            return $"{path} must be a language map.";
        }

        foreach (var (language, value) in map)
        {
            if (!XapiFormats.IsLanguageTag(language))
            {
                return $"{path} has a key that is not an RFC 5646 language tag: {language}.";
            }

            if (!TryGetString(value, out _))
            {
                return $"{path}.{language} must be a string.";
            }
        }

        return null;
    }

    private static string ValidateExtensions(JsonNode node, string path)
    {
        if (node is not JsonObject extensions)
        {
            return $"{path} must be a JSON object.";
        }

        foreach (var (key, _) in extensions)
        {
            if (!XapiFormats.IsIri(key))
            {
                return $"{path} has a key that is not an IRI: {key}.";
            }
        }

        return null;
    }

    private static string CheckProperties(JsonObject value, string[] allowed, string path)
    {
        foreach (var (name, property) in value)
        {
            if (Array.IndexOf(allowed, name) < 0)
            {
                return $"{path} has a property xAPI does not define here: {name}.";
            }

            if (property is null)
            {
                return $"{path}.{name} must not be null.";
            }
        }

        return null;
    }

    internal static string ObjectTypeOf(JsonNode value)
        => value?["objectType"] is JsonValue type && type.TryGetValue<string>(out var text) ? text : "Activity";

    private static bool IsString(JsonNode node, string expected)
        => TryGetString(node, out var text) && string.Equals(text, expected, StringComparison.Ordinal);

    private static bool TryGetString(JsonNode node, out string text)
    {
        text = null;

        return node is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.TryGetValue(out text);
    }

    /// <summary>
    /// Reads a JSON number as a decimal, whatever the node holds.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="number">The number.</param>
    /// <returns><see langword="true"/> when the value is a number.</returns>
    public static bool TryGetNumber(JsonNode node, out decimal number)
    {
        number = 0;

        // The JSON text of the number, whatever the node holds: a parsed element or a value made in code.
        return node is JsonValue value &&
            value.GetValueKind() == JsonValueKind.Number &&
            decimal.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    [GeneratedRegex(@"^1\.0(\.\d+)?$")]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"^1\.0(\.\d+)?$")]
    private static partial Regex StatementVersionRegex();
}
