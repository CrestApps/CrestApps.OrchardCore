using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core;
using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// A YesSql database with the Learning core, in-memory courses and a recording observer.
/// </summary>
internal static class LearningTestHarness
{
    public static DateTime Now => CalendarTestHarness.Now;

    public static async Task<(CalendarTestHarness Harness, InMemoryCourses Courses, RecordingEnrollmentObserver Observer)> CreateAsync(
        Action<IServiceCollection> configure = null,
        Func<ISchemaBuilder, Task> extraSchema = null,
        IIndexProvider[] extraIndexProviders = null,
        string[] extraCollections = null)
    {
        var courses = new InMemoryCourses();
        var observer = new RecordingEnrollmentObserver();

        var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton<INumberSequence, InMemoryNumberSequence>();
                services.AddDistributedMemoryCache();
                services.AddLearningCore();
                services.RemoveAll<ICourseStructureService>();
                services.AddSingleton<ICourseStructureService>(courses);
                services.AddSingleton<IEnrollmentObserver>(observer);
                configure?.Invoke(services);
            },
            async schemaBuilder =>
            {
                await LearningIndexSchema.CreateAsync(schemaBuilder);
                await LearningIndexSchema.CreateEnrollmentKeyIndexAsync(schemaBuilder);

                if (extraSchema is not null)
                {
                    await extraSchema(schemaBuilder);
                }
            },
            [.. LearningIndexProviders.All(), .. extraIndexProviders ?? []],
            [LearningConstants.CollectionName, .. extraCollections ?? []]);

        return (harness, courses, observer);
    }
}
