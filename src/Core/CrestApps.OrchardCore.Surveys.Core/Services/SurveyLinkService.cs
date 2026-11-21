using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Makes links to respond: the public link of a survey's default collector, and personal links through the Survey
/// Invitations feature.
/// </summary>
public sealed class SurveyLinkService : ISurveyLinkService
{
    private readonly SurveyCollectorStore _collectorStore;
    private readonly ISurveyPersonalLinkFactory _personalLinkFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyLinkService"/> class.
    /// </summary>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="personalLinkFactory">The personal link factory.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public SurveyLinkService(
        SurveyCollectorStore collectorStore,
        ISurveyPersonalLinkFactory personalLinkFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _collectorStore = collectorStore;
        _personalLinkFactory = personalLinkFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<string> GetPublicLinkAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        var collector = await _collectorStore.FindDefaultAsync(surveyId, cancellationToken);

        return collector is null ? null : BuildLink(collector.Token);
    }

    /// <inheritdoc />
    public Task<string> CreateLinkAsync(string surveyId, RecordRef? context, PartyRef? respondent, string email, DateTime? expiresUtc, CancellationToken cancellationToken = default)
        => _personalLinkFactory.CreateAsync(surveyId, context, respondent, email, expiresUtc, cancellationToken);

    /// <summary>
    /// Builds the absolute public link of a collector token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The URL, relative when there is no request.</returns>
    public string BuildLink(string token)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var path = $"/s/{Uri.EscapeDataString(token)}";

        return request is null
            ? path
            : $"{request.Scheme}://{request.Host}{request.PathBase}{path}";
    }
}
