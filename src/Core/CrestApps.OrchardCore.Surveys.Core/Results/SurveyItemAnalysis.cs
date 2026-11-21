namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// The analysis of one question across the scored responses that showed it.
/// </summary>
/// <param name="QuestionId">The question.</param>
/// <param name="Answers">How many scored answers the analysis rests on.</param>
/// <param name="Difficulty">The mean share of the points respondents got, from 0 (nobody) to 1 (everybody); higher is easier.</param>
/// <param name="Discrimination">The mean share of the points in the top 27% of responses minus the bottom 27%, from -1 to 1;
/// or <see langword="null"/> with fewer than two responses. Below 0.2 the question tells strong and weak respondents apart
/// poorly.</param>
public sealed record SurveyItemAnalysis(string QuestionId, int Answers, double Difficulty, double? Discrimination);
