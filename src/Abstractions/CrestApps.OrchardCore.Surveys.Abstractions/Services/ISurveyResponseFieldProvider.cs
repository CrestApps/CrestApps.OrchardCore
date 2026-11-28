using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Adds fields of a consumer to the SurveyResponses data set, such as the course of an enrollment. Register an
/// implementation with <c>services.AddSurveyResponseFieldProvider&lt;T&gt;()</c>.
/// </summary>
public interface ISurveyResponseFieldProvider
{
    /// <summary>
    /// Describes the fields the provider adds. Field names never change once shipped.
    /// </summary>
    /// <returns>The fields: name, display name and type.</returns>
    IEnumerable<(string Name, string DisplayName, DataFieldType Type)> DescribeFields();

    /// <summary>
    /// Writes the provider's fields of a response into a row.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="row">The row, by field name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task WriteFieldsAsync(SurveyResponse response, IDictionary<string, object> row, CancellationToken cancellationToken = default);
}
