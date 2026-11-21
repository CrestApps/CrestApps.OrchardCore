using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a completed, non-test <see cref="SurveyResponse"/> to its <see cref="SurveyAnswerIndex"/> rows, asking each
/// question's handler for the rows of its answer. The definition comes from the version the response ran on, read
/// without a session query so mapping never re-enters the session while it is flushing.
/// </summary>
public sealed class SurveyAnswerIndexProvider : IndexProvider<SurveyResponse>, IScopedIndexProvider
{
    private readonly IServiceProvider _serviceProvider;

    private ISurveyControlRegistry _registry;
    private SurveyVersionDefinitionReader _definitions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnswerIndexProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The scope's services, resolved on first use.</param>
    public SurveyAnswerIndexProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        CollectionName = SurveysConstants.ResponsesCollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SurveyResponse> context)
        => context.For<SurveyAnswerIndex>()
            .ExclusiveToDocumentType()
            .Map(async response =>
            {
                if (response.Status != SurveyResponseStatus.Completed || response.IsTest || response.Answers is not { Count: > 0 })
                {
                    return null;
                }

                // Resolved on first use, as Orchard Core's own index providers do.
                _registry ??= _serviceProvider.GetRequiredService<ISurveyControlRegistry>();
                _definitions ??= _serviceProvider.GetRequiredService<SurveyVersionDefinitionReader>();

                var definition = await _definitions.GetAsync(response.SurveyVersionId);

                return definition is null ? null : SurveyAnswerIndexRows.Build(response, definition, _registry);
            });
}
