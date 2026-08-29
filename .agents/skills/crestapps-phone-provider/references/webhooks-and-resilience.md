# Webhooks, event ingress, and resilience

Provider events (call-state changes, recordings, inbound SMS/DTMF) arrive as **webhooks or streams**. They
must be authenticated at the provider's own endpoint, made durable, and normalized into the internal pipeline
before anything reaches the UI. Copy `TelnyxWebhookEndpoint` and `TelnyxWebhookInboxHandler`.

## Outbound HTTP resilience

Register a named `HttpClient` with the standard resilience handler
(`src/Modules/CrestApps.OrchardCore.Telnyx/Startup.cs`):

```csharp
services.AddHttpClient(TwilioConstants.ProviderTechnicalName)
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(2);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.DisableForUnsafeHttpMethods();   // NEVER auto-replay POST/PUT/DELETE
        options.CircuitBreaker.FailureRatio = 0.1;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.MinimumThroughput = 100;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
    });
```

`DisableForUnsafeHttpMethods()` is critical: the client carries call-origination POSTs and credential
mutations; a replayed POST after a lost response would place a second call or mint a second credential. Safe
GETs still retry. For non-idempotent POSTs, add the provider's own idempotency/command id so the **provider**
de-duplicates a retry you issue deliberately.

## The webhook endpoint (minimal-API pattern)

`TelnyxWebhookEndpoint` maps `POST <webhook path>` with `.AllowAnonymous().DisableAntiforgery()` and a
`RequestSizeLimitAttribute`. The handler runs these gates **in order** — replicate all of them:

1. **Feature enabled?** Read settings; if the provider is disabled, `404`.
2. **Size limit** — reject `> MaximumRequestBodySizeBytes` with `413`.
3. **Concurrency lease** — `IProviderWebhookIngressLimiter.AcquireConcurrencyAsync`; if not acquired,
   set `Retry-After` and return `429`. (Backpressure, not a delivery failure.)
4. **Read the raw body** with a bounded reader (`RequestBodyReader.ReadAsync`, honoring the size cap).
5. **Authenticate the signature.** Unprotect the stored webhook public key (data protector), read the
   provider's signature + timestamp headers, and verify. Telnyx uses **Ed25519** over
   `"{timestamp}|{rawBody}"` (`TelnyxWebhookSignatureValidator`). Use your provider's scheme (Twilio uses an
   HMAC-SHA1 `X-Twilio-Signature` over the URL + sorted params). No key configured → `401`; key can't be
   unprotected → `503`; bad signature → `401`.
6. **Parse** the validated payload into a typed event; unparseable → `400` (and record a health-metric miss).
7. **Replay / freshness window.** Resolve the signed timestamp and reject stale or far-future events
   (`ingressLimiter.IsFresh(...)`, default window −900s..+120s) → `400`.
8. **Rate lease** — `AcquireRateAsync(providerName)`; if not acquired, `Retry-After` + `429`.
9. **Durable inbox.** Serialize the event and `inbox.AcceptAsync(new ProviderWebhookInboxDelivery {
   ProviderName, DeliveryId, HandlerName, Payload })`. A stable `DeliveryId` makes acceptance **idempotent**
   across provider redeliveries. If `Busy`, return `503`. Then `inbox.DispatchAsync(messageId)` for immediate
   processing, catching `ConcurrencyException` (a concurrent worker won — the background inbox finishes it).
10. Return `200 { accepted = true }`.

Time-critical events may take a **fast path** before the durable write (Telnyx issues the voicemail
`record_start` on the greeting-ended event immediately, because the caller is waiting for the beep — a
duplicate on redelivery is harmless because the provider rejects a second `record_start`). Use sparingly and
only for idempotent, latency-sensitive actions.

## The durable handler — `IProviderWebhookInboxHandler`

```csharp
public sealed class TwilioWebhookInboxHandler : IProviderWebhookInboxHandler
{
    public const string HandlerTechnicalName = "Twilio.CallEvents";
    public string TechnicalName => HandlerTechnicalName;
    // Values: Unspecified, NaturallyIdempotent, GuardedByDurableStore. Telnyx uses GuardedByDurableStore
    // (the durable inbox's DeliveryId de-dupes redeliveries); the handler must still be safe to run twice.
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.GuardedByDurableStore;
    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        // Deserialize -> normalize to ProviderVoiceEvent -> submit via IProviderVoiceEventSink /
        // IInboundVoiceEventSink. This runs both on immediate dispatch AND on background retry, so it MUST be
        // safe to run twice. Let ConcurrencyException propagate so the inbox retries in a fresh scope.
    }
}
```

Register under the CC Voice startup: `.AddScoped<IProviderWebhookInboxHandler, TwilioWebhookInboxHandler>()`.

**Never swallow `ConcurrencyException`** in the handler — swallowing it drops the work; propagating it lets
the durable inbox retry (this is why some voicemails weren't reaching the soft phone — see the memory note).

## Normalizing to the internal pipeline

Providers never touch Contact Center persistence models directly. Convert native events into
`ProviderVoiceEvent` and submit through `IProviderVoiceEventSink` (state transitions) or
`IInboundVoiceEventSink` (route a new inbound call). The soft phone is updated only downstream of this.

## Streams instead of webhooks

If the provider pushes events over a persistent stream/WebSocket (rather than HTTP webhooks), run a listener
that authenticates the stream, commits deliveries to the same durable inbox, and normalizes to
`ProviderVoiceEvent`. The bidirectional media path (`IContactCenterVoiceMediaProvider`) itself is a WebSocket
the provider dials back to — map that endpoint under the `VoiceMedia` feature and depend on the WebSockets
module (Telnyx `MediaStreamPath`).

## Recording ingest (if the provider records)

Telnyx's pattern (`TelnyxRecordingIngest*`): the `call.recording.saved` webhook enqueues a **durable job**
(`client_state` correlates it back to the interaction); a background sweep downloads each recording into the
encrypted media store with exponential backoff, capped attempts, and dead-lettering, then deletes it from the
provider. Reuse this shape (job store + index + `IBackgroundTask` sweep + `IRecordingMediaStore`) rather than
downloading inline in the webhook.

## Secrets & logging

- Store API keys and webhook keys **data-protected** (`IDataProtectionProvider.CreateProtector(name)`); never
  persist or log them in plaintext.
- Sanitize anything from the provider before logging (`value.SanitizeLogValue()`), including response bodies
  and call ids.
- Security rejections (`401`) and backpressure (`429`/`503`) are **not** counted as delivery failures in
  health metrics — only validated-but-unprocessable payloads are.
