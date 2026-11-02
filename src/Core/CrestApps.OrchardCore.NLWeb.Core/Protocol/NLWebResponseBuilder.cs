using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Models;

namespace CrestApps.OrchardCore.NLWeb.Protocol;

/// <summary>
/// Builds the JSON response of an NLWeb request from the events of the ask pipeline, in the shape of protocol version
/// 0.5 and later: <c>{ "_meta": { ... }, "results": [ ... ], "content": [ ... ] }</c>.
/// </summary>
public sealed class NLWebResponseBuilder
{
    private readonly JsonObject _meta = new()
    {
        ["response_type"] = NLWebProtocol.ResponseTypes.Answer,
        ["version"] = NLWebProtocol.Version,
    };

    private readonly JsonArray _results = [];
    private readonly JsonArray _content = [];

    private NLWebElicitationEvent _elicitation;
    private NLWebFailureEvent _failure;

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
                SetIfNotEmpty(_meta, "conversation_id", started.ConversationId);
                SetIfNotEmpty(_meta, "query_id", started.QueryId);
                break;

            case NLWebResultEvent result:
                _results.Add(NLWebResponseItems.CreateItem(result.Result));
                break;

            case NLWebTextEvent text:
                _content.Add(NLWebResponseItems.CreateTextContent(text.Text));

                if (text.Mode == NLWebProtocol.Modes.Summarize)
                {
                    _meta["summary"] = text.Text;
                }

                break;

            case NLWebElicitationEvent elicitation:
                _elicitation = elicitation;
                _meta["response_type"] = NLWebProtocol.ResponseTypes.Elicitation;
                break;

            case NLWebFailureEvent failure:
                _failure = failure;
                _meta["response_type"] = NLWebProtocol.ResponseTypes.Failure;
                break;

            case NLWebCompletedEvent completed:
                NLWebResponseItems.AddCompletion(_meta, completed);
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
            ["_meta"] = _meta.DeepClone(),
        };

        if (_failure is not null)
        {
            response["error"] = NLWebResponseItems.CreateError(_failure);

            return response;
        }

        if (_elicitation is not null)
        {
            response["elicitation"] = NLWebResponseItems.CreateElicitation(_elicitation);

            return response;
        }

        response["results"] = _results.DeepClone();

        if (_content.Count > 0)
        {
            response["content"] = _content.DeepClone();
        }

        return response;
    }

    private static void SetIfNotEmpty(JsonObject node, string name, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            node[name] = value;
        }
    }
}
