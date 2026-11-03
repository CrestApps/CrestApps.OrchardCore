using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Models;

namespace CrestApps.OrchardCore.NLWeb.Protocol;

/// <summary>
/// Creates the JSON pieces that the JSON response and the server-sent events share.
/// </summary>
public static class NLWebResponseItems
{
    /// <summary>
    /// Creates the JSON object of a result: its schema.org object.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The JSON object.</returns>
    public static JsonObject CreateItem(NLWebResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Item is not null)
        {
            return (JsonObject)result.Item.DeepClone();
        }

        var item = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Thing",
        };

        if (!string.IsNullOrEmpty(result.Name))
        {
            item["name"] = result.Name;
        }

        if (!string.IsNullOrEmpty(result.Url))
        {
            item["url"] = result.Url;
        }

        return item;
    }

    /// <summary>
    /// Creates a natural language content item: <c>{ "type": "text", "text": "..." }</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The content item.</returns>
    public static JsonObject CreateTextContent(string text)
        => new()
        {
            ["type"] = "text",
            ["text"] = text,
        };

    /// <summary>
    /// Creates the <c>error</c> object of a failure.
    /// </summary>
    /// <param name="failure">The failure.</param>
    /// <returns>The error object.</returns>
    public static JsonObject CreateError(NLWebFailureEvent failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new JsonObject
        {
            ["code"] = failure.Code,
            ["message"] = failure.Message,
        };
    }

    /// <summary>
    /// Creates the <c>elicitation</c> object of a request for more information.
    /// </summary>
    /// <param name="elicitation">The request for more information.</param>
    /// <returns>The elicitation object.</returns>
    public static JsonObject CreateElicitation(NLWebElicitationEvent elicitation)
    {
        ArgumentNullException.ThrowIfNull(elicitation);

        var questions = new JsonArray();

        foreach (var question in elicitation.Questions)
        {
            var options = new JsonArray();

            foreach (var option in question.Options ?? [])
            {
                options.Add(option);
            }

            var node = new JsonObject
            {
                ["id"] = question.Id,
                ["text"] = question.Text,
                ["type"] = question.Type ?? "text",
            };

            if (options.Count > 0)
            {
                node["options"] = options;
            }

            questions.Add(node);
        }

        return new JsonObject
        {
            ["text"] = elicitation.Text,
            ["questions"] = questions,
        };
    }

    /// <summary>
    /// Creates the metadata of a response of a given type.
    /// </summary>
    /// <param name="responseType">The response type, such as <c>answer</c>.</param>
    /// <returns>The metadata.</returns>
    public static JsonObject CreateMeta(string responseType)
        => new()
        {
            ["response_type"] = responseType,
            ["version"] = NLWebProtocol.Version,
        };

    /// <summary>
    /// Adds what is known when the response completes to its metadata.
    /// </summary>
    /// <param name="meta">The metadata.</param>
    /// <param name="completed">The completion event.</param>
    public static void AddCompletion(JsonObject meta, NLWebCompletedEvent completed)
    {
        ArgumentNullException.ThrowIfNull(meta);
        ArgumentNullException.ThrowIfNull(completed);

        meta["total_items"] = completed.TotalItems;
        meta["processing_time_ms"] = completed.ProcessingTimeMs;

        if (completed.SessionContext is not null)
        {
            meta["session_context"] = completed.SessionContext.DeepClone();
        }
    }
}
