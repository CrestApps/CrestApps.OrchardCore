using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Xapi;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;

/// <summary>
/// Why a cmi5 launch or fetch was refused, besides the package errors.
/// </summary>
public static class Cmi5LaunchErrors
{
    /// <summary>The item holds no cmi5 course.</summary>
    public const string NotCmi5 = "NotCmi5";

    /// <summary>The course has no such AU.</summary>
    public const string NoSuchAu = "NoSuchAu";
}

/// <summary>
/// A launch of a cmi5 AU, as the player page asks for it.
/// </summary>
public sealed class Cmi5LaunchRequest
{
    /// <summary>Gets or sets the signed-in learner.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the learner's user name, which the actor's name shows.</summary>
    public string UserName { get; set; }

    /// <summary>Gets or sets the package item.</summary>
    public string PackageItemId { get; set; }

    /// <summary>Gets or sets the position of the AU in the course.</summary>
    public int AuIndex { get; set; }

    /// <summary>Gets or sets the absolute URL of the LRS endpoint, with a trailing slash.</summary>
    public string EndpointUrl { get; set; }

    /// <summary>Gets or sets the absolute URL of the fetch endpoint, to which the fetch token is added.</summary>
    public string FetchUrlBase { get; set; }

    /// <summary>Gets or sets the absolute URL the AU returns to.</summary>
    public string ReturnUrl { get; set; }

    /// <summary>Gets or sets the path of the run endpoint, to which the run token and the file path are added.</summary>
    public string RunPathBase { get; set; }

    /// <summary>Gets or sets the learner's language, for the learner preferences.</summary>
    public string Language { get; set; }
}

/// <summary>
/// A cmi5 launch, or why it was refused.
/// </summary>
public sealed class Cmi5LaunchResult
{
    /// <summary>Gets or sets the error.</summary>
    public string Error { get; set; }

    /// <summary>Gets or sets the URL that opens the AU.</summary>
    public string LaunchUrl { get; set; }

    /// <summary>Gets or sets whether the AU opens in a window of its own (an absolute AU, or OwnWindow) rather than the sandboxed frame.</summary>
    public bool OwnWindow { get; set; }

    /// <summary>Gets or sets the session.</summary>
    public string SessionId { get; set; }

    /// <summary>Gets or sets the registration.</summary>
    public string Registration { get; set; }

    /// <summary>Gets or sets whether the launch is a review of a completed enrollment.</summary>
    public bool Review { get; set; }

    /// <summary>Gets whether the AU was launched.</summary>
    public bool Succeeded => Error is null;
}

/// <summary>
/// The answer of the fetch URL: the auth token, or a cmi5 error.
/// </summary>
/// <param name="AuthToken">The auth token, or <see langword="null"/>.</param>
/// <param name="ErrorCode">The cmi5 error code: 1 already in use, 2 expired, 3 general application error.</param>
/// <param name="ErrorText">Why.</param>
public sealed record Cmi5FetchResult(string AuthToken, string ErrorCode, string ErrorText);

/// <summary>
/// Launches cmi5 AUs (cmi5 Quartz section 8 and 10) and answers their fetch URL. A launch checks the learner's own
/// enrollment, access window and the item's release rules (as for SCORM), gives the enrollment a registration on the
/// course, writes <c>abandoned</c> for a session left open, starts a session, writes <c>LMS.LaunchData</c> to the
/// state API and the learner preferences agent profile when there is none, writes the <c>launched</c> statement and
/// returns the launch URL with <c>endpoint</c>, <c>fetch</c>, <c>registration</c>, <c>activityId</c> and <c>actor</c> (an
/// account of the site's home page and the user id, never the e-mail address). The fetch URL hands the auth token out
/// once; it opens the LRS only for that actor, registration and AU, and expires with the session.
/// </summary>
public sealed class Cmi5LaunchService
{
    /// <summary>
    /// How long a session's tokens are valid.
    /// </summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    /// <summary>
    /// The authority of the statements the LMS writes for cmi5.
    /// </summary>
    public const string AuthorityKey = "lms:cmi5";

    private const string FetchPurpose = "CrestApps.OrchardCore.Learning.Packages.Cmi5.Fetch";
    private const int EndedSessionsKept = 20;

    private readonly PackageRuntimeService _runtime;
    private readonly PackageRuntimeStore _store;
    private readonly PackageLaunchTokens _runTokens;
    private readonly XapiLaunchTokens _launchTokens;
    private readonly XapiStatementService _statements;
    private readonly XapiDocumentStore _documents;
    private readonly XapiHomePage _homePage;
    private readonly EnrollmentWriteGate _writeGate;
    private readonly IDataProtector _fetchProtector;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="Cmi5LaunchService"/> class.
    /// </summary>
    public Cmi5LaunchService(
        PackageRuntimeService runtime,
        PackageRuntimeStore store,
        PackageLaunchTokens runTokens,
        XapiLaunchTokens launchTokens,
        XapiStatementService statements,
        XapiDocumentStore documents,
        XapiHomePage homePage,
        EnrollmentWriteGate writeGate,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock)
    {
        _runtime = runtime;
        _store = store;
        _runTokens = runTokens;
        _launchTokens = launchTokens;
        _statements = statements;
        _documents = documents;
        _homePage = homePage;
        _writeGate = writeGate;
        _fetchProtector = dataProtectionProvider.CreateProtector(FetchPurpose);
        _clock = clock;
    }

    /// <summary>
    /// Gets the authority of the statements the LMS writes for cmi5.
    /// </summary>
    /// <param name="homePage">The site's home page.</param>
    /// <returns>The authority.</returns>
    public static JsonObject Authority(string homePage)
        => XapiAgents.Account(homePage, "learning-lms", "Learning");

    /// <summary>
    /// Launches an AU.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The launch, or why it was refused.</returns>
    public async Task<Cmi5LaunchResult> LaunchAsync(Cmi5LaunchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = await _runtime.ResolveLaunchAsync(request.UserId, request.PackageItemId, cancellationToken);

        if (target.Error is not null)
        {
            return new Cmi5LaunchResult { Error = target.Error };
        }

        var part = target.Part;
        var course = part.Cmi5;

        if (!string.Equals(part.Standard, PackagesConstants.Standards.Cmi5, StringComparison.Ordinal) || course is null)
        {
            return new Cmi5LaunchResult { Error = Cmi5LaunchErrors.NotCmi5 };
        }

        if (request.AuIndex < 0 || request.AuIndex >= course.Aus.Count)
        {
            return new Cmi5LaunchResult { Error = Cmi5LaunchErrors.NoSuchAu };
        }

        var au = course.Aus[request.AuIndex];
        var enrollment = target.Enrollment;
        var itemId = target.Item.ContentItemId;
        var homePage = await _homePage.GetAsync();
        var actor = XapiAgents.Account(homePage, request.UserId, request.UserName);
        var actorKey = XapiAgents.Key(actor);
        var activityId = Cmi5Vocabulary.AuActivityId(homePage, itemId, au.Id);
        var now = XapiFormats.ToMilliseconds(_clock.UtcNow);
        var session = new Cmi5Session
        {
            SessionId = Guid.NewGuid().ToString(),
            AuId = au.Id,
            ActivityId = activityId,
            LaunchMode = target.Review ? "Review" : "Normal",
            StartedUtc = now,
            ExpiresUtc = now + SessionLifetime,
        };

        string registration = null;
        string launchUrl = null;
        var ownWindow = au.IsAbsolute || string.Equals(au.LaunchMethod, "OwnWindow", StringComparison.Ordinal);

        var error = await _writeGate.RunLockedAsync(PackageRuntimeService.LockKey(enrollment.ItemId, itemId), async _ =>
        {
            var state = await _store.FindFreshAsync(enrollment.ItemId, itemId, cancellationToken);
            var isNew = state is null;

            state ??= new PackageRuntimeState
            {
                ItemId = IdGenerator.GenerateId(),
                EnrollmentId = enrollment.ItemId,
                UserId = request.UserId,
                CourseId = enrollment.CourseId,
                PackageItemId = itemId,
            };

            // A new course in the item, or a SCORM state before it: the learner starts a new registration.
            if (!string.Equals(state.Standard, PackagesConstants.Standards.Cmi5, StringComparison.Ordinal) || state.Cmi5 is null || !string.Equals(state.Cmi5.CourseId, course.Id, StringComparison.Ordinal))
            {
                state.Cmi5 = new Cmi5State
                {
                    Registration = Guid.NewGuid().ToString(),
                    CourseId = course.Id,
                };
                state.Cmi = null;
                state.Cmi2004 = null;
            }

            state.Standard = PackagesConstants.Standards.Cmi5;

            var cmi5 = state.Cmi5;
            var statements = new List<JsonObject>();

            registration = cmi5.Registration;

            // A session left open ended without terminated: cmi5 asks the LMS to write abandoned for it.
            foreach (var open in cmi5.Sessions.Where(open => !open.Ended).ToList())
            {
                open.AbandonedUtc = now;
                statements.Add(Abandoned(actor, registration, open, now));
            }

            cmi5.Sessions.Add(session);

            var ended = cmi5.Sessions.Where(item => item.Ended).OrderByDescending(item => item.StartedUtc).Skip(EndedSessionsKept).ToHashSet();

            cmi5.Sessions = [.. cmi5.Sessions.Where(item => !ended.Contains(item))];

            var auState = cmi5.Aus.FirstOrDefault(item => item.AuId == au.Id);

            if (auState is null)
            {
                auState = new Cmi5AuState { AuId = au.Id };
                cmi5.Aus.Add(auState);
            }

            auState.LastLaunchedUtc = now;

            if (!target.Review)
            {
                state.Sessions++;
                state.FirstLaunchedUtc ??= now;
                state.LastLaunchedUtc = now;
            }

            state.CurrentLaunchId = session.SessionId;
            state.PackageFileId = part.PackageFileId;
            state.PackageTitle = target.Item.DisplayText;

            launchUrl = BuildLaunchUrl(request, au, session, actor, registration, activityId, part, enrollment.ItemId, itemId);
            statements.Add(Launched(actor, registration, au, session, activityId));

            await WriteLaunchDataAsync(actor, actorKey, registration, au, session, activityId, request.ReturnUrl, now, cancellationToken);
            await WritePreferencesAsync(actor, actorKey, request.Language, now, cancellationToken);

            if (isNew)
            {
                await _store.CreateAsync(state, cancellationToken);
            }
            else
            {
                await _store.UpdateAsync(state, cancellationToken);
            }

            var written = await _statements.WriteTrustedAsync(statements, Authority(homePage), AuthorityKey, cancellationToken);

            return written.Succeeded ? null : written.Message;
        }, () => PackageRuntimeErrors.Busy, cancellationToken);

        if (error is not null)
        {
            return new Cmi5LaunchResult { Error = error };
        }

        return new Cmi5LaunchResult
        {
            LaunchUrl = launchUrl,
            OwnWindow = ownWindow,
            SessionId = session.SessionId,
            Registration = registration,
            Review = target.Review,
        };
    }

    /// <summary>
    /// Answers the fetch URL of a session: the auth token the first time, and the cmi5 errors after.
    /// </summary>
    /// <param name="fetchToken">The fetch token of the URL.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The auth token, or the error.</returns>
    public async Task<Cmi5FetchResult> FetchAsync(string fetchToken, CancellationToken cancellationToken = default)
    {
        var fetch = ReadFetchToken(fetchToken);

        if (fetch is null)
        {
            return new Cmi5FetchResult(null, "3", "The fetch URL is not valid.");
        }

        if (fetch.ExpiresUtc <= _clock.UtcNow)
        {
            return new Cmi5FetchResult(null, "2", "The fetch URL has expired.");
        }

        Cmi5FetchResult result = null;

        var busy = await _writeGate.RunLockedAsync(PackageRuntimeService.LockKey(fetch.EnrollmentId, fetch.PackageItemId), async _ =>
        {
            var state = await _store.FindFreshAsync(fetch.EnrollmentId, fetch.PackageItemId, cancellationToken);
            var session = state?.Cmi5?.Sessions.FirstOrDefault(item => item.SessionId == fetch.SessionId);

            if (session is null || !string.Equals(state.UserId, fetch.UserId, StringComparison.Ordinal))
            {
                result = new Cmi5FetchResult(null, "3", "The session of the fetch URL is not known.");

                return (string)null;
            }

            if (session.FetchedUtc is not null)
            {
                result = new Cmi5FetchResult(null, "1", "The auth token of this session was already fetched.");

                return null;
            }

            if (session.Ended || session.ExpiresUtc <= _clock.UtcNow)
            {
                result = new Cmi5FetchResult(null, "2", "The session has ended.");

                return null;
            }

            session.FetchedUtc = _clock.UtcNow;
            await _store.UpdateAsync(state, cancellationToken);

            var scope = new XapiLaunchScope
            {
                Kind = Cmi5Vocabulary.LaunchKind,
                SessionId = session.SessionId,
                Registration = state.Cmi5.Registration,
                ActivityId = session.ActivityId,
                ActorKey = fetch.ActorKey,
                ExpiresUtc = session.ExpiresUtc,
                ReadOnlyStateIds = [Cmi5Vocabulary.LaunchDataStateId],
                Properties =
                {
                    ["enrollmentId"] = fetch.EnrollmentId,
                    ["packageItemId"] = fetch.PackageItemId,
                    ["userId"] = fetch.UserId,
                    ["auId"] = session.AuId,
                },
            };

            result = new Cmi5FetchResult(_launchTokens.CreateBasicValue(scope), null, null);

            return null;
        }, () => PackageRuntimeErrors.Busy, cancellationToken);

        return busy is not null ? new Cmi5FetchResult(null, "3", "The session is busy. Try again.") : result;
    }

    internal string CreateFetchToken(Cmi5FetchToken token)
        => _fetchProtector.Protect(JsonSerializer.Serialize(token));

    internal Cmi5FetchToken ReadFetchToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 4096)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Cmi5FetchToken>(_fetchProtector.Unprotect(token));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }

    private string BuildLaunchUrl(Cmi5LaunchRequest request, Cmi5Au au, Cmi5Session session, JsonObject actor, string registration, string activityId, LearningPackagePart part, string enrollmentId, string itemId)
    {
        var fetchToken = CreateFetchToken(new Cmi5FetchToken
        {
            SessionId = session.SessionId,
            EnrollmentId = enrollmentId,
            PackageItemId = itemId,
            UserId = XapiAgents.Text(actor["account"]?["name"]),
            ActorKey = XapiAgents.Key(actor),
            ExpiresUtc = session.ExpiresUtc,
        });

        var parameters = new StringBuilder();

        void Add(string name, string value)
        {
            parameters.Append(parameters.Length == 0 ? string.Empty : "&").Append(name).Append('=').Append(Uri.EscapeDataString(value));
        }

        Add("endpoint", request.EndpointUrl);
        Add("fetch", request.FetchUrlBase + Uri.EscapeDataString(fetchToken));
        Add("registration", registration);
        Add("activityId", activityId);
        Add("actor", actor.ToJsonString());

        if (au.IsAbsolute)
        {
            return au.Url + (au.Url.Contains('?', StringComparison.Ordinal) ? "&" : "?") + parameters;
        }

        // A file of the package runs in the sandbox, opened by a run token of this session.
        var runToken = _runTokens.Create(new PackageLaunch(session.SessionId, enrollmentId, itemId, part.PackageFileId, XapiAgents.Text(actor["account"]?["name"])));
        var url = request.RunPathBase + runToken + "/" + au.Path + "?";

        if (!string.IsNullOrEmpty(au.Query))
        {
            url += au.Query + "&";
        }

        return url + parameters;
    }

    private async Task WriteLaunchDataAsync(JsonObject actor, string actorKey, string registration, Cmi5Au au, Cmi5Session session, string activityId, string returnUrl, DateTime now, CancellationToken cancellationToken)
    {
        var data = new JsonObject
        {
            ["contextTemplate"] = new JsonObject
            {
                ["contextActivities"] = new JsonObject
                {
                    ["grouping"] = new JsonArray(new JsonObject { ["objectType"] = "Activity", ["id"] = au.Id }),
                },
                ["extensions"] = new JsonObject { [Cmi5Vocabulary.Extensions.SessionId] = session.SessionId },
            },
            ["launchMode"] = session.LaunchMode,
            ["moveOn"] = au.MoveOn,
        };

        if (!string.IsNullOrEmpty(au.LaunchParameters))
        {
            data["launchParameters"] = au.LaunchParameters;
        }

        if (au.MasteryScore is { } mastery)
        {
            data["masteryScore"] = mastery;
        }

        if (!string.IsNullOrEmpty(returnUrl))
        {
            data["returnURL"] = returnUrl;
        }

        if (!string.IsNullOrEmpty(au.EntitlementKey))
        {
            data["entitlementKey"] = new JsonObject { ["courseStructure"] = au.EntitlementKey };
        }

        await _documents.SaveAsync(
            new XapiDocumentScope(XapiDocumentKinds.State, activityId, actorKey, registration),
            Cmi5Vocabulary.LaunchDataStateId,
            actor.ToJsonString(),
            "application/json",
            JsonSerializer.SerializeToUtf8Bytes(data),
            now,
            cancellationToken);
    }

    private async Task WritePreferencesAsync(JsonObject actor, string actorKey, string language, DateTime now, CancellationToken cancellationToken)
    {
        var scope = new XapiDocumentScope(XapiDocumentKinds.AgentProfile, null, actorKey, null);

        if (await _documents.FindAsync(scope, Cmi5Vocabulary.LearnerPreferencesProfileId, cancellationToken) is not null)
        {
            return;
        }

        var preferences = new JsonObject
        {
            ["languagePreference"] = string.IsNullOrEmpty(language) ? "en-US" : language,
            ["audioPreference"] = "on",
        };

        await _documents.SaveAsync(scope, Cmi5Vocabulary.LearnerPreferencesProfileId, actor.ToJsonString(), "application/json", JsonSerializer.SerializeToUtf8Bytes(preferences), now, cancellationToken);
    }

    private static JsonObject Launched(JsonObject actor, string registration, Cmi5Au au, Cmi5Session session, string activityId)
    {
        var extensions = new JsonObject
        {
            [Cmi5Vocabulary.Extensions.SessionId] = session.SessionId,
            [Cmi5Vocabulary.Extensions.LaunchMode] = session.LaunchMode,
            [Cmi5Vocabulary.Extensions.LaunchUrl] = au.Url,
            [Cmi5Vocabulary.Extensions.MoveOn] = au.MoveOn,
        };

        if (!string.IsNullOrEmpty(au.LaunchParameters))
        {
            extensions[Cmi5Vocabulary.Extensions.LaunchParameters] = au.LaunchParameters;
        }

        return Statement(actor, Cmi5Vocabulary.Verbs.Launched, "Launched", Activity(activityId, au.Title), registration, au.Id, extensions, session.StartedUtc);
    }

    internal static JsonObject Abandoned(JsonObject actor, string registration, Cmi5Session session, DateTime now)
    {
        var statement = Statement(actor, Cmi5Vocabulary.Verbs.Abandoned, "Abandoned", Activity(session.ActivityId, null), registration, session.AuId, new JsonObject { [Cmi5Vocabulary.Extensions.SessionId] = session.SessionId }, now);

        statement["result"] = new JsonObject { ["duration"] = Duration(now - session.StartedUtc) };

        return statement;
    }

    internal static JsonObject Statement(JsonObject actor, string verb, string verbDisplay, JsonObject value, string registration, string publisherId, JsonObject extensions, DateTime timestamp)
        => new()
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["actor"] = actor.DeepClone(),
            ["verb"] = new JsonObject
            {
                ["id"] = verb,
                ["display"] = new JsonObject { ["en-US"] = verbDisplay },
            },
            ["object"] = value,
            ["context"] = new JsonObject
            {
                ["registration"] = registration,
                ["contextActivities"] = new JsonObject
                {
                    ["category"] = new JsonArray(new JsonObject { ["objectType"] = "Activity", ["id"] = Cmi5Vocabulary.Categories.Cmi5 }),
                    ["grouping"] = new JsonArray(new JsonObject { ["objectType"] = "Activity", ["id"] = publisherId }),
                },
                ["extensions"] = extensions,
            },
            ["timestamp"] = XapiFormats.FormatTimestamp(timestamp),
        };

    internal static JsonObject Activity(string id, string title)
    {
        var activity = new JsonObject
        {
            ["objectType"] = "Activity",
            ["id"] = id,
        };

        if (!string.IsNullOrEmpty(title))
        {
            activity["definition"] = new JsonObject { ["name"] = new JsonObject { ["en-US"] = title } };
        }

        return activity;
    }

    internal static string Duration(TimeSpan span)
    {
        var value = span < TimeSpan.Zero ? TimeSpan.Zero : span;

        return string.Create(CultureInfo.InvariantCulture, $"PT{(long)value.TotalHours}H{value.Minutes}M{value.Seconds}.{value.Milliseconds / 10:00}S");
    }
}

/// <summary>
/// What a fetch URL is bound to.
/// </summary>
internal sealed class Cmi5FetchToken
{
    public string SessionId { get; set; }

    public string EnrollmentId { get; set; }

    public string PackageItemId { get; set; }

    public string UserId { get; set; }

    public string ActorKey { get; set; }

    public DateTime ExpiresUtc { get; set; }
}
