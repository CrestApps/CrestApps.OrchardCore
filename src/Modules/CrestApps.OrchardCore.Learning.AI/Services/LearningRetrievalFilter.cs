using System.Security.Claims;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.OrchardCore.AI.Core.DataSources;
using CrestApps.OrchardCore.Learning.AI.Models;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// The enrollment retrieval filter: trims every data source search with <see cref="LearningRetrievalPolicy"/>. It loads
/// the published content item of each chunk once, asks the Learning access decision for a tutor and Orchard Core's
/// View content permission for any other profile, and drops exam content whoever asks.
/// </summary>
internal sealed class LearningRetrievalFilter : IAIDataSourceResultFilter
{
    private static readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ICourseAccessService _courseAccessService;
    private readonly ICourseStructureService _courseStructure;
    private readonly IAuthorizationService _authorizationService;
    private readonly LearningTutorProfiles _tutorProfiles;
    private readonly LearningAIOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningRetrievalFilter"/> class.
    /// </summary>
    public LearningRetrievalFilter(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ICourseAccessService courseAccessService,
        ICourseStructureService courseStructure,
        IAuthorizationService authorizationService,
        LearningTutorProfiles tutorProfiles,
        IOptions<LearningAIOptions> options)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _courseAccessService = courseAccessService;
        _courseStructure = courseStructure;
        _authorizationService = authorizationService;
        _tutorProfiles = tutorProfiles;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DataSourceSearchResult>> FilterAsync(AIDataSourceResultFilterContext context, IReadOnlyList<DataSourceSearchResult> results)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (results is null || results.Count == 0)
        {
            return results ?? [];
        }

        var isTutor = await _tutorProfiles.IsTutorAsync(context.Resource, context.ProfileId);
        var user = context.User ?? _anonymous;
        var ids = results
            .Select(result => result.ReferenceId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var items = (await _contentManager.GetAsync(ids, VersionOptions.Published))
            .GroupBy(item => item.ContentItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var decisions = new Dictionary<string, bool>(StringComparer.Ordinal);
        var courses = new Dictionary<string, bool>(StringComparer.Ordinal);
        var kept = new List<DataSourceSearchResult>(results.Count);

        foreach (var result in results)
        {
            var key = result.ReferenceId ?? string.Empty;

            if (!decisions.TryGetValue(key, out var keep))
            {
                var candidate = await DescribeAsync(items.GetValueOrDefault(key), isTutor, user, courses);
                keep = LearningRetrievalPolicy.Keep(candidate, isTutor, _options.TutorLessonKinds);
                decisions[key] = keep;
            }

            if (keep)
            {
                kept.Add(result);
            }
        }

        return kept;
    }

    /// <summary>
    /// Gets whether a content item is exam content: a question, a question bank or a quiz, whose text may hold answer
    /// keys.
    /// </summary>
    /// <param name="item">The content item.</param>
    /// <returns><see langword="true"/> for exam content.</returns>
    internal static bool IsExamContent(ContentItem item)
        => string.Equals(item.ContentType, AssessmentsConstants.ContentTypes.QuestionBank, StringComparison.Ordinal)
            || string.Equals(item.ContentType, AssessmentsConstants.ContentTypes.Quiz, StringComparison.Ordinal)
            || item.Has<QuestionPart>()
            || item.Has<QuizPart>()
            || string.Equals(item.Get<CourseItemPart>(nameof(CourseItemPart))?.Kind, AssessmentsConstants.QuizKind, StringComparison.Ordinal);

    private async Task<LearningRetrievalCandidate> DescribeAsync(ContentItem item, bool isTutor, ClaimsPrincipal user, Dictionary<string, bool> courses)
    {
        if (item is null)
        {
            return new LearningRetrievalCandidate { Found = false };
        }

        if (IsExamContent(item))
        {
            return new LearningRetrievalCandidate { Found = true, IsExamContent = true };
        }

        var part = item.Get<CourseItemPart>(nameof(CourseItemPart));

        if (part is null)
        {
            return new LearningRetrievalCandidate { Found = true };
        }

        var kind = part.Kind;

        if (string.IsNullOrEmpty(kind))
        {
            var type = await _contentDefinitionManager.GetTypeDefinitionAsync(item.ContentType);
            kind = type?.Parts
                .FirstOrDefault(definition => string.Equals(definition.PartDefinition.Name, LearningConstants.ContentParts.CourseItem, StringComparison.Ordinal))
                ?.GetSettings<CourseItemPartSettings>()?.Kind;
        }

        if (!isTutor)
        {
            return new LearningRetrievalCandidate
            {
                Found = true,
                IsCourseItem = true,
                Kind = kind,
                CanView = await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, item),
                AssistantEnabled = await IsAssistantOnAsync(_courseStructure.GetCourseId(item), courses),
            };
        }

        var access = await _courseAccessService.GetItemAccessAsync(user, item);
        var courseId = access.CourseId ?? _courseStructure.GetCourseId(item);

        return new LearningRetrievalCandidate
        {
            Found = true,
            IsCourseItem = true,
            Kind = kind,
            Access = access.Reason,
            AssistantEnabled = await IsAssistantOnAsync(courseId, courses),
        };
    }

    private async Task<bool> IsAssistantOnAsync(string courseId, Dictionary<string, bool> courses)
    {
        if (string.IsNullOrEmpty(courseId))
        {
            return false;
        }

        if (!courses.TryGetValue(courseId, out var enabled))
        {
            var course = await _courseStructure.GetCourseAsync(courseId);
            enabled = course?.Get<LearningAssistantPart>(nameof(LearningAssistantPart))?.Enabled == true;
            courses[courseId] = enabled;
        }

        return enabled;
    }
}
