using CrestApps.OrchardCore.Learning.Assessments.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Services;

/// <summary>
/// Snapshots and scores one question type. A new question type is a content type, a part, an editor driver and a
/// scorer.
/// </summary>
public interface IQuestionScorer
{
    /// <summary>
    /// Gets the question type, one of <see cref="AssessmentsConstants.QuestionTypes"/> or a type another module adds.
    /// </summary>
    string QuestionType { get; }

    /// <summary>
    /// Copies what the question shows and what scores it into an attempt item: the choices in their shuffled order and
    /// the key.
    /// </summary>
    /// <param name="question">The question content item.</param>
    /// <param name="item">The attempt item to fill.</param>
    /// <param name="random">The random source for shuffling.</param>
    void Snapshot(ContentItem question, AttemptItem item, Random random);

    /// <summary>
    /// Scores a response from the attempt item alone.
    /// </summary>
    /// <param name="item">The attempt item with its response.</param>
    /// <returns>The points earned, or <see langword="null"/> when a grader must mark it.</returns>
    decimal? Score(AttemptItem item);
}
