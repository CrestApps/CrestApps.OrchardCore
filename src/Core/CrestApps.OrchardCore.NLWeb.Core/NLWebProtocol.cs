namespace CrestApps.OrchardCore.NLWeb;

/// <summary>
/// The names and values the NLWeb protocol defines.
/// </summary>
public static class NLWebProtocol
{
    /// <summary>
    /// The protocol version the responses declare.
    /// </summary>
    public const string Version = "0.54";

    /// <summary>
    /// The media type of a server-sent event stream.
    /// </summary>
    public const string EventStreamMediaType = "text/event-stream";

    /// <summary>
    /// The response modes: how much AI processing the results get.
    /// </summary>
    public static class Modes
    {
        /// <summary>
        /// The results only.
        /// </summary>
        public const string List = "list";

        /// <summary>
        /// The results and a summary of them.
        /// </summary>
        public const string Summarize = "summarize";

        /// <summary>
        /// The results and an answer to the question written from them.
        /// </summary>
        public const string Generate = "generate";
    }

    /// <summary>
    /// The response types of the <c>_meta.response_type</c> field.
    /// </summary>
    public static class ResponseTypes
    {
        /// <summary>
        /// The response answers the query.
        /// </summary>
        public const string Answer = "answer";

        /// <summary>
        /// The agent needs more information before it can answer.
        /// </summary>
        public const string Elicitation = "elicitation";

        /// <summary>
        /// The answer is not ready yet and can be awaited.
        /// </summary>
        public const string Promise = "promise";

        /// <summary>
        /// The query failed.
        /// </summary>
        public const string Failure = "failure";
    }

    /// <summary>
    /// The response formats of the <c>prefer.response_format</c> field.
    /// </summary>
    public static class ResponseFormats
    {
        /// <summary>
        /// The conversational search format: the schema.org objects in a <c>results</c> array.
        /// </summary>
        public const string ConversationalSearch = "conversational_search";
    }

    /// <summary>
    /// The error codes of a failure response.
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// The request is not a valid NLWeb request.
        /// </summary>
        public const string InvalidRequest = "INVALID_REQUEST";

        /// <summary>
        /// The caller may not query this site.
        /// </summary>
        public const string Forbidden = "FORBIDDEN";

        /// <summary>
        /// The site has no way to find content for the query.
        /// </summary>
        public const string NotConfigured = "NOT_CONFIGURED";

        /// <summary>
        /// The promise token is unknown.
        /// </summary>
        public const string UnknownPromise = "UNKNOWN_PROMISE";

        /// <summary>
        /// The site failed to answer.
        /// </summary>
        public const string InternalError = "INTERNAL_ERROR";
    }

    /// <summary>
    /// The names of the server-sent events.
    /// </summary>
    public static class StreamEvents
    {
        /// <summary>
        /// The first event. It carries the response metadata.
        /// </summary>
        public const string Start = "start";

        /// <summary>
        /// One result.
        /// </summary>
        public const string Result = "result";

        /// <summary>
        /// Natural language content, such as a summary or a generated answer.
        /// </summary>
        public const string Content = "content";

        /// <summary>
        /// A request for more information.
        /// </summary>
        public const string Elicitation = "elicitation";

        /// <summary>
        /// A failure.
        /// </summary>
        public const string Failure = "failure";

        /// <summary>
        /// The response is complete. It carries the final response metadata.
        /// </summary>
        public const string Complete = "complete";

        /// <summary>
        /// The stream ends.
        /// </summary>
        public const string End = "end";
    }
}
