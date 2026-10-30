using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using CrestApps.Core;
using Fluid;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Posts JSON to another system, signed so the receiver can check it came from this site and was not replayed: the
/// same headers and signature as the commerce webhooks. Failed deliveries are retried a few times before the task
/// gives up.
/// </summary>
public sealed class SendSignedWebhookTask : TaskActivity<SendSignedWebhookTask>
{
    /// <summary>
    /// The name of the HTTP client the webhooks are sent with.
    /// </summary>
    public const string HttpClientName = "CrestApps.SignedWebhook";

    /// <summary>
    /// The purpose the signing secret is protected with.
    /// </summary>
    public const string ProtectorPurpose = "CrestApps.OrchardCore.AI.SendSignedWebhookTask";

    private static readonly TimeSpan[] _retryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(27)];

    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendSignedWebhookTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="dataProtectionProvider">The data protection provider the secret is kept with.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SendSignedWebhookTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        ILogger<SendSignedWebhookTask> logger,
        IStringLocalizer<SendSignedWebhookTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _httpClientFactory = httpClientFactory;
        _dataProtectionProvider = dataProtectionProvider;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Send Signed Webhook"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["HTTP"];

    /// <summary>
    /// Gets or sets a Liquid expression for the address to post to.
    /// </summary>
    public string Url
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a Liquid expression for the event name sent in <c>X-Webhook-Event</c>.
    /// </summary>
    public string EventName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid template of the JSON body.
    /// </summary>
    public string Body
    {
        get => GetProperty(() => "{{ Workflow.Input | json }}");
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the signing secret, protected with <see cref="ProtectorPurpose"/>.
    /// </summary>
    public string ProtectedSecret
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets how many times a delivery is tried, from 1 to 5.
    /// </summary>
    public int MaxAttempts
    {
        get => GetProperty(() => 3);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Delivered"]), new Outcome(S["Failed"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var url = (await RenderAsync(Url, workflowContext))?.Trim();
        var eventName = (await RenderAsync(EventName, workflowContext))?.Trim();
        var body = await RenderAsync(Body, workflowContext, NullEncoder.Default);
        var secret = UnprotectSecret();
        var deliveryId = UniqueId.GenerateId();

        var result = new Dictionary<string, object>
        {
            ["Id"] = deliveryId,
            ["Attempts"] = 0,
            ["StatusCode"] = null,
            ["Error"] = null,
        };

        workflowContext.Output["Webhook"] = result;
        workflowContext.LastResult = result;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            result["Error"] = "The address is not an http or https URL.";

            return Outcome("Failed");
        }

        if (string.IsNullOrEmpty(secret))
        {
            // Unsigned, the receiver cannot trust it.
            result["Error"] = "The signing secret is missing or can't be read. Enter it again.";

            return Outcome("Failed");
        }

        body = string.IsNullOrWhiteSpace(body) ? "{}" : body;
        var attempts = Math.Clamp(MaxAttempts, 1, 5);
        var client = _httpClientFactory.CreateClient(HttpClientName);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            result["Attempts"] = attempt;

            try
            {
                var timestamp = new DateTimeOffset(_clock.UtcNow).ToUnixTimeSeconds();
                using var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };

                request.Headers.Add("X-Webhook-Id", deliveryId);

                if (!string.IsNullOrEmpty(eventName))
                {
                    request.Headers.Add("X-Webhook-Event", eventName);
                }

                request.Headers.Add("X-Webhook-Timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
                request.Headers.Add("X-Webhook-Signature", Sign(secret, timestamp, body));
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CrestApps-Webhooks", "1.0"));

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                result["StatusCode"] = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    result["Error"] = null;

                    return Outcome("Delivered");
                }

                result["Error"] = $"{(int)response.StatusCode} {response.ReasonPhrase}";

                if (!IsWorthRetrying(response.StatusCode))
                {
                    break;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                result["Error"] = ex.Message;
            }

            if (attempt < attempts)
            {
                await Task.Delay(_retryDelays[attempt - 1]);
            }
        }

        _logger.LogWarning("The signed webhook '{DeliveryId}' to '{Host}' failed after {Attempts} attempts: {Error}", deliveryId, uri.Host, result["Attempts"], result["Error"]);

        return Outcome("Failed");
    }

    /// <summary>
    /// Returns the signature of a body at a time: <c>v1=</c> and the lowercase hex HMAC-SHA256 of
    /// <c>{timestamp}.{body}</c>, the same as the commerce webhooks.
    /// </summary>
    /// <param name="secret">The signing secret.</param>
    /// <param name="timestamp">When it was signed, in seconds since 1970.</param>
    /// <param name="body">The body.</param>
    public static string Sign(string secret, long timestamp, string body)
        => "v1=" + Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + "." + body)));

    /// <summary>
    /// Whether a failed response may succeed if sent again: a timeout, too many requests, or a server error.
    /// </summary>
    /// <param name="statusCode">The response's status code.</param>
    internal static bool IsWorthRetrying(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private string UnprotectSecret()
    {
        if (string.IsNullOrEmpty(ProtectedSecret))
        {
            return null;
        }

        try
        {
            return _dataProtectionProvider.CreateProtector(ProtectorPurpose).Unprotect(ProtectedSecret);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "The signing secret of a Send Signed Webhook task can't be read; it needs to be entered again.");

            return null;
        }
    }

    private async Task<string> RenderAsync(string template, WorkflowExecutionContext workflowContext, System.Text.Encodings.Web.TextEncoder encoder = null)
        => string.IsNullOrWhiteSpace(template)
            ? null
            : await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(template), workflowContext, encoder);
}
