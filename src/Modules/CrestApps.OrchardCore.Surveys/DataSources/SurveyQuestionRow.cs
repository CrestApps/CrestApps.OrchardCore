using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// One question of one version.
/// </summary>
/// <param name="Version">The version.</param>
/// <param name="Page">The page.</param>
/// <param name="Question">The question.</param>
/// <param name="Position">The position of the question in the version, from 1.</param>
public sealed record SurveyQuestionRow(SurveyVersion Version, SurveyPage Page, SurveyQuestion Question, int Position);
