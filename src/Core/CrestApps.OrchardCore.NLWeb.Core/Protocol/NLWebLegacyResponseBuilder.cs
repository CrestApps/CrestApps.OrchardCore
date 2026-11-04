using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Models;

namespace CrestApps.OrchardCore.NLWeb.Protocol;

/// <summary>
/// Builds the JSON response of the original NLWeb protocol (version 0.1):
/// <c>{ "query_id": "...", "results": [ { "url", "name", "site", "score", "description", "schema_object" } ] }</c>.
/// </summary>
public sealed class NLWebLegacyResponseBuilder
{
    private readonly JsonArray _results = [];
    private readonly string _site;

    private string _queryId;
    private string _summary;
    private string _answer;
    private NLWebFailureEvent _failure;

    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebLegacyResponseBuilder"/> class.
    /// </summary>
    /// <param name="site">The site token the results come from, such as the host name.</param>
    public NLWebLegacyResponseBuilder(string site)
    {
        _site = site;
    }

    /// <summary>
    /// Adds an event to the response.
    /// </summary>
    /// <param name="nlwebEvent">The event.</param>
    public void Add(NLWebEvent nlwebEvent)
    {
        ArgumentNullException.ThrowIfNull(nlwebEvent);

        switch (nlwebEvent)
        {
            case NLWebStartedEvent started:
                _queryId = started.QueryId;
                break;

            case NLWebResultEvent result:
                _results.Add(CreateResult(result.Result));
                break;

            case NLWebTextEvent text when text.Mode == NLWebProtocol.Modes.Summarize:
                _summary = text.Text;
                break;

            case NLWebTextEvent text:
                _answer = text.Text;
                break;

            case NLWebElicitationEvent elicitation:
                _answer = elicitation.Text;
                break;

            case NLWebFailureEvent failure:
                _failure = failure;
                break;
        }
    }

    /// <summary>
    /// Builds the response.
    /// </summary>
    /// <returns>The JSON response.</returns>
    public JsonObject Build()
    {
        var response = new JsonObject
        {
            ["query_id"] = _queryId,
        };

        if (_failure is not null)
        {
            response["error"] = _failure.Message;
            response["error_code"] = _failure.Code;
            response["results"] = new JsonArray();

            return response;
        }

        response["results"] = _results.DeepClone();

        if (_summary is not null)
        {
            response["summary"] = _summary;
        }

        if (_answer is not null)
        {
            response["answer"] = _answer;
        }

        return response;
    }

    private JsonObject CreateResult(NLWebResult result)
    {
        var item = NLWebResponseItems.CreateItem(result);

        var description = result.Relevance;

        if (string.IsNullOrEmpty(description) && item["description"] is JsonValue value && value.TryGetValue<string>(out var text))
        {
            description = text;
        }

        return new JsonObject
        {
            ["url"] = result.Url,
            ["name"] = result.Name,
            ["site"] = _site,
            ["score"] = result.Score,
            ["description"] = description,
            ["schema_object"] = item.ToJsonString(NLWebJson.SerializerOptions),
        };
    }
}
