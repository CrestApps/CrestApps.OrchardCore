using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Services;

/// <summary>
/// The document resources of xAPI 1.0.3: the state resource (<c>activities/state</c>), the activity profile resource
/// (<c>activities/profile</c>) and the agent profile resource (<c>agents/profile</c>). A document is stored by
/// <c>PUT</c>, joined by <c>POST</c> (two JSON objects are merged at their top level; anything else is refused when a
/// document exists), read one at a time or as a list of ids (with <c>since</c>), and deleted one at a time or, for
/// states, all at once. Every document has an ETag; <c>If-Match</c> and <c>If-None-Match</c> are honored on every
/// write (412 when they do not hold), and a <c>PUT</c> to an existing profile without either header is a conflict
/// (409), as the concurrency rules require.
/// </summary>
public sealed class XapiDocumentsResource
{
    private readonly XapiDocumentStore _store;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly XapiOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="XapiDocumentsResource"/> class.
    /// </summary>
    public XapiDocumentsResource(
        XapiDocumentStore store,
        ISession session,
        IClock clock,
        IOptions<XapiOptions> options)
    {
        _store = store;
        _session = session;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>
    /// Handles a request to a document resource.
    /// </summary>
    /// <param name="kind">The kind of document, one of <see cref="XapiDocumentKinds"/>.</param>
    /// <param name="request">The request.</param>
    /// <param name="credential">The credential.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The response.</returns>
    public async Task<XapiResponse> HandleAsync(string kind, XapiRequest request, XapiCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);

        var (scope, documentId, error) = ReadAddress(kind, request);

        if (error is not null)
        {
            return error;
        }

        var write = request.Method is "PUT" or "POST" or "DELETE";
        var refusal = Authorize(kind, scope, documentId, write, credential);

        if (refusal is not null)
        {
            return refusal;
        }

        return request.Method switch
        {
            "GET" or "HEAD" => await GetAsync(scope, documentId, request, cancellationToken),
            "PUT" => await PutAsync(kind, scope, documentId, request, merge: false, cancellationToken),
            "POST" => await PutAsync(kind, scope, documentId, request, merge: true, cancellationToken),
            "DELETE" => await DeleteAsync(kind, scope, documentId, request, cancellationToken),
            _ => XapiResponse.Error(405, "A document resource takes GET, HEAD, PUT, POST and DELETE."),
        };
    }

    /// <summary>
    /// Computes the ETag of a content: its SHA-256 hash in hex, quoted.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>The ETag.</returns>
    public static string ETagOf(byte[] content)
        => "\"" + Convert.ToHexStringLower(SHA256.HashData(content ?? [])) + "\"";

    private static (XapiDocumentScope Scope, string DocumentId, XapiResponse Error) ReadAddress(string kind, XapiRequest request)
    {
        var idName = kind == XapiDocumentKinds.State ? "stateId" : "profileId";
        string[] allowed = kind switch
        {
            XapiDocumentKinds.State => ["activityId", "agent", "registration", "stateId", "since"],
            XapiDocumentKinds.ActivityProfile => ["activityId", "profileId", "since"],
            _ => ["agent", "profileId", "since"],
        };

        var unknown = request.Query.Keys.FirstOrDefault(name => Array.IndexOf(allowed, name) < 0);

        if (unknown is not null)
        {
            return (null, null, XapiResponse.Error(400, $"This document resource has no parameter {unknown}."));
        }

        string activityId = null;
        string agentKey = null;
        string registration = null;

        if (kind != XapiDocumentKinds.AgentProfile)
        {
            activityId = request.Param("activityId");

            if (!XapiFormats.IsIri(activityId))
            {
                return (null, null, XapiResponse.Error(400, "The activityId parameter is required and must be an IRI."));
            }
        }

        if (kind != XapiDocumentKinds.ActivityProfile)
        {
            var agentText = request.Param("agent");
            JsonNode agent = null;

            try
            {
                agent = string.IsNullOrEmpty(agentText) ? null : JsonNode.Parse(agentText);
            }
            catch (JsonException)
            {
                // Not JSON: the value is refused below.
            }

            if (agent is null || XapiStatementValidator.ValidateAgent(agent, "agent", requireIdentifier: true) is not null)
            {
                return (null, null, XapiResponse.Error(400, "The agent parameter is required and must be an identified agent in JSON."));
            }

            agentKey = XapiAgents.Key(agent);
        }

        if (kind == XapiDocumentKinds.State && request.Param("registration") is { } registrationText)
        {
            if (!XapiFormats.IsUuid(registrationText))
            {
                return (null, null, XapiResponse.Error(400, "The registration parameter must be a UUID."));
            }

            registration = registrationText.ToLowerInvariant();
        }

        var documentId = request.Param(idName);

        if (documentId is not null && documentId.Length == 0)
        {
            return (null, null, XapiResponse.Error(400, $"The {idName} parameter cannot be empty."));
        }

        if (documentId is null && (request.Method is "PUT" or "POST" || (request.Method == "DELETE" && kind != XapiDocumentKinds.State)))
        {
            return (null, null, XapiResponse.Error(400, $"The {idName} parameter is required."));
        }

        if (request.Param("since") is not null && (documentId is not null || request.Method is not ("GET" or "HEAD")))
        {
            return (null, null, XapiResponse.Error(400, $"The since parameter is only for listing the ids of documents, without {idName}."));
        }

        return (new XapiDocumentScope(kind, activityId, agentKey, registration), documentId, null);
    }

    private static XapiResponse Authorize(string kind, XapiDocumentScope scope, string documentId, bool write, XapiCredential credential)
    {
        if (credential.CanManageDocuments)
        {
            return null;
        }

        if (credential.Launch is not { } launch)
        {
            return XapiResponse.Error(403, "The credential may not use the documents of the Learning Record Store.");
        }

        // A launch token opens the documents of its own actor and activity, and the states of its registration.
        var allowed = kind switch
        {
            XapiDocumentKinds.State => scope.ActivityId == launch.ActivityId && scope.AgentKey == launch.ActorKey && string.Equals(scope.Registration, launch.Registration, StringComparison.OrdinalIgnoreCase),
            XapiDocumentKinds.ActivityProfile => scope.ActivityId == launch.ActivityId && !write,
            _ => scope.AgentKey == launch.ActorKey,
        };

        if (!allowed)
        {
            return XapiResponse.Error(403, "The launch does not open this document.");
        }

        if (write && kind == XapiDocumentKinds.State && (documentId is null || launch.ReadOnlyStateIds.Contains(documentId)))
        {
            return XapiResponse.Error(403, "The launch may read this state but not change it.");
        }

        return null;
    }

    private async Task<XapiResponse> GetAsync(XapiDocumentScope scope, string documentId, XapiRequest request, CancellationToken cancellationToken)
    {
        if (documentId is null)
        {
            DateTime? since = null;

            if (request.Param("since") is { } sinceText)
            {
                if (!XapiFormats.TryParseTimestamp(sinceText, out var parsed))
                {
                    return XapiResponse.Error(400, "The since parameter must be an ISO 8601 timestamp.");
                }

                since = parsed;
            }

            var documents = await _store.ListAsync(scope, since, cancellationToken);

            return XapiResponse.Json(documents.Select(document => document.DocumentId).ToArray());
        }

        var found = await _store.FindAsync(scope, documentId, cancellationToken);

        if (found is null)
        {
            return XapiResponse.Error(404, "There is no such document.");
        }

        var response = new XapiResponse
        {
            Status = 200,
            ContentType = found.ContentType,
            Body = found.Content ?? [],
        };

        response.Headers["ETag"] = found.ETag;
        response.Headers["Last-Modified"] = DateTime.SpecifyKind(found.UpdatedUtc, DateTimeKind.Utc).ToString("R", CultureInfo.InvariantCulture);

        return response;
    }

    private async Task<XapiResponse> PutAsync(string kind, XapiDocumentScope scope, string documentId, XapiRequest request, bool merge, CancellationToken cancellationToken)
    {
        if (request.Body.Length > _options.MaxDocumentBytes)
        {
            return XapiResponse.Error(413, $"A document may have at most {_options.MaxDocumentBytes} bytes.");
        }

        var existing = await _store.FindAsync(scope, documentId, cancellationToken);
        var precondition = CheckPreconditions(existing, request);

        if (precondition is not null)
        {
            return precondition;
        }

        // The concurrency rules: a PUT that would replace a profile must say which version it replaces.
        if (!merge && existing is not null && kind != XapiDocumentKinds.State &&
            string.IsNullOrEmpty(request.Header("If-Match")) && string.IsNullOrEmpty(request.Header("If-None-Match")))
        {
            return XapiResponse.Error(409, "The document exists: send If-Match with its ETag to replace it, or If-None-Match: * to create it only when it does not exist.");
        }

        var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType;
        var content = request.Body;

        if (merge && existing is not null)
        {
            if (!TryReadObject(existing.ContentType, existing.Content, out var stored) || !TryReadObject(contentType, content, out var posted))
            {
                return XapiResponse.Error(400, "A POST to an existing document merges JSON objects only: both the document and the request must be application/json objects.");
            }

            foreach (var (name, value) in posted)
            {
                stored[name] = value?.DeepClone();
            }

            content = JsonSerializer.SerializeToUtf8Bytes(stored);
            contentType = "application/json";
        }
        else if (merge && !TryReadObject(contentType, content, out _) && IsJson(contentType))
        {
            return XapiResponse.Error(400, "A POST with application/json must send a JSON object.");
        }

        var now = XapiFormats.ToMilliseconds(_clock.UtcNow);
        var document = existing ?? new XapiDocument
        {
            Kind = kind,
            ActivityId = scope.ActivityId,
            AgentKey = scope.AgentKey,
            Agent = request.Param("agent"),
            Registration = scope.Registration,
            DocumentId = documentId,
        };

        document.ContentType = contentType;
        document.Content = content;
        document.ETag = ETagOf(content);
        document.UpdatedUtc = now;

        if (existing is null)
        {
            await _store.CreateAsync(document, cancellationToken);
        }
        else
        {
            await _store.UpdateAsync(document, cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);

        var response = XapiResponse.Empty(204);

        response.Headers["ETag"] = document.ETag;

        return response;
    }

    private async Task<XapiResponse> DeleteAsync(string kind, XapiDocumentScope scope, string documentId, XapiRequest request, CancellationToken cancellationToken)
    {
        if (documentId is null)
        {
            // Every state of the activity, agent and registration.
            foreach (var document in await _store.ListAsync(scope, sinceUtc: null, cancellationToken))
            {
                await _store.DeleteAsync(document, cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);

            return XapiResponse.Empty(204);
        }

        var existing = await _store.FindAsync(scope, documentId, cancellationToken);
        var precondition = CheckPreconditions(existing, request);

        if (precondition is not null)
        {
            return precondition;
        }

        if (existing is not null)
        {
            await _store.DeleteAsync(existing, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        return XapiResponse.Empty(204);
    }

    private static XapiResponse CheckPreconditions(XapiDocument existing, XapiRequest request)
    {
        var ifMatch = request.Header("If-Match");
        var ifNoneMatch = request.Header("If-None-Match");

        if (!string.IsNullOrEmpty(ifMatch) && (existing is null || !MatchesAny(ifMatch, existing.ETag)))
        {
            return XapiResponse.Error(412, "The document does not have the ETag of If-Match.");
        }

        if (!string.IsNullOrEmpty(ifNoneMatch) && existing is not null && MatchesAny(ifNoneMatch, existing.ETag))
        {
            return XapiResponse.Error(412, "The document exists, and If-None-Match asked that it did not.");
        }

        return null;
    }

    private static bool MatchesAny(string header, string etag)
        => header.Split(',').Select(value => value.Trim()).Any(value =>
            value == "*" ||
            string.Equals(Unquote(value), Unquote(etag), StringComparison.OrdinalIgnoreCase));

    private static string Unquote(string value)
    {
        var text = value?.Trim() ?? string.Empty;

        if (text.StartsWith("W/", StringComparison.Ordinal))
        {
            text = text[2..];
        }

        return text.Trim('"');
    }

    private static bool IsJson(string contentType)
        => contentType.Split(';')[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadObject(string contentType, byte[] content, out JsonObject value)
    {
        value = null;

        if (!IsJson(contentType ?? string.Empty))
        {
            return false;
        }

        try
        {
            value = JsonNode.Parse(content) as JsonObject;
        }
        catch (JsonException)
        {
            // Not JSON: the value is refused below.
        }

        return value is not null;
    }
}
