using System.Net;
using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.AI.Core.Telemetry;
using CrestApps.OrchardCore.AI.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Models;
using Fluid;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;
using ActivityKind = System.Diagnostics.ActivityKind;
using TagList = System.Diagnostics.TagList;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Posts JSON to another system, signed so the receiver can check it came from this site and was not replayed: it sends
/// through the Webhooks building block's <see cref="IWebhookSender"/>, so it has the same headers, signature and egress
/// guard as every other webhook. Failed deliveries are retried a few times before the task gives up.
/// </summary>
public sealed class SendSignedWebhookTask : TaskActivity<SendSignedWebhookTask>
{
    /// <summary>
    /// The purpose the signing secret is protected with.
    /// </summary>
    public const string ProtectorPurpose = "CrestApps.OrchardCore.AI.SendSignedWebhookTask";

    private static readonly TimeSpan[] _retryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(27)];

    // The area of the retries on crestapps.retry.attempts.
    private const string RetryArea = "ai.signed_webhook";

    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IWebhookSender _webhookSender;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendSignedWebhookTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="webhookSender">Makes each signed attempt.</param>
    /// <param name="dataProtectionProvider">The data protection provider the secret is kept with.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SendSignedWebhookTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IWebhookSender webhookSender,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SendSignedWebhookTask> logger,
        IStringLocalizer<SendSignedWebhookTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _webhookSender = webhookSender;
        _dataProtectionProvider = dataProtectionProvider;
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

        // The URL, the host and the event name are free text, so they never become tags.
        using var telemetry = TelemetryScope.Start(
            AIModuleTelemetry.ActivitySource,
            "crestapps.ai.webhook.deliver",
            AIModuleTelemetry.WebhookDeliveryDuration);

        telemetry.SetSpanTag("crestapps.webhook.delivery.id", deliveryId);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            result["Error"] = "The address is not an http or https URL.";
            RecordDelivery(telemetry, "invalid_url", statusCode: null);

            return Outcome("Failed");
        }

        if (string.IsNullOrEmpty(secret))
        {
            // Unsigned, the receiver cannot trust it.
            result["Error"] = "The signing secret is missing or can't be read. Enter it again.";
            RecordDelivery(telemetry, "missing_secret", statusCode: null);

            return Outcome("Failed");
        }

        body = string.IsNullOrWhiteSpace(body) ? "{}" : body;
        var attempts = Math.Clamp(MaxAttempts, 1, 5);
        var request = new WebhookSendRequest
        {
            Url = uri.AbsoluteUri,
            Secret = secret,
            EventId = deliveryId,
            EventName = eventName,
            Body = body,
        };

        var failureReason = "exhausted";
        int? lastStatusCode = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            result["Attempts"] = attempt;

            var sent = await _webhookSender.SendAsync(request);
            result["StatusCode"] = sent.StatusCode;
            lastStatusCode = sent.StatusCode;

            if (sent.Succeeded)
            {
                result["Error"] = null;
                telemetry.SetSpanTag("crestapps.webhook.attempts", attempt);
                RecordDelivery(telemetry, failureReason: null, sent.StatusCode);

                return Outcome("Delivered");
            }

            result["Error"] = sent.Error;

            // The egress guard refused a host on the site's own network, or the receiver refused it for good: sending it
            // again is refused again.
            if (sent.EgressRefused || (sent.StatusCode is { } statusCode && !IsWorthRetrying((HttpStatusCode)statusCode)))
            {
                failureReason = sent.EgressRefused ? "egress_refused" : "rejected";

                break;
            }

            if (attempt < attempts)
            {
                AIModuleLog.SignedWebhookRetrying(_logger, deliveryId, sent.StatusCode, attempt + 1, attempts);
                RetryTelemetry.RecordRetry(RetryArea, GetRetryReason(sent.StatusCode), attempt + 1);

                await Task.Delay(_retryDelays[attempt - 1]);
            }
        }

        telemetry.SetSpanTag("crestapps.webhook.attempts", result["Attempts"]);
        RecordDelivery(telemetry, failureReason, lastStatusCode);

        _logger.LogWarning("The signed webhook '{DeliveryId}' to '{Host}' failed after {Attempts} attempts: {Error}", deliveryId, uri.Host, result["Attempts"], result["Error"]);

        return Outcome("Failed");
    }

    /// <summary>
    /// Whether a failed response may succeed if sent again: a timeout, too many requests, or a server error.
    /// </summary>
    /// <param name="statusCode">The response's status code.</param>
    internal static bool IsWorthRetrying(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    /// <summary>
    /// The bounded reason a failed attempt is tried again, for <c>crestapps.retry.attempts</c>.
    /// </summary>
    /// <param name="statusCode">The status code of the failed attempt, or <see langword="null"/> when no response came.</param>
    internal static string GetRetryReason(int? statusCode)
        => statusCode switch
        {
            null => "network",
            (int)HttpStatusCode.RequestTimeout => CrestAppsOrchardCoreTelemetry.OutcomeTimeout,
            (int)HttpStatusCode.TooManyRequests => "status_429",
            >= 500 => "status_5xx",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };

    /// <summary>
    /// Records how a delivery ended: delivered when <paramref name="failureReason"/> is <see langword="null"/>, failed
    /// for that bounded reason otherwise.
    /// </summary>
    /// <param name="telemetry">The delivery's scope.</param>
    /// <param name="failureReason">Why the delivery failed, or <see langword="null"/> when it was delivered.</param>
    /// <param name="statusCode">The status code of the last response, or <see langword="null"/>.</param>
    private static void RecordDelivery(TelemetryScope telemetry, string failureReason, int? statusCode)
    {
        var statusClass = ExternalCallTelemetry.GetStatusClass(statusCode);
        telemetry.AddMetricTag(CrestAppsOrchardCoreTelemetry.StatusClassTag, statusClass);

        if (statusCode is { } code)
        {
            telemetry.SetSpanTag("http.response.status_code", code);
        }

        if (failureReason is null)
        {
            telemetry.Succeed();
        }
        else
        {
            telemetry.Fail(failureReason);
        }
    }

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
