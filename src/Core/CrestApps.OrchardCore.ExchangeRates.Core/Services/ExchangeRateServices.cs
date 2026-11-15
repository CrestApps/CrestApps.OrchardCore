using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ExchangeRates.Core.Indexes;
using CrestApps.OrchardCore.ExchangeRates.Models;
using CrestApps.OrchardCore.ExchangeRates.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ExchangeRates.Core.Indexes
{
    /// <summary>
    /// Indexes an exchange rate by its pair and day.
    /// </summary>
    public sealed class ExchangeRateIndex : CrestApps.Core.Data.YesSql.Indexes.CatalogItemIndex
    {
        public string Currency { get; set; }

        public string BaseCurrency { get; set; }

        public DateTime Date { get; set; }

        public string Source { get; set; }
    }

    /// <summary>
    /// Maps <see cref="ExchangeRate"/> to <see cref="ExchangeRateIndex"/>.
    /// </summary>
    public sealed class ExchangeRateIndexProvider : IndexProvider<ExchangeRate>
    {
        public ExchangeRateIndexProvider()
        {
            CollectionName = ExchangeRatesConstants.CollectionName;
        }

        /// <inheritdoc/>
        public override void Describe(DescribeContext<ExchangeRate> context)
        {
            context
                .For<ExchangeRateIndex>()
                .Map(rate => new ExchangeRateIndex
                {
                    ItemId = rate.ItemId,
                    Currency = rate.Currency,
                    BaseCurrency = rate.BaseCurrency,
                    Date = rate.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    Source = rate.Source,
                });
        }
    }
}

namespace CrestApps.OrchardCore.ExchangeRates.Core.Services
{
    /// <summary>
    /// The YesSql store of the exchange rates.
    /// </summary>
    public interface IExchangeRateStore : ICatalog<ExchangeRate>
    {
        /// <summary>
        /// Finds the latest rate of a pair on or before a day.
        /// </summary>
        Task<ExchangeRate> FindLatestAsync(string currency, string baseCurrency, DateOnly onOrBefore, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds the rate of a pair on a day.
        /// </summary>
        Task<ExchangeRate> FindAsync(string currency, string baseCurrency, DateOnly date, CancellationToken cancellationToken = default);
    }

    /// <inheritdoc cref="IExchangeRateStore"/>
    public sealed class ExchangeRateStore : DocumentCatalog<ExchangeRate, ExchangeRateIndex>, IExchangeRateStore
    {
        public ExchangeRateStore(ISession session)
            : base(session)
        {
            CollectionName = ExchangeRatesConstants.CollectionName;
        }

        /// <inheritdoc/>
        public async Task<ExchangeRate> FindLatestAsync(string currency, string baseCurrency, DateOnly onOrBefore, CancellationToken cancellationToken = default)
        {
            var day = onOrBefore.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            return await Session.Query<ExchangeRate, ExchangeRateIndex>(index => index.Currency == currency && index.BaseCurrency == baseCurrency && index.Date <= day, collection: CollectionName)
                .OrderByDescending(index => index.Date)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<ExchangeRate> FindAsync(string currency, string baseCurrency, DateOnly date, CancellationToken cancellationToken = default)
        {
            var day = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            return await Session.Query<ExchangeRate, ExchangeRateIndex>(index => index.Currency == currency && index.BaseCurrency == baseCurrency && index.Date == day, collection: CollectionName)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    /// <inheritdoc cref="IExchangeRateManager"/>
    public sealed class ExchangeRateManager : CatalogManager<ExchangeRate>, IExchangeRateManager
    {
        public ExchangeRateManager(IExchangeRateStore store, IEnumerable<ICatalogEntryHandler<ExchangeRate>> handlers, ILogger<CatalogManager<ExchangeRate>> logger)
            : base(store, handlers, logger)
        {
        }
    }

    /// <summary>
    /// Keeps currency codes upper case and refuses a rate that could not convert, or a second rate of a pair for a day.
    /// </summary>
    public sealed class ExchangeRateHandler : CatalogEntryHandlerBase<ExchangeRate>
    {
        private readonly IExchangeRateStore _store;
        private readonly IClock _clock;

        internal readonly IStringLocalizer S;

        public ExchangeRateHandler(IExchangeRateStore store, IClock clock, IStringLocalizer<ExchangeRateHandler> stringLocalizer)
        {
            _store = store;
            _clock = clock;
            S = stringLocalizer;
        }

        /// <inheritdoc/>
        public override Task InitializedAsync(InitializedContext<ExchangeRate> context, CancellationToken cancellationToken = default)
        {
            context.Model.CreatedUtc = _clock.UtcNow;
            context.Model.Source ??= ExchangeRatesConstants.ManualSource;

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task ValidatingAsync(ValidatingContext<ExchangeRate> context, CancellationToken cancellationToken = default)
        {
            var rate = context.Model;

            rate.Currency = ExchangeRateCodes.Normalize(rate.Currency);
            rate.BaseCurrency = ExchangeRateCodes.Normalize(rate.BaseCurrency);

            if (!ExchangeRateCodes.IsValid(rate.Currency))
            {
                context.Result.Fail(new ValidationResult(S["The currency must be a three-letter code, such as EUR."], [nameof(ExchangeRate.Currency)]));
            }

            if (!ExchangeRateCodes.IsValid(rate.BaseCurrency))
            {
                context.Result.Fail(new ValidationResult(S["The base currency must be a three-letter code, such as USD."], [nameof(ExchangeRate.BaseCurrency)]));
            }
            else if (string.Equals(rate.Currency, rate.BaseCurrency, StringComparison.Ordinal))
            {
                context.Result.Fail(new ValidationResult(S["A rate converts one currency into another: choose two different currencies."], [nameof(ExchangeRate.BaseCurrency)]));
            }

            if (rate.Rate <= 0m)
            {
                context.Result.Fail(new ValidationResult(S["The rate must be more than zero."], [nameof(ExchangeRate.Rate)]));
            }

            if (rate.Date == default)
            {
                context.Result.Fail(new ValidationResult(S["Say from which day the rate holds."], [nameof(ExchangeRate.Date)]));
            }
            else if (context.Result.Succeeded && await _store.FindAsync(rate.Currency, rate.BaseCurrency, rate.Date, cancellationToken) is { } other && other.ItemId != rate.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["{0} to {1} already has a rate on that day. Change that one instead.", rate.Currency, rate.BaseCurrency], [nameof(ExchangeRate.Date)]));
            }
        }
    }

    /// <summary>
    /// Checks and normalizes ISO 4217 codes.
    /// </summary>
    public static class ExchangeRateCodes
    {
        public static string Normalize(string code)
            => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

        public static bool IsValid(string code)
            => code is { Length: 3 } && code.All(char.IsAsciiLetterUpper);
    }

    /// <summary>
    /// The default <see cref="IExchangeRateService"/>, reading the rates the store keeps.
    /// </summary>
    public sealed class DefaultExchangeRateService : IExchangeRateService
    {
        private readonly IExchangeRateStore _store;
        private readonly ISiteService _siteService;

        public DefaultExchangeRateService(IExchangeRateStore store, ISiteService siteService)
        {
            _store = store;
            _siteService = siteService;
        }

        /// <inheritdoc/>
        public async Task<string> GetBaseCurrencyAsync(CancellationToken cancellationToken = default)
            => ExchangeRateCodes.Normalize((await _siteService.GetSettingsAsync<ExchangeRateSettings>()).BaseCurrency);

        /// <inheritdoc/>
        public async Task<ExchangeRateQuote> GetRateAsync(string currency, string baseCurrency, DateOnly date, CancellationToken cancellationToken = default)
        {
            currency = ExchangeRateCodes.Normalize(currency);
            baseCurrency = ExchangeRateCodes.Normalize(baseCurrency);

            if (currency is null || baseCurrency is null)
            {
                return null;
            }

            if (string.Equals(currency, baseCurrency, StringComparison.Ordinal))
            {
                return new ExchangeRateQuote(1m, date, ExchangeRatesConstants.ManualSource);
            }

            var direct = await _store.FindLatestAsync(currency, baseCurrency, date, cancellationToken);
            var inverse = await _store.FindLatestAsync(baseCurrency, currency, date, cancellationToken);

            // The newer of the two holds; the inverse is turned around, to 10 decimal places.
            if (direct is not null && (inverse is null || direct.Date >= inverse.Date))
            {
                return new ExchangeRateQuote(direct.Rate, direct.Date, direct.Source);
            }

            return inverse is null ? null : new ExchangeRateQuote(Math.Round(1m / inverse.Rate, 10), inverse.Date, inverse.Source);
        }
    }

    /// <summary>
    /// Fetches the day's rates from the provider the settings name and keeps those not entered already.
    /// </summary>
    public sealed class ExchangeRateUpdater
    {
        private readonly IEnumerable<IExchangeRateProvider> _providers;
        private readonly IExchangeRateManager _manager;
        private readonly IExchangeRateStore _store;
        private readonly ISiteService _siteService;
        private readonly ILogger _logger;

        public ExchangeRateUpdater(
            IEnumerable<IExchangeRateProvider> providers,
            IExchangeRateManager manager,
            IExchangeRateStore store,
            ISiteService siteService,
            ILogger<ExchangeRateUpdater> logger)
        {
            _providers = providers;
            _manager = manager;
            _store = store;
            _siteService = siteService;
            _logger = logger;
        }

        /// <summary>
        /// Fetches and keeps the rates of a day. Returns how many were added; a rate of a pair the day has already is
        /// kept as it is.
        /// </summary>
        public async Task<int> UpdateAsync(DateOnly date, CancellationToken cancellationToken = default)
        {
            var settings = await _siteService.GetSettingsAsync<ExchangeRateSettings>();
            var baseCurrency = ExchangeRateCodes.Normalize(settings.BaseCurrency);
            var provider = _providers.FirstOrDefault(item => string.Equals(item.Name, settings.ProviderName, StringComparison.Ordinal));
            var currencies = (settings.Currencies ?? []).Select(ExchangeRateCodes.Normalize).Where(code => ExchangeRateCodes.IsValid(code) && code != baseCurrency).Distinct().ToArray();

            if (provider is null || baseCurrency is null || currencies.Length == 0)
            {
                return 0;
            }

            var rates = await provider.GetRatesAsync(baseCurrency, currencies, date, cancellationToken);
            var added = 0;

            foreach (var (currency, value) in rates ?? new Dictionary<string, decimal>())
            {
                var code = ExchangeRateCodes.Normalize(currency);

                if (!currencies.Contains(code) || value <= 0m || await _store.FindAsync(code, baseCurrency, date, cancellationToken) is not null)
                {
                    continue;
                }

                var rate = await _manager.NewAsync(cancellationToken: cancellationToken);
                rate.Currency = code;
                rate.BaseCurrency = baseCurrency;
                rate.Rate = value;
                rate.Date = date;
                rate.Source = provider.Name;

                var validation = await _manager.ValidateAsync(rate, cancellationToken);

                if (!validation.Succeeded)
                {
                    _logger.LogWarning("The {Currency} rate from '{Provider}' was not kept: {Errors}", code, provider.Name, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

                    continue;
                }

                await _manager.CreateAsync(rate, cancellationToken);
                added++;
            }

            return added;
        }
    }

    /// <summary>
    /// Registers the exchange rates.
    /// </summary>
    public static class ExchangeRatesServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the exchange rate store, manager and handler, the rate service and the updater.
        /// </summary>
        public static IServiceCollection AddExchangeRates(this IServiceCollection services)
        {
            services.TryAddScoped<IExchangeRateStore, ExchangeRateStore>();
            services.TryAddScoped<IExchangeRateManager, ExchangeRateManager>();
            services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ExchangeRate>, ExchangeRateHandler>());
            services.TryAddScoped<IExchangeRateService, DefaultExchangeRateService>();
            services.TryAddScoped<ExchangeRateUpdater>();

            return services;
        }
    }
}
