using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.Deployments;

/// <summary>
/// Exports quiz attempts, quiz overrides and quiz access codes (as hashes), naming people by user name.
/// </summary>
public sealed class LearningAttemptsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LearningAttemptsDeploymentStep>("Learning");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LearningAttemptsDeploymentStep>("Learning quiz attempts (learner records)");

    /// <inheritdoc />
    public override string Name => AssessmentsConstants.AttemptsRecipeStep;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets the quiz to export, or <see langword="null"/> for every quiz.
    /// </summary>
    public string QuizId { get; set; }

    /// <summary>
    /// Gets or sets whether to export the quiz overrides too.
    /// </summary>
    public bool IncludeOverrides { get; set; } = true;
}
