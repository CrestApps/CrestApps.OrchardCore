using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Core.Feeds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Documents;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Settings › Commerce › Agent feeds: the OpenAI feed's addresses and what it leaves out, the daily SFTP push, and the
/// ACP feed APIs the products are pushed to, with how the last pushes went.
/// </summary>
public sealed class AgentFeedSettingsDisplayDriver : SiteDisplayDriver<AgentFeedSettings>
{
    /// <summary>
    /// The settings group of the page.
    /// </summary>
    public const string GroupId = "agent-feeds";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly OpenAIProductFeedService _feeds;
    private readonly IProductFeedService _productFeeds;
    private readonly IDocumentManager<AgentFeedStatusDocument> _statusManager;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IHostEnvironment _hostEnvironment;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentFeedSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="feeds">The OpenAI product feed.</param>
    /// <param name="productFeeds">The store's product feed, for its key.</param>
    /// <param name="statusManager">The document the last pushes are recorded in.</param>
    /// <param name="dataProtectionProvider">The tenant's data protection, for the secrets entered.</param>
    /// <param name="hostEnvironment">The host environment.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentFeedSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        OpenAIProductFeedService feeds,
        IProductFeedService productFeeds,
        IDocumentManager<AgentFeedStatusDocument> statusManager,
        IDataProtectionProvider dataProtectionProvider,
        IHostEnvironment hostEnvironment,
        IStringLocalizer<AgentFeedSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _feeds = feeds;
        _productFeeds = productFeeds;
        _statusManager = statusManager;
        _dataProtectionProvider = dataProtectionProvider;
        _hostEnvironment = hostEnvironment;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => GroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, AgentFeedSettings settings, BuildEditorContext context)
        => Initialize<AgentFeedSettingsViewModel>("AgentFeedSettings_Edit", async model =>
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            var baseUrl = site.BaseUrl?.TrimEnd('/') ?? (request is null ? null : $"{request.Scheme}://{request.Host}{request.PathBase}");
            var key = await _productFeeds.GetKeyAsync();
            var query = string.IsNullOrEmpty(key) ? string.Empty : "?key=" + Uri.EscapeDataString(key);
            var sftp = settings.Sftp ?? new AgentFeedSftpSettings();
            var feedContext = await _feeds.GetContextAsync();

            model.TargetCountries = string.Join(", ", settings.TargetCountries ?? []);
            model.SftpEnabled = sftp.Enabled;
            model.SftpHost = sftp.Host;
            model.SftpPort = sftp.Port;
            model.SftpUsername = sftp.Username;
            model.HasSftpPassword = ProtectedSettingExtensions.HasSecret(sftp.Password);
            model.HasSftpPrivateKey = ProtectedSettingExtensions.HasSecret(sftp.PrivateKey);
            model.HasSftpPrivateKeyPassphrase = ProtectedSettingExtensions.HasSecret(sftp.PrivateKeyPassphrase);
            model.SftpHostKeyFingerprint = sftp.HostKeyFingerprint;
            model.SftpRemoteFolder = sftp.RemoteFolder;
            model.SftpFormat = sftp.Format;
            model.SftpCurrency = sftp.Currency;
            model.FeedApiTargets = (settings.FeedApiTargets ?? [])
                .Select(target => new AcpFeedTargetViewModel
                {
                    Id = target.Id,
                    Name = target.Name,
                    BaseUrl = target.BaseUrl,
                    FeedId = target.FeedId,
                    TargetCountry = target.TargetCountry,
                    Currency = target.Currency,
                    Enabled = target.Enabled,
                    HasBearerToken = ProtectedSettingExtensions.HasSecret(target.BearerToken),
                })
                .ToList();

            // One empty row adds a target.
            model.FeedApiTargets.Add(new AcpFeedTargetViewModel());
            model.HasBaseUrl = !string.IsNullOrWhiteSpace(site.BaseUrl);
            model.StoreCountry = feedContext.StoreCountry;
            model.JsonLinesUrl = $"{baseUrl}/feeds/openai/products.jsonl.gz{query}";
            model.CsvUrl = $"{baseUrl}/feeds/openai/products.csv.gz{query}";
            model.Feed = await _feeds.GetAsync(currency: null, fresh: true);
            model.Status = await _statusManager.GetOrCreateImmutableAsync();
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, AgentFeedPermissions.ManageAgentFeeds))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, AgentFeedSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, AgentFeedPermissions.ManageAgentFeeds))
        {
            return null;
        }

        var model = new AgentFeedSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var countries = (model.TargetCountries ?? string.Empty)
            .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(country => country.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (countries.Any(country => country.Length != 2 || !country.All(char.IsAsciiLetter)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TargetCountries), S["Enter two-letter country codes, such as US, CA."]);
        }

        settings.TargetCountries = countries;

        var sftp = settings.Sftp ??= new AgentFeedSftpSettings();

        sftp.Enabled = model.SftpEnabled;
        sftp.Host = Clean(model.SftpHost);
        sftp.Port = model.SftpPort is > 0 and <= 65535 ? model.SftpPort : 22;
        sftp.Username = Clean(model.SftpUsername);
        sftp.Password = _dataProtectionProvider.ApplySecret(AgentFeedSettings.ProtectionPurpose, sftp.Password, model.SftpPassword, model.RemoveSftpPassword);
        sftp.PrivateKey = _dataProtectionProvider.ApplySecret(AgentFeedSettings.ProtectionPurpose, sftp.PrivateKey, model.SftpPrivateKey, model.RemoveSftpPrivateKey);
        sftp.PrivateKeyPassphrase = _dataProtectionProvider.ApplySecret(AgentFeedSettings.ProtectionPurpose, sftp.PrivateKeyPassphrase, model.SftpPrivateKeyPassphrase, model.RemoveSftpPrivateKey);
        sftp.HostKeyFingerprint = Clean(model.SftpHostKeyFingerprint);
        sftp.RemoteFolder = Clean(model.SftpRemoteFolder) ?? "/";
        sftp.Format = model.SftpFormat is OpenAIProductFeedFormats.CsvGzip ? OpenAIProductFeedFormats.CsvGzip : OpenAIProductFeedFormats.JsonLinesGzip;
        sftp.Currency = CurrencyCodeUtility.IsValid(model.SftpCurrency) ? CurrencyCodeUtility.Normalize(model.SftpCurrency) : null;

        if (sftp.Enabled && (sftp.Host is null || sftp.Username is null || (sftp.Password is null && sftp.PrivateKey is null)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SftpHost), S["Enter the SFTP host, user name, and a password or private key to push the feed."]);
        }

        settings.FeedApiTargets = UpdateTargets(settings.FeedApiTargets ?? [], model.FeedApiTargets ?? [], context);

        return Edit(site, settings, context);
    }

    private List<AcpFeedTarget> UpdateTargets(List<AcpFeedTarget> existing, List<AcpFeedTargetViewModel> posted, UpdateEditorContext context)
    {
        var targets = new List<AcpFeedTarget>();
        var isDevelopment = _hostEnvironment.IsDevelopment();

        for (var i = 0; i < posted.Count; i++)
        {
            var row = posted[i];
            var current = string.IsNullOrEmpty(row.Id) ? null : existing.FirstOrDefault(target => target.Id == row.Id);

            // A blank new row adds nothing; a row marked for removal goes.
            if (row.Remove || (current is null && string.IsNullOrWhiteSpace(row.BaseUrl)))
            {
                continue;
            }

            var baseUrl = Clean(row.BaseUrl);

            if (!EgressNetwork.IsAllowedUrl(baseUrl, isDevelopment))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{nameof(AgentFeedSettingsViewModel.FeedApiTargets)}[{i}].{nameof(AcpFeedTargetViewModel.BaseUrl)}", S["Enter the feed API's https address, such as https://api.example.com/acp."]);
            }

            var target = current ?? new AcpFeedTarget { Id = IdGenerator.GenerateId() };

            target.Name = Clean(row.Name) ?? baseUrl;
            target.BaseUrl = baseUrl;
            target.BearerToken = _dataProtectionProvider.ApplySecret(AgentFeedSettings.ProtectionPurpose, target.BearerToken, row.BearerToken);
            target.FeedId = Clean(row.FeedId);
            target.TargetCountry = Clean(row.TargetCountry)?.ToUpperInvariant();
            target.Currency = CurrencyCodeUtility.IsValid(row.Currency) ? CurrencyCodeUtility.Normalize(row.Currency) : null;
            target.Enabled = row.Enabled;

            if (target.TargetCountry is { } country && (country.Length != 2 || !country.All(char.IsAsciiLetter)))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{nameof(AgentFeedSettingsViewModel.FeedApiTargets)}[{i}].{nameof(AcpFeedTargetViewModel.TargetCountry)}", S["Enter a two-letter country code, such as US."]);
            }

            if (target.Enabled && !ProtectedSettingExtensions.HasSecret(target.BearerToken))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{nameof(AgentFeedSettingsViewModel.FeedApiTargets)}[{i}].{nameof(AcpFeedTargetViewModel.BearerToken)}", S["Enter the feed API's key."]);
            }

            targets.Add(target);
        }

        return targets;
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
