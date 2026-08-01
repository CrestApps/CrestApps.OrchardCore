using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Telephony.ProviderContracts;

/// <summary>
/// Drives the production Asterisk REST Interface client against recorded provider responses, recording every request the
/// client issued so it can be checked against the declarations the Asterisk project publishes for the pinned release.
/// </summary>
internal sealed class AriRestContractHarness
{
    private const string BaseUrl = "http://asterisk.contract.invalid/ari/";

    private readonly AsteriskContractCassettes _cassettes;
    private readonly List<RecordedResponse> _recordedResponses = [];

    private string _currentOperation = "unattributed";

    /// <summary>
    /// Initializes a new instance of the <see cref="AriRestContractHarness"/> class.
    /// </summary>
    /// <param name="cassettes">The loaded provider contract cassette set.</param>
    public AriRestContractHarness(AsteriskContractCassettes cassettes)
    {
        ArgumentNullException.ThrowIfNull(cassettes);

        _cassettes = cassettes;
        LoadRecordedResponses();
    }

    /// <summary>
    /// Gets every request the client issued while the harness was replaying responses.
    /// </summary>
    public List<AriIssuedRequest> IssuedRequests { get; } = [];

    /// <summary>
    /// Gets the requests the client issued that no recorded response covers.
    /// </summary>
    public List<string> UnrecordedRequests { get; } = [];

    /// <summary>
    /// Gets the status codes the harness replayed.
    /// </summary>
    public List<HttpStatusCode> IssuedStatusCodes { get; } = [];

    /// <summary>
    /// Gets the client operations the harness exercised.
    /// </summary>
    public List<string> ExercisedOperations { get; } = [];

    /// <summary>
    /// Creates an originate request that carries every optional field the client projects onto the query string.
    /// </summary>
    /// <returns>The originate request.</returns>
    public static AsteriskAriOriginateRequest CreateOriginateRequest()
    {
        return new AsteriskAriOriginateRequest
        {
            Endpoint = "PJSIP/5551000",
            CallerId = "+15550001",
            ChannelId = "1699887600.42",
            App = "crestapps-telephony",
            AppArgs = ["CRESTAPPS_ORIGINATED", "contact-center"],
            Variables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CRESTAPPS_ORIGINATED"] = "1",
                ["CRESTAPPS_INTERACTION_ID"] = "01HTQ0J6P2WK3Z9Y8B7C6D5E4F",
            },
            TimeoutSeconds = 30,
        };
    }

    /// <summary>
    /// Creates a production client bound to this harness.
    /// </summary>
    /// <returns>The client.</returns>
    public AsteriskAriClient CreateClient()
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var settings = new AsteriskSettings
        {
            IsEnabled = true,
            BaseUrl = BaseUrl,
            UserName = "ari-user",
            Password = dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName).Protect("secret"),
            ApplicationName = "crestapps-telephony",
            TimeoutSeconds = 30,
        };

        var shellSettings = new ShellSettings { Name = "Default" };
        var options = Options.Create(new DefaultAsteriskOptions());
        var gate = new AsteriskAriApplicationGate(
            new AsteriskAriApplicationOwnershipRegistry(NullLogger<AsteriskAriApplicationOwnershipRegistry>.Instance),
            shellSettings,
            options);

        return new AsteriskAriClient(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            new StubHttpClientFactory(new StubHttpMessageHandler(Respond)),
            options,
            shellSettings,
            gate,
            NullLogger<AsteriskAriClient>.Instance);
    }

    /// <summary>
    /// Invokes every operation the Asterisk REST Interface client publishes.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once every operation has been exercised.</returns>
    public async Task ExerciseEveryOperationAsync(CancellationToken cancellationToken)
    {
        var client = CreateClient();

        await ExerciseAsync(nameof(IAsteriskAriClient.OriginateAsync), () => client.OriginateAsync(CreateOriginateRequest(), cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.CreateBridgeAsync), () => client.CreateBridgeAsync("conf-01HTQ0", "mixing", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.AddChannelToBridgeAsync), () => client.AddChannelToBridgeAsync("conf-01HTQ0", "1699887600.42", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.RemoveChannelFromBridgeAsync), () => client.RemoveChannelFromBridgeAsync("conf-01HTQ0", "1699887600.42", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.AnswerAsync), () => client.AnswerAsync("1699887600.42", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.HangupAsync), () => client.HangupAsync("1699887600.42", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.ChannelExistsAsync), () => client.ChannelExistsAsync("1699887600.42", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.HoldChannelAsync), () => client.HoldChannelAsync("1699887600.42", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.UnholdChannelAsync), () => client.UnholdChannelAsync("1699887600.42", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.DestroyBridgeAsync), () => client.DestroyBridgeAsync("conf-01HTQ0", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.StartBridgeRecordingAsync), () => client.StartBridgeRecordingAsync("conf-01HTQ0", "rec-01HTQ0", "wav", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.PauseBridgeRecordingAsync), () => client.PauseBridgeRecordingAsync("rec-01HTQ0", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.UnpauseBridgeRecordingAsync), () => client.UnpauseBridgeRecordingAsync("rec-01HTQ0", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.StopBridgeRecordingAsync), () => client.StopBridgeRecordingAsync("rec-01HTQ0", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.DownloadStoredRecordingAsync), () => client.DownloadStoredRecordingAsync("rec-01HTQ0", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.DeleteStoredRecordingAsync), () => client.DeleteStoredRecordingAsync("rec-01HTQ0", cancellationToken));
        await ExerciseAsync(nameof(IAsteriskAriClient.SnoopChannelAsync), () => client.SnoopChannelAsync("1699887600.42", "both", "none", "snoop-01HTQ0", cancellationToken));
    }

    private async Task ExerciseAsync(string operation, Func<Task> invoke)
    {
        _currentOperation = operation;
        ExercisedOperations.Add(operation);

        await invoke();
    }

    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var path = request.RequestUri.IsAbsoluteUri
            ? request.RequestUri.AbsolutePath
            : request.RequestUri.OriginalString;
        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        var query = string.Empty;

        if (queryIndex >= 0)
        {
            query = path.Substring(queryIndex);
            path = path.Substring(0, queryIndex);
        }
        else if (request.RequestUri.IsAbsoluteUri)
        {
            query = request.RequestUri.Query;
        }

        path = NormalizePath(path);
        var issued = new AriIssuedRequest(_currentOperation, request.Method.Method, path);

        foreach (var parameter in QueryHelpers.ParseQuery(query))
        {
            issued.QueryParameterNames.Add(parameter.Key);
        }

        IssuedRequests.Add(issued);

        if (!TryFindRecordedResponse(request.Method.Method, path, out var recorded))
        {
            UnrecordedRequests.Add($"{request.Method.Method} {path}");
            IssuedStatusCodes.Add(HttpStatusCode.NotImplemented);

            return new HttpResponseMessage(HttpStatusCode.NotImplemented)
            {
                Content = new StringContent(string.Empty),
            };
        }

        IssuedStatusCodes.Add(recorded.StatusCode);

        return new HttpResponseMessage(recorded.StatusCode)
        {
            Content = new StringContent(recorded.Body ?? string.Empty, Encoding.UTF8, "application/json"),
        };
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.TrimStart('/');

        if (normalized.StartsWith("ari/", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(4);
        }

        return normalized;
    }

    private bool TryFindRecordedResponse(string httpMethod, string path, out RecordedResponse recorded)
    {
        var requestSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var candidate in _recordedResponses)
        {
            if (!string.Equals(candidate.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var templateSegments = candidate.PathTemplate.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (templateSegments.Length != requestSegments.Length)
            {
                continue;
            }

            var matches = true;

            for (var i = 0; i < templateSegments.Length; i++)
            {
                var templateSegment = templateSegments[i];

                if (templateSegment.StartsWith('{') && templateSegment.EndsWith('}'))
                {
                    continue;
                }

                if (!string.Equals(templateSegment, requestSegments[i], StringComparison.Ordinal))
                {
                    matches = false;

                    break;
                }
            }

            if (matches)
            {
                recorded = candidate;

                return true;
            }
        }

        recorded = null;

        return false;
    }

    private void LoadRecordedResponses()
    {
        var path = Path.Combine(_cassettes.DirectoryPath, "rest", "responses.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        foreach (var recorded in document.RootElement.GetProperty("responses").EnumerateArray())
        {
            string body = null;

            if (recorded.TryGetProperty("body", out var bodyElement))
            {
                body = bodyElement.GetRawText();
            }
            else if (recorded.TryGetProperty("textBody", out var textElement))
            {
                body = textElement.GetString();
            }

            _recordedResponses.Add(new RecordedResponse(
                recorded.GetProperty("httpMethod").GetString(),
                recorded.GetProperty("pathTemplate").GetString(),
                (HttpStatusCode)recorded.GetProperty("statusCode").GetInt32(),
                body));
        }
    }

    private sealed record RecordedResponse(
        string HttpMethod,
        string PathTemplate,
        HttpStatusCode StatusCode,
        string Body);
}
