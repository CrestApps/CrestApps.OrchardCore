using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Models;

namespace CrestApps.OrchardCore.NLWeb.Protocol;

/// <summary>
/// One server-sent event of a streamed NLWeb response.
/// </summary>
public sealed class NLWebStreamFrame
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebStreamFrame"/> class.
    /// </summary>
    /// <param name="name">The event name, such as <c>result</c>.</param>
    /// <param name="data">The JSON data of the event.</param>
    public NLWebStreamFrame(
        string name,
        JsonObject data)
    {
        Name = name;
        Data = data;
    }

    /// <summary>
    /// Gets the event name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the JSON data of the event.
    /// </summary>
    public JsonObject Data { get; }

    /// <summary>
    /// Gets the frame that ends the stream.
    /// </summary>
    public static NLWebStreamFrame End { get; } = new(NLWebProtocol.StreamEvents.End, []);

    /// <summary>
    /// Creates the frame of a pipeline event.
    /// </summary>
    /// <param name="nlwebEvent">The event.</param>
    /// <returns>The frame, or <see langword="null"/> when the event is not streamed.</returns>
    public static NLWebStreamFrame Create(NLWebEvent nlwebEvent)
    {
        ArgumentNullException.ThrowIfNull(nlwebEvent);

        switch (nlwebEvent)
        {
            case NLWebStartedEvent started:
                var startMeta = NLWebResponseItems.CreateMeta(NLWebProtocol.ResponseTypes.Answer);
                startMeta["streaming"] = true;

                if (!string.IsNullOrEmpty(started.ConversationId))
                {
                    startMeta["conversation_id"] = started.ConversationId;
                }

                if (!string.IsNullOrEmpty(started.QueryId))
                {
                    startMeta["query_id"] = started.QueryId;
                }

                return new NLWebStreamFrame(NLWebProtocol.StreamEvents.Start, new JsonObject
                {
                    ["_meta"] = startMeta,
                });

            case NLWebResultEvent result:
                return new NLWebStreamFrame(NLWebProtocol.StreamEvents.Result, new JsonObject
                {
                    ["index"] = result.Index,
                    ["item"] = NLWebResponseItems.CreateItem(result.Result),
                });

            case NLWebTextEvent text:
                return new NLWebStreamFrame(NLWebProtocol.StreamEvents.Content, NLWebResponseItems.CreateTextContent(text.Text));

            case NLWebElicitationEvent elicitation:
                return new NLWebStreamFrame(NLWebProtocol.StreamEvents.Elicitation, new JsonObject
                {
                    ["_meta"] = NLWebResponseItems.CreateMeta(NLWebProtocol.ResponseTypes.Elicitation),
                    ["elicitation"] = NLWebResponseItems.CreateElicitation(elicitation),
                });

            case NLWebFailureEvent failure:
                return new NLWebStreamFrame(NLWebProtocol.StreamEvents.Failure, new JsonObject
                {
                    ["_meta"] = NLWebResponseItems.CreateMeta(NLWebProtocol.ResponseTypes.Failure),
                    ["error"] = NLWebResponseItems.CreateError(failure),
                });

            case NLWebCompletedEvent completed:
                var meta = NLWebResponseItems.CreateMeta(NLWebProtocol.ResponseTypes.Answer);
                NLWebResponseItems.AddCompletion(meta, completed);

                return new NLWebStreamFrame(NLWebProtocol.StreamEvents.Complete, new JsonObject
                {
                    ["_meta"] = meta,
                });

            default:
                return null;
        }
    }

    /// <summary>
    /// Writes the frame in the server-sent event format: an <c>event:</c> line, a <c>data:</c> line with the JSON on
    /// one line, and a blank line.
    /// </summary>
    /// <returns>The text of the frame.</returns>
    public string Format()
    {
        var builder = new StringBuilder();
        builder.Append("event: ").Append(Name).Append('\n');
        builder.Append("data: ").Append(Data.ToJsonString(NLWebJson.SerializerOptions)).Append("\n\n");

        return builder.ToString();
    }
}
