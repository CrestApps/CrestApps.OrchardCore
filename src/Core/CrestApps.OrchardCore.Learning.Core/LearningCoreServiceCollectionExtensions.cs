using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Telemetry;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Core;

/// <summary>
/// Registers the Learning core services.
/// </summary>
public static class LearningCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the stores, the enrollment and progress services, the course structure reader, the access decision and
    /// its authorization handler, and the built-in kinds, sources, release rules and completion criteria.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLearningCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<EnrollmentStore>();
        services.TryAddScoped<IEnrollmentStore>(sp => sp.GetRequiredService<EnrollmentStore>());
        services.TryAddScoped<EnrollmentProgressStore>();
        services.TryAddScoped<IEnrollmentService, DefaultEnrollmentService>();
        services.TryAddScoped<ISelfEnrollmentService, DefaultSelfEnrollmentService>();
        services.TryAddScoped<ILearnerProgressService, DefaultLearnerProgressService>();
        services.TryAddScoped<ICourseStructureService, DefaultCourseStructureService>();
        services.TryAddScoped<ICourseAccessService, DefaultCourseAccessService>();
        services.TryAddScoped<CourseReleaseEvaluator>();
        services.TryAddScoped<CourseCompletionEvaluator>();
        services.TryAddScoped<CourseListSynchronizer>();
        services.TryAddScoped<LearningUpkeep>();
        services.TryAddScoped<CourseSettingsPolicy>();
        services.AddEnrollmentObserver<LearningTelemetryObserver>();
        services.AddOptions<LearningUpkeepOptions>();
        services.TryAddScoped(sp => new Lazy<IAuthorizationService>(sp.GetRequiredService<IAuthorizationService>));
        services.TryAddScoped(sp => new Lazy<ICourseAccessService>(sp.GetRequiredService<ICourseAccessService>));
        services.TryAddScoped(sp => new Lazy<ICourseStructureService>(sp.GetRequiredService<ICourseStructureService>));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, LearningContentAccessHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, LearningRecordAuthorizationHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, CourseStaffContentAuthorizationHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICoursePrerequisiteEvaluator, CompletedCoursePrerequisiteEvaluator>());

        services.AddCourseItemReleaseRule<ItemCompletedReleaseRule>();
        services.AddCourseItemReleaseRule<DateReleaseRule>();
        services.AddCourseItemReleaseRule<DaysAfterEnrollmentReleaseRule>();
        services.AddCourseCompletionCriterion<MinimumGradeCriterion>();
        services.AddCourseCompletionCriterion<MinimumTimeCriterion>();
        services.AddCourseCompletionCriterion<ItemPassedCriterion>();
        services.AddCourseCompletionCriterion<StaffApprovalCriterion>();

        services.AddEnrollmentSource(LearningConstants.EnrollmentSources.Manual, LocalizationSource.Create("Enrolled by staff", typeof(LearningCoreServiceCollectionExtensions)));
        services.AddEnrollmentSource(LearningConstants.EnrollmentSources.Self, LocalizationSource.Create("Self enrollment", typeof(LearningCoreServiceCollectionExtensions)));
        services.AddEnrollmentSource(LearningConstants.EnrollmentSources.Key, LocalizationSource.Create("Enrollment key", typeof(LearningCoreServiceCollectionExtensions)));
        services.AddEnrollmentSource(LearningConstants.EnrollmentSources.Pipeline, LocalizationSource.Create("Data pipeline", typeof(LearningCoreServiceCollectionExtensions)));
        services.AddEnrollmentSource(LearningConstants.EnrollmentSources.Workflow, LocalizationSource.Create("Workflow", typeof(LearningCoreServiceCollectionExtensions)));

        services.AddCourseItemKind(LearningConstants.CourseItemKinds.Page, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("Page", typeof(LearningCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Lesson", typeof(LearningCoreServiceCollectionExtensions));
            kind.ContentType = LearningConstants.ContentTypes.PageLesson;
            kind.Icon = "fa-solid fa-file-lines";
            kind.CompletionModes.Add(CompletionModes.Viewed);
            kind.CompletionModes.Add(CompletionModes.MarkedDone);
            kind.CompletionModes.Add(CompletionModes.MinimumTime);
        });

        services.AddCourseItemKind<VideoLessonPart>(LearningConstants.CourseItemKinds.Video, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("Video", typeof(LearningCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Lesson", typeof(LearningCoreServiceCollectionExtensions));
            kind.ContentType = LearningConstants.ContentTypes.VideoLesson;
            kind.Icon = "fa-solid fa-circle-play";
            kind.CompletionModes.Add(CompletionModes.Watched);
            kind.CompletionModes.Add(CompletionModes.Viewed);
            kind.CompletionModes.Add(CompletionModes.MarkedDone);
        });

        services.AddCourseItemKind(LearningConstants.CourseItemKinds.File, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("File", typeof(LearningCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Lesson", typeof(LearningCoreServiceCollectionExtensions));
            kind.ContentType = LearningConstants.ContentTypes.FileLesson;
            kind.Icon = "fa-solid fa-file-arrow-down";
            kind.CompletionModes.Add(CompletionModes.Viewed);
            kind.CompletionModes.Add(CompletionModes.MarkedDone);
        });

        services.AddCourseItemKind(LearningConstants.CourseItemKinds.Link, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("Link", typeof(LearningCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Lesson", typeof(LearningCoreServiceCollectionExtensions));
            kind.ContentType = LearningConstants.ContentTypes.LinkLesson;
            kind.Icon = "fa-solid fa-link";
            kind.CompletionModes.Add(CompletionModes.Viewed);
            kind.CompletionModes.Add(CompletionModes.MarkedDone);
        });

        services.AddCourseItemKind<AcknowledgementPart>(LearningConstants.CourseItemKinds.Acknowledgement, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("Acknowledgement", typeof(LearningCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Acknowledgement", typeof(LearningCoreServiceCollectionExtensions));
            kind.ContentType = LearningConstants.ContentTypes.Acknowledgement;
            kind.Icon = "fa-solid fa-file-signature";
            kind.CompletionModes.Add(CompletionModes.Acknowledged);
        });

        return services;
    }
}
