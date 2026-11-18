using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Services;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Packages.Controllers;

/// <summary>
/// The learner's side of a learning package. <c>Launch</c> and <c>Runtime</c> are called by the player page with the
/// learner's session and antiforgery token. <c>Run</c> serves the files of the package to the sandboxed frame, which
/// has no session: only the launch token in the path opens them, and every response carries a sandbox
/// Content-Security-Policy without <c>allow-same-origin</c>, so even a package file opened outside the frame runs on an
/// opaque origin that cannot read the learner's cookies, the player page or the site's endpoints.
/// </summary>
public sealed class PackageRunController : Controller
{
    /// <summary>
    /// The sandbox every package file runs in: scripts, forms and popups, and no same origin.
    /// </summary>
    public const string SandboxPolicy = "sandbox allow-scripts allow-forms allow-popups";

    /// <summary>
    /// The largest runtime call: a thousand values of at most 4,096 characters.
    /// </summary>
    internal const long MaxRuntimeRequestBytes = 8L * 1024 * 1024;

    /// <summary>
    /// The largest HTML page the run-time script is put into.
    /// </summary>
    internal const long MaxHtmlBytes = 32L * 1024 * 1024;

    private readonly PackageRuntimeService _runtime;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageRunController"/> class.
    /// </summary>
    public PackageRunController(
        PackageRuntimeService runtime,
        IAuthorizationService authorizationService,
        IStringLocalizer<PackageRunController> stringLocalizer)
    {
        _runtime = runtime;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Serves a file of the package of a launch token to the package frame.
    /// </summary>
    /// <param name="token">The launch token.</param>
    /// <param name="path">The path of the file in the package.</param>
    /// <returns>The file, or not found.</returns>
    [HttpGet("learning/packages/run/{token}/{**path}", Name = "LearningPackageRun")]
    public async Task<IActionResult> Run(string token, string path)
    {
        SetSandboxHeaders(Response);

        var file = await _runtime.OpenFileAsync(token, path, HttpContext.RequestAborted);

        if (file is null)
        {
            return NotFound();
        }

        Response.RegisterForDisposeAsync(file);

        if (!file.IsHtml)
        {
            Response.Headers.CacheControl = "private, max-age=3600";

            return File(file.Content, file.ContentType);
        }

        // A cmi5 AU talks to the LRS itself: its pages are served as they are, never cached.
        if (string.Equals(file.Standard, PackagesConstants.Standards.Cmi5, StringComparison.Ordinal))
        {
            Response.Headers.CacheControl = "no-store";

            return File(file.Content, file.ContentType);
        }

        // A page starts with the learner's data, so it is never cached.
        Response.Headers.CacheControl = "no-store";

        // The entry says how large the page is: a page over the limit is never read into memory.
        if (file.Length > MaxHtmlBytes)
        {
            return NotFound();
        }

        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, HttpContext.RequestAborted);

        if (buffer.Length > MaxHtmlBytes)
        {
            return NotFound();
        }

        var playerOrigin = $"{Request.Scheme}://{Request.Host}";
        var page = PackageHtmlInjector.Inject(buffer.ToArray(), PackageShimScript.Build(file.Launch, file.State, playerOrigin, file.Standard));

        return File(page, file.ContentType);
    }

    /// <summary>
    /// Launches a package for the signed-in learner.
    /// </summary>
    /// <param name="itemId">The package item.</param>
    /// <returns>The launch URL of the frame and the runtime URL of the player, or why it was refused.</returns>
    [HttpPost("learning/packages/{itemId}/launch", Name = "LearningPackageLaunch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Launch(string itemId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.AccessLearning))
        {
            return Forbid();
        }

        var result = await _runtime.LaunchAsync(userId, User.Identity?.Name, itemId, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return Json(new { launched = false, message = ErrorText(result.Error) });
        }

        var launchUrl = Url.RouteUrl("LearningPackageRun", new { token = result.Token, path = result.Part.LaunchPath });

        if (!string.IsNullOrEmpty(result.Part.LaunchQuery))
        {
            launchUrl += "?" + result.Part.LaunchQuery;
        }

        return Json(new
        {
            launched = true,
            token = result.Token,
            launchId = result.Launch.LaunchId,
            protocol = PackageShimScript.ProtocolFor(result.Part.Standard),
            launchUrl,
            runtimeUrl = Url.RouteUrl("LearningPackageRuntime", new { enrollmentId = result.Launch.EnrollmentId, itemId = result.Launch.PackageItemId }),
            review = result.Review,
        });
    }

    /// <summary>
    /// Takes what the content of a launch set, committed or finished, as the player page relays it.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="itemId">The package item.</param>
    /// <param name="token">The launch token.</param>
    /// <param name="payload">The values, as JSON: <c>{ "changes": [[element, value], ...], "commit": bool, "finish": bool }</c>.</param>
    /// <returns>What was taken.</returns>
    [HttpPost("learning/packages/runtime/{enrollmentId}/{itemId}", Name = "LearningPackageRuntime")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRuntimeRequestBytes)]
    [RequestFormLimits(ValueLengthLimit = (int)MaxRuntimeRequestBytes)]
    public async Task<IActionResult> Runtime(string enrollmentId, string itemId, string token, string payload)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!TryReadPayload(payload, out var changes, out var commit, out var finish, out var sequence))
        {
            return BadRequest();
        }

        var result = await _runtime.ApplyAsync(new PackageRuntimeRequest
        {
            UserId = userId,
            EnrollmentId = enrollmentId,
            PackageItemId = itemId,
            Token = token,
            Changes = changes,
            Commit = commit,
            Finish = finish,
            Sequence = sequence,
        }, HttpContext.RequestAborted);

        return Json(new
        {
            accepted = result.Succeeded,
            retry = result.Error == PackageRuntimeErrors.Busy || result.Progress?.RefusedReason == LearningRefusals.Busy,
            message = result.Succeeded ? null : ErrorText(result.Error),
            refused = result.Refused.Select(refusal => new { index = refusal.Index, element = refusal.Element, code = refusal.Code }),
            review = result.Review,
            status = PackageStateSummary.Of(result.State).Status,
            itemCompleted = result.Progress?.ItemCompleted ?? false,
            courseCompleted = result.Progress?.CourseCompleted ?? false,
        });
    }

    internal static void SetSandboxHeaders(HttpResponse response)
    {
        response.Headers.ContentSecurityPolicy = SandboxPolicy;
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";

        // The frame has an opaque origin, so its own requests for the package's scripts, fonts and data files are cross
        // origin. The token in the path is what opens them, never a cookie, so sharing them adds nothing a token holder
        // could not already read.
        response.Headers.AccessControlAllowOrigin = "*";
        response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    }

    internal static bool TryReadPayload(string payload, out List<PackageRuntimeChange> changes, out bool commit, out bool finish, out long? sequence)
    {
        changes = [];
        commit = false;
        finish = false;
        sequence = null;

        if (string.IsNullOrEmpty(payload))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 4 });
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            commit = root.TryGetProperty("commit", out var commitValue) && commitValue.ValueKind == JsonValueKind.True;
            finish = root.TryGetProperty("finish", out var finishValue) && finishValue.ValueKind == JsonValueKind.True;

            if (root.TryGetProperty("sequence", out var sequenceValue))
            {
                if (sequenceValue.ValueKind != JsonValueKind.Number || !sequenceValue.TryGetInt64(out var number) || number < 1)
                {
                    return false;
                }

                sequence = number;
            }

            if (root.TryGetProperty("changes", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var pair in list.EnumerateArray())
                {
                    if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2 ||
                        pair[0].ValueKind != JsonValueKind.String || pair[1].ValueKind != JsonValueKind.String)
                    {
                        return false;
                    }

                    changes.Add(new PackageRuntimeChange(pair[0].GetString(), pair[1].GetString()));
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string ErrorText(string error)
        => error switch
        {
            PackageRuntimeErrors.NotEnrolled => S["You need an enrollment in the course to open this package."],
            PackageRuntimeErrors.NotActive => S["Your enrollment is not active, so the package cannot be opened."],
            PackageRuntimeErrors.AccessEnded => S["Your access to this course has ended."],
            PackageRuntimeErrors.NotAvailableYet => S["This package is not open to you yet."],
            PackageRuntimeErrors.NoPackageFile => S["No package was imported into this item yet."],
            PackageRuntimeErrors.NotAPackage => S["This is not a learning package of your course."],
            PackageRuntimeErrors.Superseded => S["This package was opened again in another window, or its session ended. Launch it again to go on."],
            PackageRuntimeErrors.InvalidToken => S["This session of the package has expired. Launch it again to go on."],
            PackageRuntimeErrors.Busy => S["Your progress is being saved. Try again in a moment."],
            PackageRuntimeErrors.StateTooLarge => S["The package stored more data than a package may keep, so this progress was not saved."],
            _ => S["Your progress in the package could not be saved."],
        };
}
