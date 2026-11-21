using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;
using CrestApps.OrchardCore.Learning.Xapi.Models;

namespace CrestApps.OrchardCore.Learning.Xapi.Core;

/// <summary>
/// The keys of agents and groups: the inverse functional identifier that makes two agents the same, written as one
/// string (<c>mbox:mailto:...</c>, <c>mbox_sha1sum:...</c>, <c>openid:...</c> or <c>account:homePage|name</c>), and the
/// SHA-256 hash the indexes keep.
/// </summary>
public static class XapiAgents
{
    /// <summary>
    /// Gets the key of an agent or an identified group.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <returns>The key, or <see langword="null"/> for an anonymous group or a value that is not an agent.</returns>
    public static string Key(JsonNode agent)
    {
        if (agent is not JsonObject value)
        {
            return null;
        }

        // A mailbox is matched ignoring case, as mail systems do in practice.
        if (Text(value["mbox"]) is { } mbox)
        {
            return "mbox:" + mbox.ToLowerInvariant();
        }

        if (Text(value["mbox_sha1sum"]) is { } sha1)
        {
            return "mbox_sha1sum:" + sha1.ToLowerInvariant();
        }

        if (Text(value["openid"]) is { } openId)
        {
            return "openid:" + openId;
        }

        return value["account"] is JsonObject account && Text(account["homePage"]) is { } homePage && Text(account["name"]) is { } name
            ? AccountKey(homePage, name)
            : null;
    }

    /// <summary>
    /// Gets the key of an agent identified by an account.
    /// </summary>
    /// <param name="homePage">The home page of the account's system.</param>
    /// <param name="name">The account name.</param>
    /// <returns>The key.</returns>
    public static string AccountKey(string homePage, string name)
        => "account:" + homePage + "|" + name;

    /// <summary>
    /// Gets the account name of an agent given as JSON text, such as the <c>agent</c> parameter of a document.
    /// </summary>
    /// <param name="agentJson">The agent, as JSON text.</param>
    /// <returns>The account name, or <see langword="null"/> when the agent has no account.</returns>
    public static string AccountName(string agentJson)
    {
        if (string.IsNullOrEmpty(agentJson))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(agentJson) is JsonObject agent && agent["account"] is JsonObject account ? Text(account["name"]) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the SHA-256 hash of a key or an IRI, in lower case hex, as the indexes keep it.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The hash, or <see langword="null"/> for no value.</returns>
    public static string Hash(string value)
        => string.IsNullOrEmpty(value) ? null : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// Makes an agent identified by an account.
    /// </summary>
    /// <param name="homePage">The home page of the account's system.</param>
    /// <param name="name">The account name.</param>
    /// <param name="displayName">The agent's name, or <see langword="null"/>.</param>
    /// <returns>The agent.</returns>
    public static JsonObject Account(string homePage, string name, string displayName = null)
    {
        var agent = new JsonObject { ["objectType"] = "Agent" };

        if (!string.IsNullOrEmpty(displayName))
        {
            agent["name"] = displayName;
        }

        agent["account"] = new JsonObject
        {
            ["homePage"] = homePage,
            ["name"] = name,
        };

        return agent;
    }

    /// <summary>
    /// Gets the text of a JSON string value.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The text, or <see langword="null"/> when the value is not a string.</returns>
    public static string Text(JsonNode node)
        => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}

/// <summary>
/// Normalizes, compares and formats statements, and builds the keys statement queries filter on.
/// </summary>
public static class XapiStatementNormalizer
{
    private static readonly string[] _ignoredInComparison = ["id", "authority", "stored", "timestamp", "version"];
    private static readonly string[] _languageMapNames = ["display", "name", "description"];

    /// <summary>
    /// Normalizes a valid statement as the LRS stores it: the context activities as arrays (in the statement and a
    /// sub-statement), and the ids of the statement, the registration and statement references in lower case.
    /// </summary>
    /// <param name="statement">The statement, changed in place.</param>
    public static void Normalize(JsonObject statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        LowerCase(statement, "id");
        NormalizeBody(statement);

        if (statement["object"] is JsonObject value && XapiStatementValidator.ObjectTypeOf(value) == "SubStatement")
        {
            NormalizeBody(value);
        }
    }

    /// <summary>
    /// Gets whether two statements are the same by the statement comparison rules of xAPI 1.0.3: the properties the
    /// LRS sets (<c>authority</c>, <c>stored</c>, <c>timestamp</c>, <c>version</c>) and the id are ignored, as are the
    /// order of properties, a single context activity against an array of one, the default object types, the case of
    /// UUIDs and how a number is written.
    /// </summary>
    /// <param name="first">The first statement.</param>
    /// <param name="second">The second statement.</param>
    /// <returns><see langword="true"/> when they are the same.</returns>
    public static bool AreEquivalent(JsonObject first, JsonObject second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return JsonEquals(Comparable(first), Comparable(second));
    }

    /// <summary>
    /// Formats a stored statement for a query's <c>format</c>: <c>exact</c> as stored; <c>ids</c> with only what
    /// identifies agents, groups, activities and verbs; <c>canonical</c> with one language in each language map, the
    /// first the client accepts, else the first the statement has.
    /// </summary>
    /// <param name="statement">The statement; it is not changed.</param>
    /// <param name="format">The format.</param>
    /// <param name="languages">The languages the client accepts, best first.</param>
    /// <returns>The formatted copy.</returns>
    public static JsonObject Format(JsonObject statement, string format, IReadOnlyList<string> languages)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var copy = statement.DeepClone().AsObject();

        switch (format)
        {
            case "ids":
                ReduceToIds(copy);
                break;
            case "canonical":
                PickLanguages(copy, languages ?? []);
                break;
        }

        return copy;
    }

    /// <summary>
    /// Builds the keys of a statement: the actor and an agent object (direct) and the authority, the instructor, the
    /// team, group members of the authority and the agents of a sub-statement (related); the object activity (direct)
    /// and the context activities and those of a sub-statement (related); the verb and the registration.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The keys.</returns>
    public static XapiStatementKeys BuildKeys(JsonObject statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var keys = new XapiStatementKeys();

        AddAgent(keys, statement["actor"], direct: true);

        var value = statement["object"] as JsonObject;
        var objectType = XapiStatementValidator.ObjectTypeOf(value);

        switch (objectType)
        {
            case "Agent":
            case "Group":
                AddAgent(keys, value, direct: true);
                break;
            case "Activity":
                AddActivity(keys, value, direct: true);
                break;
            case "SubStatement":
                AddAgent(keys, value["actor"], direct: false);
                AddSubObject(keys, value["object"] as JsonObject);
                AddContext(keys, value["context"] as JsonObject);
                break;
        }

        if (statement["authority"] is JsonObject authority)
        {
            AddAgent(keys, authority, direct: false);

            if (authority["member"] is JsonArray members)
            {
                foreach (var member in members)
                {
                    AddAgent(keys, member, direct: false);
                }
            }
        }

        AddContext(keys, statement["context"] as JsonObject);

        if (XapiAgents.Text(statement["verb"]?["id"]) is { } verb)
        {
            keys.Verbs.Add(verb);
        }

        if (XapiAgents.Text(statement["context"]?["registration"]) is { } registration)
        {
            keys.Registrations.Add(registration.ToLowerInvariant());
        }

        return keys;
    }

    /// <summary>
    /// Joins the keys of a statement with those of the statement it refers to.
    /// </summary>
    /// <param name="own">The statement's own keys.</param>
    /// <param name="inherited">The keys of the statement it refers to, or <see langword="null"/>.</param>
    /// <returns>The joined keys.</returns>
    public static XapiStatementKeys Join(XapiStatementKeys own, XapiStatementKeys inherited)
    {
        var keys = new XapiStatementKeys
        {
            Agents = [.. own.Agents],
            Activities = [.. own.Activities],
            Verbs = [.. own.Verbs],
            Registrations = [.. own.Registrations],
        };

        if (inherited is null)
        {
            return keys;
        }

        foreach (var agent in inherited.Agents.Where(agent => !keys.Agents.Any(existing => existing.Value == agent.Value && existing.Direct == agent.Direct)))
        {
            keys.Agents.Add(new XapiStatementKey { Value = agent.Value, Direct = agent.Direct });
        }

        foreach (var activity in inherited.Activities.Where(activity => !keys.Activities.Any(existing => existing.Value == activity.Value && existing.Direct == activity.Direct)))
        {
            keys.Activities.Add(new XapiStatementKey { Value = activity.Value, Direct = activity.Direct });
        }

        foreach (var verb in inherited.Verbs.Where(verb => !keys.Verbs.Contains(verb)))
        {
            keys.Verbs.Add(verb);
        }

        foreach (var registration in inherited.Registrations.Where(registration => !keys.Registrations.Contains(registration)))
        {
            keys.Registrations.Add(registration);
        }

        return keys;
    }

    private static void NormalizeBody(JsonObject statement)
    {
        if (statement["object"] is JsonObject value && XapiStatementValidator.ObjectTypeOf(value) == "StatementRef")
        {
            LowerCase(value, "id");
        }

        if (statement["context"] is not JsonObject context)
        {
            return;
        }

        LowerCase(context, "registration");

        if (context["statement"] is JsonObject reference)
        {
            LowerCase(reference, "id");
        }

        if (context["contextActivities"] is JsonObject activities)
        {
            foreach (var kind in activities.Select(pair => pair.Key).ToList())
            {
                if (activities[kind] is JsonObject single)
                {
                    activities[kind] = new JsonArray(single.DeepClone());
                }
            }
        }
    }

    private static void LowerCase(JsonObject value, string property)
    {
        if (XapiAgents.Text(value[property]) is { } text)
        {
            value[property] = text.ToLowerInvariant();
        }
    }

    private static JsonObject Comparable(JsonObject statement)
    {
        var copy = statement.DeepClone().AsObject();

        foreach (var property in _ignoredInComparison)
        {
            copy.Remove(property);
        }

        Normalize(copy);
        RemoveDefaultTypes(copy);

        if (copy["object"] is JsonObject value && XapiStatementValidator.ObjectTypeOf(value) == "SubStatement")
        {
            RemoveDefaultTypes(value);
        }

        return copy;
    }

    private static void RemoveDefaultTypes(JsonObject statement)
    {
        RemoveType(statement["actor"], "Agent");
        RemoveType(statement["object"], "Activity");

        if (statement["context"] is JsonObject context)
        {
            RemoveType(context["instructor"], "Agent");

            if (context["contextActivities"] is JsonObject activities)
            {
                foreach (var (_, list) in activities)
                {
                    foreach (var activity in list?.AsArray() ?? [])
                    {
                        RemoveType(activity, "Activity");
                    }
                }
            }
        }

        if (statement["actor"]?["member"] is JsonArray members)
        {
            foreach (var member in members)
            {
                RemoveType(member, "Agent");
            }
        }
    }

    private static void RemoveType(JsonNode node, string defaultType)
    {
        if (node is JsonObject value && XapiAgents.Text(value["objectType"]) == defaultType)
        {
            value.Remove("objectType");
        }
    }

    internal static bool JsonEquals(JsonNode first, JsonNode second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }

        switch (first)
        {
            case JsonObject firstObject when second is JsonObject secondObject:
                return firstObject.Count == secondObject.Count &&
                    firstObject.All(pair => secondObject.TryGetPropertyValue(pair.Key, out var other) && JsonEquals(pair.Value, other));
            case JsonArray firstArray when second is JsonArray secondArray:
                return firstArray.Count == secondArray.Count &&
                    firstArray.Select((item, index) => JsonEquals(item, secondArray[index])).All(equal => equal);
            case JsonValue firstValue when second is JsonValue secondValue:
                var kind = firstValue.GetValueKind();

                if (kind != secondValue.GetValueKind())
                {
                    return false;
                }

                if (kind == JsonValueKind.Number &&
                    decimal.TryParse(firstValue.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var firstNumber) &&
                    decimal.TryParse(secondValue.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var secondNumber))
                {
                    return firstNumber == secondNumber;
                }

                return string.Equals(firstValue.ToJsonString(), secondValue.ToJsonString(), StringComparison.Ordinal);
            default:
                return false;
        }
    }

    private static void ReduceToIds(JsonObject statement)
    {
        ReduceAgent(statement["actor"]);
        ReduceVerb(statement["verb"]);
        ReduceObject(statement["object"]);
        ReduceAgent(statement["authority"]);
        ReduceContext(statement["context"]);

        if (statement["object"] is JsonObject value && XapiStatementValidator.ObjectTypeOf(value) == "SubStatement")
        {
            ReduceAgent(value["actor"]);
            ReduceVerb(value["verb"]);
            ReduceObject(value["object"]);
            ReduceContext(value["context"]);
        }
    }

    private static void ReduceContext(JsonNode node)
    {
        if (node is not JsonObject context)
        {
            return;
        }

        ReduceAgent(context["instructor"]);
        ReduceAgent(context["team"]);

        if (context["contextActivities"] is JsonObject activities)
        {
            foreach (var (_, list) in activities)
            {
                foreach (var activity in list is JsonArray array ? array.ToList() : [list])
                {
                    ReduceObject(activity);
                }
            }
        }
    }

    private static void ReduceObject(JsonNode node)
    {
        if (node is not JsonObject value)
        {
            return;
        }

        switch (XapiStatementValidator.ObjectTypeOf(value))
        {
            case "Activity":
                value.Remove("definition");
                break;
            case "Agent":
            case "Group":
                ReduceAgent(value);
                break;
        }
    }

    private static void ReduceVerb(JsonNode node)
    {
        if (node is JsonObject verb)
        {
            verb.Remove("display");
        }
    }

    private static void ReduceAgent(JsonNode node)
    {
        if (node is not JsonObject agent)
        {
            return;
        }

        agent.Remove("name");

        if (XapiAgents.Key(agent) is not null)
        {
            // An identified agent or group: its identifier says who it is.
            agent.Remove("member");

            return;
        }

        foreach (var member in agent["member"]?.AsArray() ?? [])
        {
            ReduceAgent(member);
        }
    }

    private static void PickLanguages(JsonNode node, IReadOnlyList<string> languages)
    {
        switch (node)
        {
            case JsonObject value:
                foreach (var (name, child) in value.ToList())
                {
                    if (Array.IndexOf(_languageMapNames, name) >= 0 && child is JsonObject map && map.Count > 1 && map.All(pair => pair.Value is JsonValue))
                    {
                        var chosen = Choose(map, languages);

                        value[name] = new JsonObject { [chosen] = map[chosen].DeepClone() };
                    }
                    else if (name != "extensions")
                    {
                        PickLanguages(child, languages);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    PickLanguages(item, languages);
                }

                break;
        }
    }

    private static string Choose(JsonObject map, IReadOnlyList<string> languages)
    {
        foreach (var language in languages)
        {
            var exact = map.FirstOrDefault(pair => string.Equals(pair.Key, language, StringComparison.OrdinalIgnoreCase));

            if (exact.Key is not null)
            {
                return exact.Key;
            }

            var primary = language.Split('-')[0];
            var close = map.FirstOrDefault(pair => string.Equals(pair.Key.Split('-')[0], primary, StringComparison.OrdinalIgnoreCase));

            if (close.Key is not null)
            {
                return close.Key;
            }
        }

        return map.First().Key;
    }

    private static void AddAgent(XapiStatementKeys keys, JsonNode agent, bool direct)
    {
        if (XapiAgents.Key(agent) is { } key && !keys.Agents.Any(existing => existing.Value == key && existing.Direct == direct))
        {
            keys.Agents.Add(new XapiStatementKey { Value = key, Direct = direct });
        }
    }

    private static void AddActivity(XapiStatementKeys keys, JsonNode activity, bool direct)
    {
        if (XapiAgents.Text(activity?["id"]) is { } id && !keys.Activities.Any(existing => existing.Value == id && existing.Direct == direct))
        {
            keys.Activities.Add(new XapiStatementKey { Value = id, Direct = direct });
        }
    }

    private static void AddSubObject(XapiStatementKeys keys, JsonObject value)
    {
        switch (XapiStatementValidator.ObjectTypeOf(value))
        {
            case "Agent":
            case "Group":
                AddAgent(keys, value, direct: false);
                break;
            case "Activity":
                AddActivity(keys, value, direct: false);
                break;
        }
    }

    private static void AddContext(XapiStatementKeys keys, JsonObject context)
    {
        if (context is null)
        {
            return;
        }

        AddAgent(keys, context["instructor"], direct: false);
        AddAgent(keys, context["team"], direct: false);

        if (context["contextActivities"] is JsonObject activities)
        {
            foreach (var (_, list) in activities)
            {
                foreach (var activity in list is JsonArray array ? array : new JsonArray(list?.DeepClone()))
                {
                    AddActivity(keys, activity, direct: false);
                }
            }
        }
    }
}
