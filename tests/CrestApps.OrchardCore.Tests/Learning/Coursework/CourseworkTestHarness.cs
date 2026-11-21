using System.Collections.Concurrent;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Coursework.Core.Indexes;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// The assessments harness with the coursework core on a real store, in-memory content items (coursework and rubrics)
/// and an in-memory protected file store that can refuse a file by name.
/// </summary>
internal sealed class CourseworkTestHarness : IAsyncDisposable
{
    private CourseworkTestHarness(AssessmentTestHarness inner, InMemoryCourseworkContent content, InMemoryProtectedFiles files)
    {
        Inner = inner;
        Content = content;
        Files = files;
    }

    public AssessmentTestHarness Inner { get; }

    public InMemoryCourses Courses => Inner.Courses;

    public TestClock Clock => Inner.Clock;

    public InMemoryCourseworkContent Content { get; }

    public InMemoryProtectedFiles Files { get; }

    public static async Task<CourseworkTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
    {
        var files = new InMemoryProtectedFiles();
        InMemoryCourseworkContent content = null;

        var inner = await AssessmentTestHarness.CreateAsync(
            services =>
            {
                services.AddLearningCourseworkCore();
                services.RemoveAll<IProtectedFileStore<CourseworkFiles>>();
                services.AddSingleton<IProtectedFileStore<CourseworkFiles>>(files);
                services.RemoveAll<ICourseworkContentSource>();
                services.AddSingleton<ICourseworkContentSource>(_ => content);
                configure?.Invoke(services);
            },
            CourseworkIndexSchema.CreateAsync,
            [new SubmissionIndexProvider()],
            [CourseworkConstants.CollectionName]);

        content = new InMemoryCourseworkContent(inner.Courses);

        return new CourseworkTestHarness(inner, content, files);
    }

    public ContentItem AddCoursework(string courseId, string id, Action<CourseworkPart> configure = null, string completionMode = CompletionModes.Submitted, decimal weight = 1)
    {
        var item = Courses.AddItem(courseId, id, kind: CourseworkConstants.CourseworkKind, completionMode: completionMode, configure: part =>
        {
            part.Gradable = true;
            part.Weight = weight;
        });

        item.Alter<CourseworkPart>(part => configure?.Invoke(part));

        return item;
    }

    public ContentItem AddRubric(string id, params (string Criterion, (string Level, decimal Points)[] Levels)[] criteria)
    {
        var rubric = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = CourseworkConstants.ContentTypes.Rubric,
            DisplayText = id,
            Published = true,
            Latest = true,
        };

        rubric.Alter<RubricPart>(part => part.Criteria = criteria
            .Select(criterion => new RubricCriterion
            {
                Id = criterion.Criterion,
                Title = criterion.Criterion,
                Levels = criterion.Levels.Select(level => new RubricLevel { Id = level.Level, Title = level.Level, Points = level.Points }).ToList(),
            })
            .ToList());

        Content.Add(rubric);

        return rubric;
    }

    public Task<Enrollment> EnrollAsync(string userId, string courseId)
        => Inner.EnrollAsync(userId, courseId);

    public Task<SubmissionResult> SubmitAsync(string userId, string courseworkId, string text = null, params (string Name, int Bytes)[] files)
        => RunAsync(services => services.GetRequiredService<ISubmissionService>().SubmitAsync(new SubmitCourseworkRequest
        {
            UserId = userId,
            CourseworkId = courseworkId,
            Text = text,
            Files = files.Select(file => new SubmissionUpload(file.Name, file.Bytes, () => new MemoryStream(new byte[file.Bytes]))).ToList(),
        }));

    public Task<SubmissionResult> GradeAsync(string submissionId, string actor, decimal? points = null, IDictionary<string, string> choices = null, string reason = null, string feedback = "Good work")
        => RunAsync(services => services.GetRequiredService<ISubmissionService>().GradeAsync(new GradeSubmissionRequest
        {
            SubmissionId = submissionId,
            Actor = actor,
            Points = points,
            RubricChoices = choices ?? new Dictionary<string, string>(StringComparer.Ordinal),
            Feedback = feedback,
            Reason = reason,
        }));

    public Task<SubmissionResult> ReturnAsync(string submissionId, string actor, string feedback = "Please add the references.")
        => RunAsync(services => services.GetRequiredService<ISubmissionService>().ReturnAsync(submissionId, feedback, actor));

    public Task<Submission> FindAsync(string submissionId)
        => RunAsync(services => services.GetRequiredService<SubmissionStore>().FindByIdAsync(submissionId).AsTask());

    public Task<Enrollment> FindEnrollmentAsync(string enrollmentId)
        => RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollmentId));

    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
        => Inner.RunAsync(work);

    public ValueTask DisposeAsync()
        => Inner.DisposeAsync();
}

/// <summary>
/// The coursework items of the in-memory courses and the rubrics a test adds.
/// </summary>
internal sealed class InMemoryCourseworkContent : ICourseworkContentSource
{
    private readonly InMemoryCourses _courses;
    private readonly ConcurrentDictionary<string, ContentItem> _items = new(StringComparer.Ordinal);

    public InMemoryCourseworkContent(InMemoryCourses courses)
    {
        _courses = courses;
    }

    public void Add(ContentItem item)
        => _items[item.ContentItemId] = item;

    public Task<ContentItem> GetPublishedAsync(string contentItemId)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            return Task.FromResult<ContentItem>(null);
        }

        if (_items.TryGetValue(contentItemId, out var item))
        {
            return Task.FromResult(item);
        }

        try
        {
            return Task.FromResult(_courses.Get(contentItemId));
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult<ContentItem>(null);
        }
    }
}

/// <summary>
/// A protected file store in memory that refuses the files whose names a test lists, as an antivirus scanner would.
/// </summary>
internal sealed class InMemoryProtectedFiles : IProtectedFileStore<CourseworkFiles>
{
    private int _next;

    public ConcurrentDictionary<string, ProtectedFile> Stored { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Refused { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Deleted { get; } = [];

    public Task<ProtectedFile> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        if (Refused.Contains(fileName))
        {
            throw new InvalidOperationException($"The file '{fileName}' was refused.");
        }

        using var buffer = new MemoryStream();

        content.CopyTo(buffer);

        var file = new ProtectedFile("f" + Interlocked.Increment(ref _next), fileName, buffer.Length);

        Stored[file.FileId] = file;

        return Task.FromResult(file);
    }

    public Task<ProtectedFile> FindAsync(string fileId, CancellationToken cancellationToken = default)
        => Task.FromResult(fileId is not null && Stored.TryGetValue(fileId, out var file) ? file : null);

    public Task<Stream> OpenAsync(string fileId, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(fileId is not null && Stored.ContainsKey(fileId) ? new MemoryStream() : null);

    public Task<bool> DeleteAsync(string fileId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(fileId);

        return Task.FromResult(fileId is not null && Stored.TryRemove(fileId, out _));
    }
}
