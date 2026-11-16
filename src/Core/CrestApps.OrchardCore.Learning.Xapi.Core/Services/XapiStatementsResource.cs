using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Services;

/// <summary>
/// The statements resource of xAPI 1.0.3: <c>PUT</c> with a <c>statementId</c>, <c>POST</c> with one statement or an
/// array, and <c>GET</c> by <c>statementId</c>, by <c>voidedStatementId</c> or by query (agent, verb, activity,
/// registration, related_activities, related_agents, since, until, limit, format, attachments, ascending), with the
/// <c>more</c> link and <c>X-Experience-API-Consistent-Through</c>. A request with a parameter the resource does not
/// define is refused.
/// </summary>
public sealed class XapiStatementsResource
{
    private static readonly HashSet<string> _queryParameters = new(StringComparer.Ordinal)
    {
        "statementId", "voidedStatementId", "agent", "verb", "activity", "registration", "related_activities", "related_agents",
        "since", "until", "limit", "format", "attachments", "ascending", "more",
    };

    private static readonly string[] _formats = ["ids", "exact", "canonical"];

    private readonly XapiStatementService _statements;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="XapiStatementsResource"/> class.
    /// </summary>
    /// <param name="statements">The statement service.</param>
    /// <param name="clock">The clock.</param>
    public XapiStatementsResource(XapiStatementService statements, IClock clock)
    {
        _statements = statements;
        _clock = clock;
    }

    /// <summary>
    /// Handles a request to the statements resource.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="credential">The credential of the request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The response.</returns>
    public async Task<XapiResponse> HandleAsync(XapiRequest request, XapiCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);

        return request.Method switch
        {
            "PUT" => await PutAsync(request, credential, cancellationToken),
            "POST" => await PostAsync(request, credential, cancellationToken),
            "GET" or "HEAD" => await GetAsync(request, credential, cancellationToken),
            _ => XapiResponse.Error(405, "The statements resource takes GET, HEAD, PUT and POST."),
        };
    }

    private async Task<XapiResponse> PutAsync(XapiRequest request, XapiCredential credential, CancellationToken cancellationToken)
    {
        if (request.Query.Keys.Any(name => name != "statementId"))
        {
            return XapiResponse.Error(400, "A PUT to the statements resource takes only the statementId parameter.");
        }

        var statementId = request.Param("statementId");

        if (!XapiFormats.IsUuid(statementId))
        {
            return XapiResponse.Error(400, "A PUT to the statements resource needs a statementId that is a UUID.");
        }

        var (nodes, attachments, error) = ReadBody(request);

        if (error is not null)
        {
            return error;
        }

        if (nodes is not JsonObject statement)
        {
            return XapiResponse.Error(400, "A PUT to the statements resource takes one statement.");
        }

        if (statement["id"] is { } bodyId && !string.Equals(XapiAgents.Text(bodyId), statementId, StringComparison.OrdinalIgnoreCase))
        {
            return XapiResponse.Error(400, "The statement id in the body is not the statementId parameter.");
        }

        statement["id"] = statementId.ToLowerInvariant();

        var result = await _statements.WriteAsync([statement], attachments, credential, cancellationToken);

        return result.Succeeded ? XapiResponse.Empty(204) : XapiResponse.Error(result.Status, result.Message);
    }

    private async Task<XapiResponse> PostAsync(XapiRequest request, XapiCredential credential, CancellationToken cancellationToken)
    {
        if (request.Query.Count > 0)
        {
            return XapiResponse.Error(400, "A POST to the statements resource takes no parameters.");
        }

        var (nodes, attachments, error) = ReadBody(request);

        if (error is not null)
        {
            return error;
        }

        List<JsonObject> statements;

        switch (nodes)
        {
            case JsonObject single:
                statements = [single];
                break;
            case JsonArray array when array.All(item => item is JsonObject):
                statements = array.Select(item => item.AsObject()).ToList();
                break;
            default:
                return XapiResponse.Error(400, "A POST to the statements resource takes a statement or an array of statements.");
        }

        var result = await _statements.WriteAsync(statements, attachments, credential, cancellationToken);

        return result.Succeeded ? XapiResponse.Json(result.Ids) : XapiResponse.Error(result.Status, result.Message);
    }

    private async Task<XapiResponse> GetAsync(XapiRequest request, XapiCredential credential, CancellationToken cancellationToken)
    {
        if (!XapiStatementService.CanRead(credential))
        {
            return XapiResponse.Error(403, "The credential may not read statements.");
        }

        var unknown = request.Query.Keys.FirstOrDefault(name => !_queryParameters.Contains(name));

        if (unknown is not null)
        {
            return XapiResponse.Error(400, $"The statements resource has no parameter {unknown}.");
        }

        var parameters = new Dictionary<string, string>(request.Query, StringComparer.Ordinal);
        long? sequenceBound = null;
        var skip = 0;

        if (parameters.TryGetValue("more", out var more))
        {
            if (parameters.Count > 1 || !TryReadMore(more, out parameters, out skip, out sequenceBound))
            {
                return XapiResponse.Error(400, "The more parameter is not a link this LRS made.");
            }
        }

        var format = parameters.GetValueOrDefault("format") ?? "exact";

        if (Array.IndexOf(_formats, format) < 0)
        {
            return XapiResponse.Error(400, "The format parameter must be ids, exact or canonical.");
        }

        if (!TryReadBoolean(parameters, "attachments", out var withAttachments) ||
            !TryReadBoolean(parameters, "ascending", out var ascending) ||
            !TryReadBoolean(parameters, "related_agents", out var relatedAgents) ||
            !TryReadBoolean(parameters, "related_activities", out var relatedActivities))
        {
            return XapiResponse.Error(400, "The attachments, ascending, related_agents and related_activities parameters must be true or false.");
        }

        var languages = AcceptedLanguages(request.Header("Accept-Language"));
        var now = XapiFormats.ToMilliseconds(_clock.UtcNow);
        var consistentThrough = XapiFormats.FormatTimestamp(now);
        var statementId = parameters.GetValueOrDefault("statementId");
        var voidedStatementId = parameters.GetValueOrDefault("voidedStatementId");

        if (statementId is not null || voidedStatementId is not null)
        {
            if (parameters.Keys.Any(name => name is not ("statementId" or "voidedStatementId" or "format" or "attachments")) || (statementId is not null && voidedStatementId is not null))
            {
                return XapiResponse.Error(400, "statementId and voidedStatementId take only the format and attachments parameters.");
            }

            var id = statementId ?? voidedStatementId;

            if (!XapiFormats.IsUuid(id))
            {
                return XapiResponse.Error(400, "The statement id must be a UUID.");
            }

            var record = await _statements.FindAsync(id, voidedStatementId is not null, credential, cancellationToken);

            if (record is null)
            {
                return XapiResponse.Error(404, "There is no such statement.");
            }

            var single = Render(record, format, languages);
            var response = await RespondAsync(single, [record], withAttachments, cancellationToken);

            response.Headers[XapiConstants.Headers.ConsistentThrough] = consistentThrough;
            response.Headers["Last-Modified"] = DateTime.SpecifyKind(record.StoredUtc, DateTimeKind.Utc).ToString("R", CultureInfo.InvariantCulture);

            return response;
        }

        var query = new XapiStatementQuery
        {
            RelatedAgents = relatedAgents,
            RelatedActivities = relatedActivities,
            Ascending = ascending,
            Skip = skip,
            MaxSequence = sequenceBound,
        };

        var error = ReadFilters(parameters, query);

        if (error is not null)
        {
            return XapiResponse.Error(400, error);
        }

        // The first page fixes the end of the result, so a statement stored while a client reads the pages does not
        // move the pages it has not read yet.
        query.MaxSequence ??= await _statements.CurrentSequenceAsync(cancellationToken);

        var requestedLimit = query.Limit;
        var page = await _statements.QueryAsync(query, credential, cancellationToken);

        if (page is null)
        {
            return XapiResponse.Error(403, "The credential may not read statements.");
        }

        var nextLink = string.Empty;

        if (page.HasMore)
        {
            parameters.Remove("more");
            parameters["limit"] = requestedLimit.ToString(CultureInfo.InvariantCulture);
            nextLink = request.EndpointPath.TrimEnd('/') + "/statements?more=" + Uri.EscapeDataString(WriteMore(parameters, skip + page.Records.Count, query.MaxSequence ?? 0));
        }

        var result = new JsonObject
        {
            ["statements"] = new JsonArray(page.Records.Select(record => (JsonNode)Render(record, format, languages)).ToArray()),
            ["more"] = nextLink,
        };

        var queried = await RespondAsync(result, page.Records, withAttachments, cancellationToken);

        queried.Headers[XapiConstants.Headers.ConsistentThrough] = consistentThrough;

        return queried;
    }

    private static string ReadFilters(Dictionary<string, string> parameters, XapiStatementQuery query)
    {
        if (parameters.TryGetValue("agent", out var agentText))
        {
            JsonNode agent;

            try
            {
                agent = JsonNode.Parse(agentText);
            }
            catch (JsonException)
            {
                return "The agent parameter must be an agent in JSON.";
            }

            var error = XapiStatementValidator.ValidateAgent(agent, "agent", requireIdentifier: true);

            if (error is not null)
            {
                return error;
            }

            query.AgentKey = XapiAgents.Key(agent);
        }

        if (parameters.TryGetValue("verb", out var verb))
        {
            if (!XapiFormats.IsIri(verb))
            {
                return "The verb parameter must be an IRI.";
            }

            query.Verb = verb;
        }

        if (parameters.TryGetValue("activity", out var activity))
        {
            if (!XapiFormats.IsIri(activity))
            {
                return "The activity parameter must be an IRI.";
            }

            query.Activity = activity;
        }

        if (parameters.TryGetValue("registration", out var registration))
        {
            if (!XapiFormats.IsUuid(registration))
            {
                return "The registration parameter must be a UUID.";
            }

            query.Registration = registration.ToLowerInvariant();
        }

        if (parameters.TryGetValue("since", out var sinceText))
        {
            if (!XapiFormats.TryParseTimestamp(sinceText, out var since))
            {
                return "The since parameter must be an ISO 8601 timestamp.";
            }

            query.SinceUtc = since;
        }

        if (parameters.TryGetValue("until", out var untilText))
        {
            if (!XapiFormats.TryParseTimestamp(untilText, out var until))
            {
                return "The until parameter must be an ISO 8601 timestamp.";
            }

            query.UntilUtc = query.UntilUtc is { } bound && bound < until ? bound : until;
        }

        if (parameters.TryGetValue("limit", out var limitText))
        {
            if (!int.TryParse(limitText, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) || limit < 0)
            {
                return "The limit parameter must be a whole number, 0 or more.";
            }

            query.Limit = limit;
        }

        return null;
    }

    private async Task<XapiResponse> RespondAsync(JsonObject body, IReadOnlyList<XapiStatementRecord> records, bool withAttachments, CancellationToken cancellationToken)
    {
        if (!withAttachments)
        {
            return XapiResponse.Json(body.ToJsonString());
        }

        var parts = new List<XapiPart>
        {
            new()
            {
                Headers = { ["Content-Type"] = "application/json" },
                Content = Encoding.UTF8.GetBytes(body.ToJsonString()),
            },
        };

        var sent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in records.SelectMany(record => record.Attachments ?? []))
        {
            if (!sent.Add(file.Sha2))
            {
                continue;
            }

            await using var stream = await _statements.OpenAttachmentAsync(file, cancellationToken);

            if (stream is null)
            {
                continue;
            }

            using var content = new MemoryStream();
            await stream.CopyToAsync(content, cancellationToken);

            parts.Add(new XapiPart
            {
                Headers =
                {
                    ["Content-Type"] = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    ["Content-Transfer-Encoding"] = "binary",
                    [XapiConstants.Headers.Hash] = file.Sha2,
                },
                Content = content.ToArray(),
            });
        }

        var (bytes, contentType) = XapiMultipart.Write(parts);

        return new XapiResponse
        {
            Status = 200,
            ContentType = contentType,
            Body = bytes,
        };
    }

    private static JsonObject Render(XapiStatementRecord record, string format, IReadOnlyList<string> languages)
    {
        var statement = JsonNode.Parse(record.Statement).AsObject();

        return format == "exact" ? statement : XapiStatementNormalizer.Format(statement, format, languages);
    }

    private static (JsonNode Statements, IReadOnlyList<XapiAttachmentContent> Attachments, XapiResponse Error) ReadBody(XapiRequest request)
    {
        var mediaType = request.MediaType;
        byte[] json;
        var attachments = new List<XapiAttachmentContent>();

        if (mediaType == "application/json")
        {
            json = request.Body;
        }
        else if (mediaType == "multipart/mixed")
        {
            var parts = XapiMultipart.Parse(request.Body, request.ContentType);

            if (parts is null || parts.Count == 0)
            {
                return (null, null, XapiResponse.Error(400, "The multipart/mixed body could not be read."));
            }

            if (!parts[0].Header("Content-Type").StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return (null, null, XapiResponse.Error(400, "The first part of a multipart/mixed request must be the statements, as application/json."));
            }

            json = parts[0].Content;

            foreach (var part in parts.Skip(1))
            {
                if (!string.Equals(part.Header("Content-Transfer-Encoding"), "binary", StringComparison.OrdinalIgnoreCase))
                {
                    return (null, null, XapiResponse.Error(400, "Every attachment part must have Content-Transfer-Encoding: binary."));
                }

                attachments.Add(new XapiAttachmentContent
                {
                    Sha2 = part.Header(XapiConstants.Headers.Hash).ToLowerInvariant(),
                    ContentType = part.Header("Content-Type"),
                    Content = part.Content,
                });
            }
        }
        else
        {
            return (null, null, XapiResponse.Error(400, "Statements must be sent as application/json or multipart/mixed."));
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });

            return (JsonNode.Parse(document.RootElement.GetRawText()), attachments, null);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return (null, null, XapiResponse.Error(400, "The body is not valid JSON."));
        }
    }

    private static bool TryReadBoolean(Dictionary<string, string> parameters, string name, out bool value)
    {
        value = false;

        if (!parameters.TryGetValue(name, out var text))
        {
            return true;
        }

        switch (text)
        {
            case "true":
                value = true;
                return true;
            case "false":
                return true;
            default:
                return false;
        }
    }

    private static string WriteMore(Dictionary<string, string> parameters, int skip, long sequence)
    {
        var token = new JsonObject
        {
            ["p"] = JsonSerializer.SerializeToNode(parameters),
            ["s"] = skip,
            ["q"] = sequence,
        };

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(token.ToJsonString())).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool TryReadMore(string more, out Dictionary<string, string> parameters, out int skip, out long? sequence)
    {
        parameters = null;
        skip = 0;
        sequence = null;

        try
        {
            var text = more.Replace('-', '+').Replace('_', '/');

            text += new string('=', (4 - (text.Length % 4)) % 4);

            var token = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(text)))?.AsObject();

            if (token?["p"] is not JsonObject values || token["s"] is null || token["q"] is null)
            {
                return false;
            }

            parameters = values.ToDictionary(pair => pair.Key, pair => pair.Value?.GetValue<string>(), StringComparer.Ordinal);
            skip = token["s"].GetValue<int>();
            sequence = token["q"].GetValue<long>();

            return skip >= 0 && !parameters.ContainsKey("more") && parameters.Keys.All(_queryParameters.Contains);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    private static List<string> AcceptedLanguages(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return [];
        }

        return header.Split(',')
            .Select((entry, index) =>
            {
                var pieces = entry.Split(';');
                var quality = 1.0;

                foreach (var piece in pieces.Skip(1))
                {
                    var trimmed = piece.Trim();

                    if (trimmed.StartsWith("q=", StringComparison.OrdinalIgnoreCase) &&
                        double.TryParse(trimmed[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        quality = parsed;
                    }
                }

                return (Language: pieces[0].Trim(), Quality: quality, Index: index);
            })
            .Where(entry => entry.Language.Length > 0 && entry.Language != "*" && entry.Quality > 0)
            .OrderByDescending(entry => entry.Quality)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Language)
            .ToList();
    }
}
