using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Turns the responses a learner gave to the survey of a course item into the item's result, for one kind of course
/// item (a course survey is answered once; a quiz combines its attempts). Register an implementation with
/// <c>services.AddScoped&lt;ILearningSurveyItemResultProvider, T&gt;()</c>.
/// </summary>
public interface ILearningSurveyItemResultProvider
{
    /// <summary>
    /// Gets the course item kind the provider serves, such as Quiz.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Builds the result of a course item from the learner's responses.
    /// </summary>
    /// <param name="item">The course item.</param>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="definition">The definition of the version the latest graded response ran on (or, with none graded,
    /// the version of the response the change is about), such as its pass mark. Each response also carries the pass
    /// Surveys decided against its own version.</param>
    /// <param name="responses">The learner's responses through the enrollment, voided ones left out.</param>
    /// <returns>The result, or <see langword="null"/> to record nothing.</returns>
    CourseItemResult Build(ContentItem item, Enrollment enrollment, SurveyDefinition definition, IReadOnlyList<SurveyResponse> responses);
}
