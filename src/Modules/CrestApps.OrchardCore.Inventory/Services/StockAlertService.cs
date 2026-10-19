using System.Net;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// Keeps shoppers' requests to hear when a product is back in stock, and emails them, once each, when it is.
/// </summary>
public sealed class StockAlertService
{
    /// <summary>
    /// The most requests one email address makes in a day, so the form can't be used to flood someone's inbox.
    /// </summary>
    public const int MaxPerEmailPerDay = 10;

    /// <summary>
    /// The most emails one restock sends at once; the rest go when the next one does.
    /// </summary>
    public const int MaxPerRestock = 500;

    private readonly ISession _session;
    private readonly IStockItemStore _stockItems;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public StockAlertService(
        ISession session,
        IStockItemStore stockItems,
        IContentManager contentManager,
        IClock clock,
        IServiceProvider serviceProvider,
        ILogger<StockAlertService> logger,
        IStringLocalizer<StockAlertService> stringLocalizer)
    {
        _session = session;
        _stockItems = stockItems;
        _contentManager = contentManager;
        _clock = clock;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Records a request, unless the product is in stock, isn't counted, or the same email already asked.
    /// </summary>
    public async Task<StockAlertResult> SubscribeAsync(string contentItemId, string variantId, string email, string userId, CancellationToken cancellationToken = default)
    {
        email = email?.Trim();

        if (string.IsNullOrEmpty(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Length > 254)
        {
            return StockAlertResult.Failure(S["Enter a valid email address."]);
        }

        var item = string.IsNullOrEmpty(contentItemId) ? null : await _stockItems.FindAsync(contentItemId, string.IsNullOrEmpty(variantId) ? null : variantId, cancellationToken: cancellationToken);

        if (item is null || item.Archived)
        {
            return StockAlertResult.Failure(S["That product can't be found."]);
        }

        if (item.Available > 0 || item.AllowBackorder)
        {
            return StockAlertResult.Failure(S["{0} can be bought now.", item.Title]);
        }

        var normalized = email.ToUpperInvariant();
        var since = _clock.UtcNow.AddDays(-1);

        if (await _session.QueryIndex<StockAlertIndex>(index => index.StockItemId == item.ItemId && index.NormalizedEmail == normalized && index.Waiting, InventoryConstants.CollectionName).CountAsync(cancellationToken) > 0)
        {
            return StockAlertResult.Success(S["You'll get an email at {0} when {1} is back.", email, item.Title]);
        }

        if (await _session.QueryIndex<StockAlertIndex>(index => index.NormalizedEmail == normalized && index.CreatedUtc >= since, InventoryConstants.CollectionName).CountAsync(cancellationToken) >= MaxPerEmailPerDay)
        {
            return StockAlertResult.Failure(S["That email has asked about enough products today. Try again tomorrow."]);
        }

        await _session.SaveAsync(new StockAlert
        {
            StockItemId = item.ItemId,
            ContentItemId = item.ContentItemId,
            VariantId = item.VariantId,
            Email = email,
            UserId = userId,
            CreatedUtc = _clock.UtcNow,
        }, collection: InventoryConstants.CollectionName, cancellationToken: cancellationToken);

        return StockAlertResult.Success(S["You'll get an email at {0} when {1} is back.", email, item.Title]);
    }

    public Task<int> CountWaitingAsync(string stockItemId, CancellationToken cancellationToken = default)
        => _session.QueryIndex<StockAlertIndex>(index => index.StockItemId == stockItemId && index.Waiting, InventoryConstants.CollectionName).CountAsync(cancellationToken);

    /// <summary>
    /// Emails everyone waiting for the stock item, oldest request first, and marks each request sent.
    /// </summary>
    public async Task<int> NotifyAsync(StockItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_serviceProvider.GetService<IEmailService>() is not { } emailService)
        {
            return 0;
        }

        var alerts = (await _session.Query<StockAlert, StockAlertIndex>(index => index.StockItemId == item.ItemId && index.Waiting, InventoryConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .Take(MaxPerRestock)
            .ListAsync(cancellationToken)).ToList();

        if (alerts.Count == 0)
        {
            return 0;
        }

        var url = await GetProductUrlAsync(item.ContentItemId);
        var subject = S["{0} is back in stock", item.Title].Value;
        var sent = 0;

        foreach (var alert in alerts)
        {
            var text = S["Good news: {0} is back in stock.", item.Title].Value + (url is null ? string.Empty : "\n\n" + url) + "\n\n" + S["You asked to hear when it was back, so this is the only email about it."].Value;
            var html = "<p>" + WebUtility.HtmlEncode(S["Good news: {0} is back in stock.", item.Title].Value) + "</p>" +
                (url is null ? string.Empty : $"<p><a href=\"{WebUtility.HtmlEncode(url)}\">{WebUtility.HtmlEncode(S["See {0}", item.Title].Value)}</a></p>") +
                "<p style=\"color:#6c757d;font-size:12px\">" + WebUtility.HtmlEncode(S["You asked to hear when it was back, so this is the only email about it."].Value) + "</p>";

            var result = await emailService.SendAsync(new MailMessage
            {
                To = alert.Email,
                Subject = subject,
                TextBody = text,
                HtmlBody = html,
            }, cancellationToken: cancellationToken);

            if (!result.Succeeded)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("The back-in-stock email for stock item '{StockItemId}' couldn't be sent.", item.ItemId);
                }

                continue;
            }

            alert.SentUtc = _clock.UtcNow;
            await _session.SaveAsync(alert, collection: InventoryConstants.CollectionName, cancellationToken: cancellationToken);
            sent++;
        }

        return sent;
    }

    // The product's page, from the site's base URL; without one, the email names the product only.
    private async Task<string> GetProductUrlAsync(string contentItemId)
    {
        var site = await _serviceProvider.GetRequiredService<ISiteService>().GetSiteSettingsAsync();
        var product = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);

        if (string.IsNullOrWhiteSpace(site.BaseUrl) || product is null)
        {
            return null;
        }

        // The product's own address when it has one; outside a request, routing can't build it.
        if (((System.Text.Json.Nodes.JsonObject)product.Content)["AutoroutePart"]?["Path"]?.GetValue<string>() is { Length: > 0 } slug)
        {
            return site.BaseUrl.TrimEnd('/') + "/" + slug.TrimStart('/');
        }

        var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(product);
        var path = metadata.DisplayRouteValues is null ? null : _serviceProvider.GetRequiredService<LinkGenerator>().GetPathByRouteValues(routeName: null, metadata.DisplayRouteValues);

        return path is null ? null : site.BaseUrl.TrimEnd('/') + path;
    }
}

public sealed record StockAlertResult(bool Succeeded, string Message)
{
    public static StockAlertResult Success(string message)
        => new(true, message);

    public static StockAlertResult Failure(string message)
        => new(false, message);
}
