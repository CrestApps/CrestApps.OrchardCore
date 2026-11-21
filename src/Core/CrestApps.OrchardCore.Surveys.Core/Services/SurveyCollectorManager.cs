using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Identity;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Creates and changes collectors: tokens, passwords and status.
/// </summary>
public sealed class SurveyCollectorManager
{
    private static readonly PasswordHasher<SurveyCollector> _passwordHasher = new();

    private readonly SurveyCollectorStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyCollectorManager"/> class.
    /// </summary>
    /// <param name="store">The collector store.</param>
    /// <param name="clock">The clock.</param>
    public SurveyCollectorManager(
        SurveyCollectorStore store,
        IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Makes a new collector of a survey with a new token; it is not saved.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The collector.</returns>
    public SurveyCollector New(string surveyId, string kind)
        => new()
        {
            ItemId = IdGenerator.GenerateId(),
            SurveyId = surveyId,
            Kind = string.IsNullOrEmpty(kind) ? SurveyCollectorKinds.WebLink : kind,
            Token = SurveyNames.NewToken(),
            Status = CollectorStatus.Open,
            CreatedUtc = _clock.UtcNow,
        };

    /// <summary>
    /// Sets or clears the password of a collector.
    /// </summary>
    /// <param name="collector">The collector.</param>
    /// <param name="password">The password, or empty to clear it.</param>
    public static void SetPassword(SurveyCollector collector, string password)
    {
        ArgumentNullException.ThrowIfNull(collector);

        collector.PasswordHash = string.IsNullOrEmpty(password) ? null : _passwordHasher.HashPassword(collector, password);
    }

    /// <summary>
    /// Checks a password against a collector's.
    /// </summary>
    /// <param name="collector">The collector.</param>
    /// <param name="password">The password entered.</param>
    /// <returns><see langword="true"/> when the collector has no password or the password matches.</returns>
    public static bool VerifyPassword(SurveyCollector collector, string password)
    {
        ArgumentNullException.ThrowIfNull(collector);

        if (string.IsNullOrEmpty(collector.PasswordHash))
        {
            return true;
        }

        return !string.IsNullOrEmpty(password) && _passwordHasher.VerifyHashedPassword(collector, collector.PasswordHash, password) != PasswordVerificationResult.Failed;
    }

    /// <summary>
    /// Saves a collector, new or changed.
    /// </summary>
    /// <param name="collector">The collector.</param>
    /// <param name="isNew">Whether it is new.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveAsync(SurveyCollector collector, bool isNew, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collector);

        collector.ModifiedUtc = _clock.UtcNow;

        if (isNew)
        {
            await _store.CreateAsync(collector, cancellationToken);
        }
        else
        {
            await _store.UpdateAsync(collector, cancellationToken);
        }
    }
}
