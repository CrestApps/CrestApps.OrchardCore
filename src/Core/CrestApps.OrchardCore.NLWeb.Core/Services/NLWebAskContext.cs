using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Models;

namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// The state of one NLWeb question while the ask pipeline answers it.
/// </summary>
public sealed class NLWebAskContext
{
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();

    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebAskContext"/> class.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="user">The caller.</param>
    public NLWebAskContext(
        NLWebAskRequest request,
        ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(request);

        Request = request;
        User = user ?? new ClaimsPrincipal(new ClaimsIdentity());
        QueryText = request.Query.DecontextualizedText ?? request.Query.Text;
        ItemType = request.Query.ItemType;
        Site = request.Query.Site;
        Mode = string.IsNullOrEmpty(request.Prefer.Mode) ? NLWebProtocol.Modes.List : request.Prefer.Mode;
    }

    /// <summary>
    /// Gets the request.
    /// </summary>
    public NLWebAskRequest Request { get; }

    /// <summary>
    /// Gets the time elapsed since the pipeline started answering the question.
    /// </summary>
    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTimestamp);

    /// <summary>
    /// Gets the caller. An anonymous caller has an unauthenticated identity.
    /// </summary>
    public ClaimsPrincipal User { get; }

    /// <summary>
    /// Gets or sets the query the retrievers search for. Query processors can rewrite it, for example to include the
    /// conversation's context.
    /// </summary>
    public string QueryText { get; set; }

    /// <summary>
    /// Gets or sets the schema.org type the results should have, when known.
    /// </summary>
    public string ItemType { get; set; }

    /// <summary>
    /// Gets or sets the part of the site to search.
    /// </summary>
    public string Site { get; set; }

    /// <summary>
    /// Gets or sets the response mode: <c>list</c>, <c>summarize</c> or <c>generate</c>.
    /// </summary>
    public string Mode { get; set; }

    /// <summary>
    /// Gets or sets the most results the answer has.
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Gets or sets how many candidates the retrievers find for each result, so filtering and ranking still leave
    /// enough results.
    /// </summary>
    public int CandidateFactor { get; set; } = 3;

    /// <summary>
    /// Gets the results, in the order they are returned.
    /// </summary>
    public IList<NLWebResult> Results { get; } = [];

    /// <summary>
    /// Gets or sets the request for more information to return instead of an answer.
    /// </summary>
    public NLWebElicitationEvent Elicitation { get; set; }

    /// <summary>
    /// Gets or sets the failure to return instead of an answer.
    /// </summary>
    public NLWebFailureEvent Failure { get; set; }

    /// <summary>
    /// Gets or sets the session context to return to the caller, which it sends back with its next request.
    /// </summary>
    public JsonObject SessionContext { get; set; }

    /// <summary>
    /// Gets a bag that the pipeline's services can use to share values.
    /// </summary>
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Gets a value indicating whether a step ended the pipeline with a failure or a request for more information.
    /// </summary>
    public bool IsCompleted => Failure is not null || Elicitation is not null;
}
