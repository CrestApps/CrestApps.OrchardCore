using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;
using CrestApps.OrchardCore.Learning.Xapi.Models;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Services;

/// <summary>
/// Routes a request to the LRS resource it names, after the version check and the credential check: every resource but
/// <c>about</c> needs the <c>X-Experience-API-Version</c> header with a 1.0 version and a credential.
/// </summary>
public sealed class XapiEndpointHandler
{
    /// <summary>
    /// The xAPI versions the LRS accepts and lists in the about resource.
    /// </summary>
    public static readonly string[] SupportedVersions = ["1.0.0", "1.0.1", "1.0.2", XapiConstants.Version];

    private readonly XapiStatementsResource _statements;
    private readonly XapiDocumentsResource _documents;
    private readonly XapiActivityStore _activities;

    /// <summary>
    /// Initializes a new instance of the <see cref="XapiEndpointHandler"/> class.
    /// </summary>
    public XapiEndpointHandler(
        XapiStatementsResource statements,
        XapiDocumentsResource documents,
        XapiActivityStore activities)
    {
        _statements = statements;
        _documents = documents;
        _activities = activities;
    }

    /// <summary>
    /// Handles a request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="credential">The credential, or <see langword="null"/> when the request had none that is valid.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The response.</returns>
    public async Task<XapiResponse> HandleAsync(XapiRequest request, XapiCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resource = request.Resource?.Trim('/') ?? string.Empty;

        if (resource == "about")
        {
            return request.Method is "GET" or "HEAD"
                ? XapiResponse.Json(new JsonObject
                {
                    ["version"] = new JsonArray(SupportedVersions.Select(version => (JsonNode)version).ToArray()),
                    ["extensions"] = new JsonObject(),
                }.ToJsonString())
                : XapiResponse.Error(405, "The about resource takes GET and HEAD.");
        }

        if (!XapiStatementValidator.IsSupportedVersion(request.Header(XapiConstants.Headers.Version)))
        {
            return XapiResponse.Error(400, $"Send the {XapiConstants.Headers.Version} header with a 1.0 version, such as {XapiConstants.Version}. This LRS implements xAPI {XapiConstants.Version}.");
        }

        if (resource is not ("statements" or "activities/state" or "activities/profile" or "activities" or "agents/profile" or "agents"))
        {
            return XapiResponse.Error(404, "The Learning Record Store has no such resource.");
        }

        if (credential is null)
        {
            var refused = XapiResponse.Error(401, "Authenticate with HTTP Basic (an OpenID application's client id and secret), a bearer token of an OpenID application, or a launch token.");

            refused.Headers["WWW-Authenticate"] = "Basic realm=\"Learning Record Store\", Bearer";

            return refused;
        }

        return resource switch
        {
            "statements" => await _statements.HandleAsync(request, credential, cancellationToken),
            "activities/state" => await _documents.HandleAsync(XapiDocumentKinds.State, request, credential, cancellationToken),
            "activities/profile" => await _documents.HandleAsync(XapiDocumentKinds.ActivityProfile, request, credential, cancellationToken),
            "agents/profile" => await _documents.HandleAsync(XapiDocumentKinds.AgentProfile, request, credential, cancellationToken),
            "activities" => await ActivityAsync(request, credential, cancellationToken),
            _ => Person(request, credential),
        };
    }

    private async Task<XapiResponse> ActivityAsync(XapiRequest request, XapiCredential credential, CancellationToken cancellationToken)
    {
        if (request.Method is not ("GET" or "HEAD"))
        {
            return XapiResponse.Error(405, "The activities resource takes GET and HEAD.");
        }

        if (request.Query.Keys.Any(name => name != "activityId"))
        {
            return XapiResponse.Error(400, "The activities resource takes only the activityId parameter.");
        }

        var activityId = request.Param("activityId");

        if (!XapiFormats.IsIri(activityId))
        {
            return XapiResponse.Error(400, "The activityId parameter is required and must be an IRI.");
        }

        if (!credential.CanManageDocuments && !credential.CanReadAllStatements && !credential.CanReadOwnStatements && !credential.IsLaunch)
        {
            return XapiResponse.Error(403, "The credential may not read activities.");
        }

        var record = await _activities.FindAsync(activityId, cancellationToken);
        var activity = new JsonObject
        {
            ["objectType"] = "Activity",
            ["id"] = activityId,
        };

        if (record?.Definition is { } definition && JsonNode.Parse(definition) is JsonObject value && value.Count > 0)
        {
            activity["definition"] = value;
        }

        return XapiResponse.Json(activity.ToJsonString());
    }

    private static XapiResponse Person(XapiRequest request, XapiCredential credential)
    {
        if (request.Method is not ("GET" or "HEAD"))
        {
            return XapiResponse.Error(405, "The agents resource takes GET and HEAD.");
        }

        if (request.Query.Keys.Any(name => name != "agent"))
        {
            return XapiResponse.Error(400, "The agents resource takes only the agent parameter.");
        }

        JsonObject agent = null;

        try
        {
            agent = request.Param("agent") is { } text ? JsonNode.Parse(text) as JsonObject : null;
        }
        catch (JsonException)
        {
            // Not JSON: the value is refused below.
        }

        if (agent is null || XapiStatementValidator.ValidateAgent(agent, "agent", requireIdentifier: true) is not null || XapiAgents.Text(agent["objectType"]) == "Group")
        {
            return XapiResponse.Error(400, "The agent parameter is required and must be an identified agent in JSON.");
        }

        var key = XapiAgents.Key(agent);

        if (!credential.CanReadAllStatements && !credential.CanManageDocuments && !(credential.IsLaunch && key == credential.Launch.ActorKey))
        {
            return XapiResponse.Error(403, "The credential may not read this person.");
        }

        // The LRS does not join agents into persons, so the person is the agent it was asked for.
        var person = new JsonObject { ["objectType"] = "Person" };

        foreach (var name in new[] { "name", "mbox", "mbox_sha1sum", "openid", "account" })
        {
            if (agent[name] is { } value)
            {
                person[name] = new JsonArray(value.DeepClone());
            }
        }

        return XapiResponse.Json(person.ToJsonString());
    }
}
