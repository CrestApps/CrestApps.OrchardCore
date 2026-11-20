using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// Awards, revokes and reads digital credentials. Awards and revocations take one lock and are saved before it is
/// released, so an award is made once even when two requests ask for it at the same time.
/// </summary>
public sealed class DefaultCredentialAwardService : ICredentialAwardService
{
    // Awards, revocations and recipe imports take the same lock: status list slots are picked and checked under it.
    internal const string AwardLockKey = "LearningCredentials:Award";
    private const string _lockKey = AwardLockKey;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(60);

    private readonly CredentialAwardStore _store;
    private readonly ICredentialDefinitionCatalog _definitions;
    private readonly CredentialRecipientResolver _recipients;
    private readonly CredentialIssuerContext _issuerContext;
    private readonly CredentialSigningKeys _keys;
    private readonly CredentialStatusLists _statusLists;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IServiceProvider _services;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCredentialAwardService"/> class.
    /// </summary>
    /// <param name="store">The award store.</param>
    /// <param name="definitions">The definitions.</param>
    /// <param name="recipients">The recipients.</param>
    /// <param name="issuerContext">The issuer.</param>
    /// <param name="keys">The credentials key set.</param>
    /// <param name="statusLists">The status lists.</param>
    /// <param name="distributedLock">The distributed lock.</param>
    /// <param name="session">The session.</param>
    /// <param name="services">The services, for the observers, which may use this service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DefaultCredentialAwardService(
        CredentialAwardStore store,
        ICredentialDefinitionCatalog definitions,
        CredentialRecipientResolver recipients,
        CredentialIssuerContext issuerContext,
        CredentialSigningKeys keys,
        CredentialStatusLists statusLists,
        IDistributedLock distributedLock,
        ISession session,
        IServiceProvider services,
        IClock clock,
        ILogger<DefaultCredentialAwardService> logger)
    {
        _store = store;
        _definitions = definitions;
        _recipients = recipients;
        _issuerContext = issuerContext;
        _keys = keys;
        _statusLists = statusLists;
        _distributedLock = distributedLock;
        _session = session;
        _services = services;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CredentialAwardResult> AwardAsync(CredentialAwardRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.DefinitionId) || string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.RuleKind))
        {
            return CredentialAwardResult.Fail(CredentialAwardErrors.InvalidRequest);
        }

        var definition = await _definitions.FindAsync(request.DefinitionId, cancellationToken);

        if (definition?.Achievement is null)
        {
            return CredentialAwardResult.Fail(CredentialAwardErrors.DefinitionNotFound);
        }

        var recipient = await _recipients.FindAsync(request.UserId);

        if (recipient is null)
        {
            return CredentialAwardResult.Fail(CredentialAwardErrors.RecipientNotFound);
        }

        if (string.IsNullOrWhiteSpace(recipient.Email))
        {
            return CredentialAwardResult.Fail(CredentialAwardErrors.RecipientHasNoAddress);
        }

        var issuer = await _issuerContext.GetAsync();

        if (issuer is null)
        {
            return CredentialAwardResult.Fail(CredentialAwardErrors.NoIssuerAddress);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(_lockKey, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return CredentialAwardResult.Fail(CredentialAwardErrors.Busy);
        }

        CredentialAward award;

        await using (locker)
        {
            var now = TruncateToSeconds(_clock.UtcNow);
            var held = await _store.ListForUserAsync(request.UserId, definition.DefinitionId, cancellationToken);

            if (!string.IsNullOrEmpty(request.SourceId) &&
                held.FirstOrDefault(item => item.RuleKind == request.RuleKind && item.SourceId == request.SourceId) is { } existing)
            {
                return CredentialAwardResult.Success(existing, created: false);
            }

            // A person who holds a valid award of the definition gets no second one, whichever rule asks.
            if (held.Where(item => item.GetStatus(now) == CredentialAwardStatus.Active).OrderByDescending(item => item.IssuedUtc).FirstOrDefault() is { } valid)
            {
                return CredentialAwardResult.Success(valid, created: false);
            }

            var (listNumber, listIndex) = await _statusLists.AllocateAsync(cancellationToken);
            var salt = CredentialIdentityHash.NewSalt();

            award = new CredentialAward
            {
                ItemId = UniqueId.GenerateId(),
                PublicId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
                DefinitionId = definition.DefinitionId,
                DefinitionVersionId = definition.VersionId,
                Achievement = Copy(definition.Achievement, CredentialUrls.Achievement(issuer.BaseUrl, definition.DefinitionId)),
                RecipientUserId = request.UserId,
                Recipient = new CredentialRecipientIdentity
                {
                    IdentityType = CredentialIdentityHash.EmailAddress,
                    IdentityHash = CredentialIdentityHash.Hash(recipient.Email, salt),
                    Salt = salt,
                },
                RuleKind = request.RuleKind,
                SourceId = string.IsNullOrEmpty(request.SourceId) ? UniqueId.GenerateId() : request.SourceId,
                AwardedBy = request.Actor,
                AwardReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                IssuedUtc = now,
                ExpiresUtc = definition.Achievement.ValidityMonths is > 0 ? now.AddMonths(definition.Achievement.ValidityMonths.Value) : null,
                StatusListNumber = listNumber,
                StatusListIndex = listIndex,
            };

            var credential = OpenBadgeCredentialBuilder.Build(new OpenBadgeCredentialInput
            {
                BaseUrl = issuer.BaseUrl,
                IssuerName = issuer.Name,
                PublicId = award.PublicId,
                Achievement = award.Achievement,
                Recipient = award.Recipient,
                ValidFromUtc = award.IssuedUtc,
                ValidUntilUtc = award.ExpiresUtc,
                StatusListNumber = award.StatusListNumber,
                StatusListIndex = award.StatusListIndex,
            });

            award.Credential = credential.ToJsonString();
            award.Jwt = await _keys.SignAsync(CredentialJws.Payload(credential), issuer.BaseUrl, cancellationToken);

            await _store.CreateAsync(award, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        await NotifyAsync(observer => observer.AwardedAsync(award, cancellationToken), award);

        return CredentialAwardResult.Success(award, created: true);
    }

    /// <inheritdoc/>
    public async Task<CredentialAwardResult> RevokeAsync(string awardId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return CredentialAwardResult.Fail(CredentialAwardErrors.ReasonRequired);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(_lockKey, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return CredentialAwardResult.Fail(CredentialAwardErrors.Busy);
        }

        CredentialAward award;

        await using (locker)
        {
            award = await FindByIdAsync(awardId, cancellationToken);

            if (award is null)
            {
                return CredentialAwardResult.Fail(CredentialAwardErrors.AwardNotFound);
            }

            if (award.IsRevoked)
            {
                return CredentialAwardResult.Fail(CredentialAwardErrors.AlreadyRevoked);
            }

            award.RevokedUtc = TruncateToSeconds(_clock.UtcNow);
            award.RevokedBy = actor;
            award.RevokedReason = reason.Trim();

            await _store.UpdateAsync(award, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        await NotifyAsync(observer => observer.RevokedAsync(award, cancellationToken), award);

        return CredentialAwardResult.Success(award, created: false);
    }

    /// <inheritdoc/>
    public async Task<CredentialAward> FindByIdAsync(string awardId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(awardId) ? null : await _store.FindByIdAsync(awardId, cancellationToken);

    /// <inheritdoc/>
    public Task<CredentialAward> FindByPublicIdAsync(string publicId, CancellationToken cancellationToken = default)
        => IsPublicId(publicId) ? _store.FindByPublicIdAsync(publicId, cancellationToken) : Task.FromResult<CredentialAward>(null);

    /// <inheritdoc/>
    public Task<IReadOnlyList<CredentialAward>> ListForUserAsync(string userId, CancellationToken cancellationToken = default)
        => _store.ListForUserAsync(userId, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> SetShowNameAsync(string awardId, string userId, bool showName, CancellationToken cancellationToken = default)
    {
        var award = await FindByIdAsync(awardId, cancellationToken);

        if (award is null || string.IsNullOrEmpty(userId) || !string.Equals(award.RecipientUserId, userId, StringComparison.Ordinal))
        {
            return false;
        }

        award.ShowName = showName;
        await _store.UpdateAsync(award, cancellationToken);

        return true;
    }

    /// <summary>
    /// Tells whether a value has the shape of a public code: 32 lowercase hexadecimal characters.
    /// </summary>
    /// <param name="publicId">The value.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public static bool IsPublicId(string publicId)
        => publicId is { Length: 32 } && publicId.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static CredentialAchievement Copy(CredentialAchievement achievement, string id)
        => new()
        {
            Id = id,
            Name = achievement.Name,
            Description = achievement.Description,
            Criteria = achievement.Criteria,
            ImageUrl = achievement.ImageUrl,
            AchievementType = achievement.AchievementType,
            Tags = (achievement.Tags ?? []).ToList(),
            Alignments = (achievement.Alignments ?? [])
                .Where(alignment => alignment is not null)
                .Select(alignment => new CredentialAlignment
                {
                    TargetName = alignment.TargetName,
                    TargetUrl = alignment.TargetUrl,
                    TargetDescription = alignment.TargetDescription,
                    TargetFramework = alignment.TargetFramework,
                    TargetCode = alignment.TargetCode,
                    TargetType = alignment.TargetType,
                })
                .ToList(),
            ValidityMonths = achievement.ValidityMonths,
        };

    private static DateTime TruncateToSeconds(DateTime utc)
        => new(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

    private async Task NotifyAsync(Func<ICredentialAwardObserver, Task> notify, CredentialAward award)
    {
        foreach (var observer in _services.GetServices<ICredentialAwardObserver>())
        {
            try
            {
                await notify(observer);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "An observer of the credential award '{AwardId}' failed.", award.ItemId);
            }
        }
    }
}
