using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.Indexes;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.SigningKeys;
using CrestApps.OrchardCore.SigningKeys.Core;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Tests.Learning.Credentials;

/// <summary>
/// The Learning and Assessments harness with Digital Credentials: in-memory definitions, a test key ring, recipients
/// whose e-mail is known, a fixed issuer address and a queue that holds awards until the test runs them, as the site
/// runs them after the request.
/// </summary>
internal sealed class CredentialTestHarness : IAsyncDisposable
{
    public const string BaseUrl = "https://lms.example.com";

    private CredentialTestHarness(AssessmentTestHarness inner, InMemoryCredentialDefinitions definitions, RecordingCredentialAwardQueue queue, TestCredentialKeyRing ring)
    {
        Inner = inner;
        Definitions = definitions;
        Queue = queue;
        Ring = ring;
    }

    public AssessmentTestHarness Inner { get; }

    public InMemoryCredentialDefinitions Definitions { get; }

    public RecordingCredentialAwardQueue Queue { get; }

    public TestCredentialKeyRing Ring { get; }

    public TestClock Clock => Inner.Clock;

    public static async Task<CredentialTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
    {
        var definitions = new InMemoryCredentialDefinitions();
        var queue = new RecordingCredentialAwardQueue();
        var ring = new TestCredentialKeyRing();

        var inner = await AssessmentTestHarness.CreateAsync(
            services =>
            {
                services.AddOptions<SigningKeyOptions>();
                services.AddLearningCredentialsCore();
                services.AddLearningCredentialsAssessments();
                services.AddLearningCredentialsPrograms();
                services.AddLearningCredentialsCertifications();
                services.AddSingleton<ISigningKeyRing>(ring);
                services.AddSingleton<ICredentialDefinitionCatalog>(definitions);
                services.RemoveAll<ICredentialAwardQueue>();
                services.AddSingleton<ICredentialAwardQueue>(queue);
                services.RemoveAll<CredentialIssuerContext>();
                services.AddScoped<CredentialIssuerContext, FixedIssuerContext>();
                services.RemoveAll<CredentialRecipientResolver>();
                services.AddScoped<CredentialRecipientResolver, TestRecipientResolver>();
                services.TryAddScoped(_ => Moq.Mock.Of<CrestApps.OrchardCore.Learning.Programs.Services.ILearningPathService>(paths =>
                    paths.SupportsIssuingAsync(Moq.It.IsAny<CrestApps.OrchardCore.Learning.Programs.Models.LearningPathProgress>(), Moq.It.IsAny<CancellationToken>()) == Task.FromResult(true)));
                configure?.Invoke(services);
            },
            CredentialsIndexSchema.CreateAsync,
            [new CredentialAwardIndexProvider()],
            [CredentialsConstants.CollectionName]);

        inner.Courses.AddCourse("safety");
        inner.Courses.AddItem("safety", "lesson");
        inner.Courses.AddCourse("exams");
        inner.AddQuiz("exams", "final", quiz => quiz.SurveyId = "final-survey");

        return new CredentialTestHarness(inner, definitions, queue, ring);
    }

    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work) => Inner.RunAsync(work);

    public Task RunAsync(Func<IServiceProvider, Task> work) => Inner.RunAsync<object>(async services =>
    {
        await work(services);

        return null;
    });

    public Task<CredentialAwardResult> AwardAsync(CredentialAwardRequest request)
        => RunAsync(services => services.GetRequiredService<ICredentialAwardService>().AwardAsync(request));

    /// <summary>
    /// Runs the queued awards, each in its own scope, as the site does after the request.
    /// </summary>
    public async Task<IReadOnlyList<CredentialAwardResult>> RunQueuedAsync()
    {
        var results = new List<CredentialAwardResult>();

        foreach (var request in Queue.Drain())
        {
            results.Add(await AwardAsync(request));
        }

        return results;
    }

    public async Task<Enrollment> CompleteCourseAsync(string userId, string courseId = "safety")
    {
        var enrollment = await Inner.EnrollAsync(userId, courseId);

        await RunAsync(async services =>
        {
            var fresh = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId);

            await services.GetRequiredService<ILearnerProgressService>().RecordViewedAsync(fresh, "lesson", 100);
        });

        return await RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId));
    }

    /// <summary>
    /// Enrolls a learner in the exams course and tells the observers that the learner completed an attempt at the final
    /// quiz (a response to its survey), passed or not.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="pass">Whether the attempt passed.</param>
    /// <returns>The attempt.</returns>
    public async Task<SurveyResponse> PassQuizAsync(string userId, bool pass = true)
    {
        var enrollment = await Inner.EnrollAsync(userId, "exams");
        var response = new SurveyResponse
        {
            ItemId = "response-" + userId,
            SurveyId = "final-survey",
            UserId = userId,
            Status = SurveyResponseStatus.Completed,
            Grading = GradingStatus.Graded,
            Context = LearningSurveyContexts.Enrollment(enrollment.ItemId),
            CompletedUtc = Clock.UtcNow,
            Percent = pass ? 100 : 0,
            Passed = pass,
        };

        await NotifyAsync(response, (observer, context) => observer.CompletedAsync(context));

        return response;
    }

    /// <summary>
    /// Tells the observers about a response of the final quiz.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="notify">The observer method to call.</param>
    /// <returns>A task.</returns>
    public Task NotifyAsync(SurveyResponse response, Func<ISurveyResponseObserver, SurveyResponseEventContext, Task> notify)
        => RunAsync(async services =>
        {
            var context = new SurveyResponseEventContext
            {
                Survey = new Survey { ItemId = "final-survey", Kind = SurveyKinds.Quiz, Owner = LearningSurveyContexts.Owner("final") },
                Response = response,
                ActorId = "grader",
            };

            foreach (var observer in services.GetServices<ISurveyResponseObserver>())
            {
                await notify(observer, context);
            }
        });

    public Task<IReadOnlyList<CredentialAward>> AwardsAsync(string userId)
        => RunAsync(services => services.GetRequiredService<ICredentialAwardService>().ListForUserAsync(userId));

    public ValueTask DisposeAsync() => Inner.DisposeAsync();

    private sealed class FixedIssuerContext : CredentialIssuerContext
    {
        public FixedIssuerContext()
            : base(null, null)
        {
        }

        public override Task<CredentialIssuer> GetAsync()
            => Task.FromResult(new CredentialIssuer(BaseUrl, "Learning tests"));
    }

    private sealed class TestRecipientResolver : CredentialRecipientResolver
    {
        public TestRecipientResolver()
            : base(null)
        {
        }

        // "ghost" is no user; "nomail" has no e-mail address; everyone else is userId@Example.com.
        public override Task<CredentialRecipient> FindAsync(string userId)
            => Task.FromResult(userId switch
            {
                "ghost" => null,
                "nomail" => new CredentialRecipient(userId, null),
                _ => new CredentialRecipient(userId, userId + "@Example.com"),
            });

        public override Task<string> GetDisplayNameAsync(string userId)
            => Task.FromResult("Name of " + userId);
    }
}

/// <summary>
/// Credential definitions held in memory.
/// </summary>
internal sealed class InMemoryCredentialDefinitions : ICredentialDefinitionCatalog
{
    private readonly Dictionary<string, CredentialDefinition> _definitions = new(StringComparer.Ordinal);

    public CredentialDefinition Add(string definitionId, params CredentialAwardRule[] rules)
    {
        var definition = new CredentialDefinition
        {
            DefinitionId = definitionId,
            VersionId = definitionId + "-v1",
            Title = "Forklift operator",
            Achievement = new CredentialAchievement
            {
                Name = "Forklift operator",
                Description = "Operates a forklift safely.",
                Criteria = "Completed the forklift course.",
                AchievementType = "Badge",
            },
            Rules = rules.ToList(),
        };

        _definitions[definitionId] = definition;

        return definition;
    }

    public void Remove(string definitionId) => _definitions.Remove(definitionId);

    public Task<CredentialDefinition> FindAsync(string definitionId, CancellationToken cancellationToken = default)
        => Task.FromResult(definitionId is not null && _definitions.TryGetValue(definitionId, out var definition) ? definition : null);

    public Task<IReadOnlyList<CredentialDefinition>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CredentialDefinition>>(_definitions.Values.ToArray());

    public Task<IReadOnlyList<CredentialDefinition>> ListByRuleAsync(string kind, string targetId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CredentialDefinition>>(_definitions.Values
            .Where(definition => definition.Rules.Any(rule => rule.Kind == kind && rule.TargetId == targetId))
            .ToArray());
}

/// <summary>
/// Holds the awards the rules ask for.
/// </summary>
internal sealed class RecordingCredentialAwardQueue : ICredentialAwardQueue
{
    private readonly List<CredentialAwardRequest> _requests = [];

    public IReadOnlyList<CredentialAwardRequest> Requests => _requests;

    public void Enqueue(CredentialAwardRequest request) => _requests.Add(request);

    public IReadOnlyList<CredentialAwardRequest> Drain()
    {
        var drained = _requests.ToArray();
        _requests.Clear();

        return drained;
    }
}

/// <summary>
/// A key ring with real RSA keys held in memory.
/// </summary>
internal sealed class TestCredentialKeyRing : ISigningKeyRing
{
    private readonly List<SigningKey> _keys = [];

    public TestCredentialKeyRing()
    {
        Rotate();
    }

    public SigningKey Current => _keys[_keys.Count - 1];

    public SigningKey Rotate()
    {
        var kid = $"credentials-{_keys.Count + 1}";
        var privateJwk = SigningKeyJwk.Create(SigningKeyAlgorithms.RS256, kid);
        var key = new SigningKey
        {
            Kid = kid,
            Algorithm = SigningKeyAlgorithms.RS256,
            PrivateJwk = privateJwk,
            PublicJwk = SigningKeyJwk.PublicOf(privateJwk),
        };

        _keys.Add(key);

        return key;
    }

    public Task<SigningKey> GetSigningKeyAsync(string keySet, CancellationToken cancellationToken = default)
        => Task.FromResult(Current);

    public Task<IReadOnlyList<JsonObject>> GetPublishedKeysAsync(string keySet, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<JsonObject>>(_keys.Select(key => key.PublicJwk.DeepClone().AsObject()).ToArray());

    public Task<SigningKey> RotateAsync(string keySet, CancellationToken cancellationToken = default)
        => Task.FromResult(Rotate());

    public Task<bool> RotateIfDueAsync(string keySet, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<int> ImportAsync(string keySet, IEnumerable<SigningKeyImport> keys, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<bool> HasKeysAsync(string keySet, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
