using CrestApps.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Transactions.Core.Indexes
{
    /// <summary>
    /// Indexes a party's collection settings by party and what they turn off.
    /// </summary>
    public sealed class PartyCollectionSettingsIndex : CrestApps.Core.Data.YesSql.Indexes.CatalogItemIndex
    {
        public string PartyKind { get; set; }

        public string PartyId { get; set; }

        public bool RemindersPaused { get; set; }

        public bool LateFeesExempt { get; set; }
    }

    /// <summary>
    /// Maps <see cref="PartyCollectionSettings"/> to <see cref="PartyCollectionSettingsIndex"/>.
    /// </summary>
    public sealed class PartyCollectionSettingsIndexProvider : IndexProvider<PartyCollectionSettings>
    {
        public PartyCollectionSettingsIndexProvider()
        {
            CollectionName = TransactionsConstants.CollectionName;
        }

        /// <inheritdoc/>
        public override void Describe(DescribeContext<PartyCollectionSettings> context)
        {
            context
                .For<PartyCollectionSettingsIndex>()
                .Map(settings => new PartyCollectionSettingsIndex
                {
                    ItemId = settings.ItemId,
                    PartyKind = settings.Party.IsEmpty ? null : settings.Party.Kind,
                    PartyId = settings.Party.IsEmpty ? null : settings.Party.Id,
                    RemindersPaused = settings.RemindersPaused,
                    LateFeesExempt = settings.LateFeesExempt,
                });
        }
    }
}

namespace CrestApps.OrchardCore.Transactions.Core.Services
{
    /// <summary>
    /// The YesSql store of the parties' collection settings.
    /// </summary>
    public sealed class PartyCollectionSettingsStore : DocumentCatalog<PartyCollectionSettings, PartyCollectionSettingsIndex>
    {
        public PartyCollectionSettingsStore(ISession session)
            : base(session)
        {
            CollectionName = TransactionsConstants.CollectionName;
        }

        public async Task<PartyCollectionSettings> FindByPartyAsync(PartyRef party, CancellationToken cancellationToken = default)
        {
            var kind = party.Kind;
            var id = party.Id;

            return await Session.Query<PartyCollectionSettings, PartyCollectionSettingsIndex>(index => index.PartyKind == kind && index.PartyId == id, collection: CollectionName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PartyCollectionSettings>> ListExcludedAsync(CancellationToken cancellationToken = default)
            => [.. await Session.Query<PartyCollectionSettings, PartyCollectionSettingsIndex>(index => index.RemindersPaused || index.LateFeesExempt, collection: CollectionName)
                .ListAsync(cancellationToken)];
    }

    /// <summary>
    /// The default <see cref="IPartyCollectionSettingsService"/>.
    /// </summary>
    public sealed class DefaultPartyCollectionSettingsService : IPartyCollectionSettingsService
    {
        private readonly PartyCollectionSettingsStore _store;
        private readonly ISession _session;
        private readonly IClock _clock;

        public DefaultPartyCollectionSettingsService(PartyCollectionSettingsStore store, ISession session, IClock clock)
        {
            _store = store;
            _session = session;
            _clock = clock;
        }

        /// <inheritdoc/>
        public async Task<PartyCollectionSettings> GetAsync(PartyRef party, CancellationToken cancellationToken = default)
            => party.IsEmpty ? null : await _store.FindByPartyAsync(party, cancellationToken);

        /// <inheritdoc/>
        public async Task SaveAsync(PartyRef party, bool remindersPaused, bool lateFeesExempt, CancellationToken cancellationToken = default)
        {
            if (party.IsEmpty)
            {
                return;
            }

            var settings = await _store.FindByPartyAsync(party, cancellationToken);

            if (settings is null)
            {
                settings = new PartyCollectionSettings { ItemId = IdGenerator.GenerateId(), Party = party };
                settings.RemindersPaused = remindersPaused;
                settings.LateFeesExempt = lateFeesExempt;
                settings.ModifiedUtc = _clock.UtcNow;
                await _store.CreateAsync(settings, cancellationToken);
            }
            else
            {
                settings.RemindersPaused = remindersPaused;
                settings.LateFeesExempt = lateFeesExempt;
                settings.ModifiedUtc = _clock.UtcNow;
                await _store.UpdateAsync(settings, cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<PartyCollectionExclusions> GetExclusionsAsync(CancellationToken cancellationToken = default)
            => new(await _store.ListExcludedAsync(cancellationToken));
    }

    /// <summary>
    /// Puts a party's collection settings into its billing profile, whatever its kind.
    /// </summary>
    public sealed class PartyCollectionSettingsBillingProfileSource : IPartyBillingProfileSource
    {
        private readonly IPartyCollectionSettingsService _settings;

        public PartyCollectionSettingsBillingProfileSource(IPartyCollectionSettingsService settings)
        {
            _settings = settings;
        }

        /// <inheritdoc/>
        public string Kind => PartyKinds.Any;

        /// <inheritdoc/>
        public async Task ContributeAsync(PartyRef party, PartyBillingProfile profile, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);

            if (await _settings.GetAsync(party, cancellationToken) is { } settings)
            {
                profile.RemindersPaused = settings.RemindersPaused;
                profile.LateFeesExempt = settings.LateFeesExempt;
            }
        }
    }
}
