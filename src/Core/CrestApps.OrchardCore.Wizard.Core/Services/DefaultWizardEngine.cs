using CrestApps.OrchardCore.Wizard.Handlers;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Wizard.Core.Services;

/// <summary>
/// The default <see cref="IWizardEngine"/>. It serializes completion for a session under a distributed lock,
/// reloads the authoritative session to avoid lost updates, validates data-collecting steps, and runs the
/// completing and completed handlers exactly once. The completing handlers and the status transition are
/// committed to the store while the lock is still held, so a concurrent retry cannot double-complete a
/// session. Repeated attempts on an already-completed session are a safe no-op, which makes the engine safe
/// to call from an idempotent external return (for example a payment redirect).
/// </summary>
public sealed class DefaultWizardEngine : IWizardEngine
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private const string CompletionLockPrefix = "WIZARD_COMPLETE_";

    private readonly IWizardSessionStore _sessionStore;
    private readonly IEnumerable<IWizardHandler> _wizardHandlers;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger<DefaultWizardEngine> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultWizardEngine"/> class.
    /// </summary>
    /// <param name="sessionStore">The wizard session store.</param>
    /// <param name="wizardHandlers">The registered wizard handlers.</param>
    /// <param name="distributedLock">The distributed lock.</param>
    /// <param name="session">The YesSql session used to commit the completion inside the lock.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DefaultWizardEngine(
        IWizardSessionStore sessionStore,
        IEnumerable<IWizardHandler> wizardHandlers,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        ILogger<DefaultWizardEngine> logger)
    {
        _sessionStore = sessionStore;
        _wizardHandlers = wizardHandlers;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WizardCompletionResult> CompleteAsync(
        WizardFlow flow,
        Func<WizardFlow, Task<bool>> prepareUnderLock = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var sessionId = flow.Session.SessionId;

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(
            CompletionLockPrefix + sessionId,
            _lockTimeout,
            _lockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"Could not acquire a completion lock for wizard session '{sessionId}'.");
        }

        await using (locker)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);

            if (session is null)
            {
                return new WizardCompletionResult
                {
                    Status = WizardCompletionStatus.Failed,
                };
            }

            if (session.Status == WizardSessionStatus.Completed)
            {
                return new WizardCompletionResult
                {
                    Status = WizardCompletionStatus.AlreadyCompleted,
                };
            }

            if (session.Status is not (WizardSessionStatus.Pending or WizardSessionStatus.AwaitingExternal))
            {
                return new WizardCompletionResult
                {
                    Status = WizardCompletionStatus.Failed,
                };
            }

            var authoritativeFlow = new WizardFlow(session);

            var blockingStep = GetFirstIncompleteStep(session);

            if (blockingStep is not null)
            {
                return new WizardCompletionResult
                {
                    Status = WizardCompletionStatus.Blocked,
                    BlockingStepKey = blockingStep.Key,
                };
            }

            if (prepareUnderLock is not null && !await prepareUnderLock(authoritativeFlow))
            {
                // Discard any partial writes made by the prepare callback and leave the persisted session
                // in its pending state so the visitor can retry.
                await _session.CancelAsync();

                session.Status = WizardSessionStatus.Failed;
                session.ModifiedUtc = _clock.UtcNow;

                var failedContext = new WizardFlowFailedContext(authoritativeFlow);

                await _wizardHandlers.InvokeAsync((handler, ctx) => handler.FailedAsync(ctx), failedContext, _logger);

                return new WizardCompletionResult
                {
                    Status = WizardCompletionStatus.Failed,
                };
            }

            var completingContext = new WizardFlowCompletingContext(authoritativeFlow);

            try
            {
                // The completing handlers can throw, so they are invoked directly instead of through
                // 'InvokeAsync' (which would swallow the exception) to roll back and fail fast.
                foreach (var handler in _wizardHandlers)
                {
                    await handler.CompletingAsync(completingContext);
                }

                session.Status = WizardSessionStatus.Completed;
                session.CompletedUtc = _clock.UtcNow;
                session.ModifiedUtc = _clock.UtcNow;

                await _sessionStore.SaveAsync(session, cancellationToken);

                // Commit the completing writes and the status transition while the lock is still held so a
                // concurrent retry observes the completed session instead of double-completing it.
                await _session.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A completing handler failed for wizard session '{SessionId}'. The wizard will be marked as failed.", sessionId);

                await _session.CancelAsync();

                session.Status = WizardSessionStatus.Failed;
                session.ModifiedUtc = _clock.UtcNow;

                var handlerFailedContext = new WizardFlowFailedContext(authoritativeFlow);

                await _wizardHandlers.InvokeAsync((h, ctx) => h.FailedAsync(ctx), handlerFailedContext, _logger);

                return new WizardCompletionResult
                {
                    Status = WizardCompletionStatus.Failed,
                };
            }

            var completedContext = new WizardFlowCompletedContext(authoritativeFlow);

            await _wizardHandlers.InvokeAsync((handler, ctx) => handler.CompletedAsync(ctx), completedContext, _logger);

            return new WizardCompletionResult
            {
                Status = WizardCompletionStatus.Completed,
            };
        }
    }

    private static WizardStep GetFirstIncompleteStep(WizardSession session)
    {
        foreach (var step in session.Steps)
        {
            if (step.Conceal || !step.CollectData)
            {
                continue;
            }

            if (!session.SavedSteps.ContainsKey(step.Key))
            {
                return step;
            }
        }

        return null;
    }
}
