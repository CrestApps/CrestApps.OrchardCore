using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// The base of the data sets whose rows are parts of a completed response, expanded with the definition of its version.
/// </summary>
public abstract class SurveyResponseChildDataSet : SurveysDataSetBase<SurveyResponse, SurveyResponseIndex, SurveyResponseChildRow>
{
    private readonly ISurveyVersionCache _versions;

    private protected SurveyResponseChildDataSet(
        DataSetDescriptor descriptor,
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        ISurveyVersionCache versions)
        : base(descriptor, session, authorizationService, access, SurveysConstants.ResponsesCollectionName)
    {
        _versions = versions;
    }

    /// <summary>
    /// Adds a field read from the row's values.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="displayName">The field's label.</param>
    /// <param name="type">The type.</param>
    /// <param name="group">The group.</param>
    /// <returns>The field.</returns>
    protected DataField AddValue(string name, string displayName, DataFieldType type, string group)
        => AddField(name, displayName, type, row => row.Values.GetValueOrDefault(name), group);

    /// <summary>
    /// Expands a response into its rows.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="definition">The definition of its version, or <see langword="null"/>.</param>
    /// <returns>The values of each row.</returns>
    protected abstract IEnumerable<Dictionary<string, object>> Expand(SurveyResponse response, SurveyDefinition definition);

    /// <inheritdoc />
    protected override IEnumerable<SurveyResponseChildRow> GetRows(SurveyResponse document)
    {
        yield return new SurveyResponseChildRow { Response = document };
    }

    /// <inheritdoc />
    /// <remarks>Expands the response before the conditions are evaluated for an anonymous survey's group sizes, so a
    /// condition on an answer counts the rows the consumer keeps.</remarks>
    protected override async IAsyncEnumerable<SurveyResponseChildRow> CompleteAsync(SurveyResponseChildRow row, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var version = await _versions.GetAsync(row.Response.SurveyVersionId, cancellationToken);
        var shows = AttributesOf(row.Response);

        foreach (var values in Expand(row.Response, version?.Definition))
        {
            // An anonymous survey's version shows only when enough completed responses share its joint key.
            if (!shows.ShowsVersion && values.ContainsKey("VersionNumber"))
            {
                values["VersionNumber"] = null;
            }

            yield return new SurveyResponseChildRow
            {
                Response = row.Response,
                Values = values,
            };
        }
    }

    /// <inheritdoc />
    protected override Expression<Func<SurveyResponseIndex, bool>> AnonymousRowsOf(string surveyId)
    {
        var completed = nameof(SurveyResponseStatus.Completed);

        return index => index.SurveyId == surveyId && index.Status == completed && !index.IsTest;
    }

    /// <inheritdoc />
    protected override SurveyResponse ResponseOf(SurveyResponseChildRow row)
        => row?.Response;

    /// <inheritdoc />
    protected override IQueryIndex<SurveyResponseIndex> OfSurveys(IQueryIndex<SurveyResponseIndex> rows, string[] surveyIds)
    {
        var completed = nameof(SurveyResponseStatus.Completed);

        return surveyIds is null
            ? rows.Where(index => index.Status == completed && !index.IsTest)
            : rows.Where(index => index.SurveyId.IsIn(surveyIds) && index.Status == completed && !index.IsTest);
    }

    /// <summary>
    /// Marks a time read back from storage as UTC.
    /// </summary>
    /// <param name="value">The time.</param>
    /// <returns>The time in UTC, or <see langword="null"/>.</returns>
    protected static DateTime? Utc(DateTime? value)
        => value is { } time ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : null;
}
