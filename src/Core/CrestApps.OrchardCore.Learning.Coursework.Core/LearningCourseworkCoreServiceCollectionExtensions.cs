using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Learning coursework core.
/// </summary>
public static class LearningCourseworkCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the coursework course item kind, the submission store and service, the protected file area of
    /// submitted files with its access policy, and the submissions in the grading list.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLearningCourseworkCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<SubmissionStore>();
        services.TryAddScoped<ICourseworkContentSource, DefaultCourseworkContentSource>();
        services.TryAddScoped<ISubmissionService, DefaultSubmissionService>();
        services.AddProtectedFileArea<CourseworkFiles>(CourseworkConstants.FilesFolder);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProtectedFileAccessPolicy<CourseworkFiles>, CourseworkFilesPolicy>());
        services.AddGradingQueueSource<SubmissionGradingQueueSource>();
        services.AddCourseItemKind<CourseworkPart>(CourseworkConstants.CourseworkKind, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("Coursework", typeof(LearningCourseworkCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Coursework", typeof(LearningCourseworkCoreServiceCollectionExtensions));
            kind.ContentType = CourseworkConstants.ContentTypes.Coursework;
            kind.CompletionModes.Add(CompletionModes.Submitted);
            kind.CompletionModes.Add(CompletionModes.Graded);
            kind.ProducesGrade = true;
            kind.Icon = "fa-solid fa-file-pen";
        });

        return services;
    }
}
