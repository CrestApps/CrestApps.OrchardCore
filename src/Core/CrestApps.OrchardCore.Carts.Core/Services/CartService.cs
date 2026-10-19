using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Carts.Core.Services;

/// <inheritdoc />
/// <remarks>
/// Every change is priced through the product resolvers before it is kept, and every cart is priced again when
/// it is shown, so a product withdrawn, repriced or taken off sale after it was added is caught before anyone
/// pays. Nothing the browser sends is a price.
/// </remarks>
public sealed class CartService : ICartService
{
    /// <summary>
    /// The cookie that identifies a guest's cart. It holds one protected token; the cart stores only its hash.
    /// </summary>
    internal static readonly GuestSessionTokenScope GuestTokenScope = new(
        "cart_owner",
        "CrestApps.OrchardCore.Carts.GuestOwnership.v1",
        TimeSpan.FromDays(90),
        MaxSessions: 1,
        HeaderName: "X-Cart-Token");

    internal const string GuestTokenKey = "cart";

    private readonly ICartStore _store;
    private readonly ISession _session;
    private readonly GuestSessionTokenManager _tokens;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IContentManager _contentManager;
    private readonly IProductSnapshotResolver _snapshotResolver;
    private readonly IPriceResolver _priceResolver;
    private readonly IEnumerable<IProductAvailabilityProvider> _availability;
    private readonly IClock _clock;
    private readonly CartOptions _options;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public CartService(
        ICartStore store,
        ISession session,
        GuestSessionTokenManager tokens,
        IHttpContextAccessor httpContextAccessor,
        IContentManager contentManager,
        IProductSnapshotResolver snapshotResolver,
        IPriceResolver priceResolver,
        IEnumerable<IProductAvailabilityProvider> availability,
        IClock clock,
        IOptions<CartOptions> options,
        ILogger<CartService> logger,
        IStringLocalizer<CartService> stringLocalizer)
    {
        _store = store;
        _session = session;
        _tokens = tokens;
        _httpContextAccessor = httpContextAccessor;
        _contentManager = contentManager;
        _snapshotResolver = snapshotResolver;
        _priceResolver = priceResolver;
        _availability = availability;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
        S = stringLocalizer;
    }

    public async Task<Cart> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();

        var cart = userId is not null
            ? await _store.FindByUserAsync(userId, cancellationToken)
            : await _store.FindByGuestTokenHashAsync(_tokens.GetHash(GuestTokenScope, GuestTokenKey), cancellationToken);

        // An expired cart waits for the cleanup task, but it is no longer anyone's cart.
        return cart is not null && cart.ExpiresUtc >= _clock.UtcNow ? cart : null;
    }

    public Task<CartResult> AddAsync(string contentItemId, string priceId, int quantity, CancellationToken cancellationToken = default)
        => AddAsync(contentItemId, priceId, variantId: null, quantity, cancellationToken);

    public Task<CartResult> AddAsync(string contentItemId, string priceId, string variantId, int quantity, CancellationToken cancellationToken = default)
        => AddAsync(contentItemId, priceId, variantId, quantity, addOns: null, cancellationToken);

    public async Task<CartResult> AddAsync(string contentItemId, string priceId, string variantId, int quantity, IReadOnlyDictionary<string, string> addOns, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            return CartResult.Failure(S["This product is no longer available."]);
        }

        quantity = Math.Max(1, quantity);

        var cart = await GetCurrentAsync(cancellationToken);
        var currency = cart?.Lines.Count > 0 ? cart.Currency : null;

        var resolved = await ResolveAsync(contentItemId, priceId, variantId, quantity, currency, cart?.ItemId, cancellationToken);

        if (resolved.Problem is not null)
        {
            return CartResult.Failure(resolved.Problem, cart);
        }

        variantId = resolved.Product.VariantId;

        // Only the product's own add-ons are kept, and the required ones have to be there.
        var chosen = ProductAddOns.Normalize(resolved.ProductPart, addOns);

        if (ProductAddOns.FindMissing(resolved.ProductPart, chosen) is { } missing)
        {
            return CartResult.Failure(S["Fill in {0} to add {1} to your cart.", missing.Label, resolved.Title], cart);
        }

        var surcharge = ProductAddOns.GetSurcharge(resolved.ProductPart, chosen);

        var existing = cart?.Lines.FirstOrDefault(line =>
            line.ContentItemId == contentItemId &&
            string.Equals(line.PriceId, resolved.Price.PriceId, StringComparison.Ordinal) &&
            string.Equals(line.VariantId, variantId, StringComparison.Ordinal) &&
            ProductAddOns.AreSame(line.AddOns, chosen));

        var total = (existing?.Quantity ?? 0) + quantity;

        if (QuantityProblem(resolved, total) is { } rule)
        {
            return CartResult.Failure(rule, cart);
        }

        if (total > resolved.MaxQuantity)
        {
            return CartResult.Failure(resolved.LimitedByStock
                ? S["Only {0} of {1} left.", resolved.MaxQuantity, resolved.Title]
                : resolved.MaxQuantity == 1
                    ? S["{0} is already in your cart, and only one can be bought.", resolved.Title]
                    : S["You can buy at most {0} of {1}.", resolved.MaxQuantity, resolved.Title], cart);
        }

        if (existing is null && cart?.Lines.Count >= _options.MaxLines)
        {
            return CartResult.Failure(S["Your cart is full. Buy or remove some items first."], cart);
        }

        // A price may allow some quantities and not others, so the new total is asked about, not only what
        // was added.
        if (existing is not null)
        {
            resolved = await ResolveAsync(contentItemId, priceId, variantId, total, currency, cart.ItemId, cancellationToken);

            if (resolved.Problem is not null)
            {
                return CartResult.Failure(resolved.Problem, cart);
            }
        }

        var isNew = cart is null;
        cart ??= CreateCart();

        if (existing is null)
        {
            cart.Lines.Add(new CartLine
            {
                LineId = UniqueId.GenerateId(),
                ContentItemId = contentItemId,
                PriceId = resolved.Price.PriceId,
                VariantId = variantId,
                Quantity = quantity,
                AddedUnitPrice = resolved.Price.UnitPrice + surcharge,
                AddedUtc = _clock.UtcNow,
                AddOns = chosen,
            });
        }
        else
        {
            // The buyer just saw and accepted the price for the new quantity, such as a volume discount reached.
            existing.Quantity = total;
            existing.AddedUnitPrice = resolved.Price.UnitPrice + surcharge;
        }

        cart.Currency = resolved.Price.Currency;

        if (!await SaveAsync(cart, isNew, cancellationToken))
        {
            return CartResult.Failure(S["Your cart was changed in another window. Please try again."]);
        }

        return CartResult.Success(cart, S["{0} was added to your cart.", resolved.Title]);
    }

    public async Task<CartResult> UpdateQuantityAsync(string lineId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity < 1)
        {
            return await RemoveAsync(lineId, cancellationToken);
        }

        var cart = await GetCurrentAsync(cancellationToken);
        var line = cart?.Lines.FirstOrDefault(candidate => candidate.LineId == lineId);

        if (line is null)
        {
            return CartResult.Failure(S["That item is no longer in your cart."], cart);
        }

        var resolved = await ResolveAsync(line.ContentItemId, line.PriceId, line.VariantId, quantity, cart.Currency, cart.ItemId, cancellationToken);

        if (resolved.Problem is not null)
        {
            return CartResult.Failure(resolved.Problem, cart);
        }

        if (quantity > resolved.MaxQuantity)
        {
            return CartResult.Failure(resolved.LimitedByStock
                ? S["Only {0} of {1} left.", resolved.MaxQuantity, resolved.Title]
                : S["You can buy at most {0} of {1}.", resolved.MaxQuantity, resolved.Title], cart);
        }

        if (QuantityProblem(resolved, quantity) is { } rule)
        {
            return CartResult.Failure(rule, cart);
        }

        line.Quantity = quantity;

        // A new quantity can reach or leave a volume discount: the price it gives is the one the buyer now expects.
        line.AddedUnitPrice = resolved.Price is null
            ? line.AddedUnitPrice
            : resolved.Price.UnitPrice + ProductAddOns.GetSurcharge(resolved.ProductPart, line.AddOns);

        if (!await SaveAsync(cart, isNew: false, cancellationToken))
        {
            return CartResult.Failure(S["Your cart was changed in another window. Please try again."]);
        }

        return CartResult.Success(cart, S["Your cart was updated."]);
    }

    public async Task<CartResult> RemoveAsync(string lineId, CancellationToken cancellationToken = default)
    {
        var cart = await GetCurrentAsync(cancellationToken);

        if (cart is null || cart.Lines.RemoveAll(line => line.LineId == lineId) == 0)
        {
            return CartResult.Failure(S["That item is no longer in your cart."], cart);
        }

        if (cart.Lines.Count == 0)
        {
            cart.Currency = null;
        }

        if (!await SaveAsync(cart, isNew: false, cancellationToken))
        {
            return CartResult.Failure(S["Your cart was changed in another window. Please try again."]);
        }

        return CartResult.Success(cart, S["The item was removed from your cart."]);
    }

    public async Task DeleteAsync(Cart cart, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cart);

        await _store.DeleteAsync(cart, cancellationToken);

        if (cart.GuestTokenHash is not null && cart.GuestTokenHash == _tokens.GetHash(GuestTokenScope, GuestTokenKey))
        {
            _tokens.Revoke(GuestTokenScope, GuestTokenKey);
        }
    }

    public async Task MergeGuestCartAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var hash = _tokens.GetHash(GuestTokenScope, GuestTokenKey);

        if (hash is null)
        {
            return;
        }

        _tokens.Revoke(GuestTokenScope, GuestTokenKey);

        var guestCart = await _store.FindByGuestTokenHashAsync(hash, cancellationToken);

        if (guestCart is null || guestCart.Lines.Count == 0 || guestCart.ExpiresUtc < _clock.UtcNow)
        {
            return;
        }

        var userCart = await _store.FindByUserAsync(userId, cancellationToken);

        if (userCart is null || userCart.ExpiresUtc < _clock.UtcNow || userCart.Lines.Count == 0)
        {
            if (userCart is not null)
            {
                await _store.DeleteAsync(userCart, cancellationToken);
            }

            // The guest's cart simply becomes theirs.
            guestCart.UserId = userId;
            guestCart.GuestTokenHash = null;
            Touch(guestCart);

            await _store.UpdateAsync(guestCart, cancellationToken);

            return;
        }

        // Lines priced in another currency cannot join the user's cart, and are dropped with the guest cart.
        if (string.Equals(userCart.Currency, guestCart.Currency, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var guestLine in guestCart.Lines)
            {
                var line = userCart.Lines.FirstOrDefault(candidate =>
                    candidate.ContentItemId == guestLine.ContentItemId &&
                    string.Equals(candidate.PriceId, guestLine.PriceId, StringComparison.Ordinal) &&
                    string.Equals(candidate.VariantId, guestLine.VariantId, StringComparison.Ordinal) &&
                    ProductAddOns.AreSame(candidate.AddOns, guestLine.AddOns));

                if (line is not null)
                {
                    // Quantities the price does not allow are pointed out when the cart is shown.
                    line.Quantity = Math.Min(_options.MaxQuantity, line.Quantity + guestLine.Quantity);
                }
                else if (userCart.Lines.Count < _options.MaxLines)
                {
                    userCart.Lines.Add(guestLine);
                }
            }
        }
        else if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The guest cart of user '{UserId}' was priced in '{GuestCurrency}' and their cart in '{UserCurrency}', so its lines were not merged.", userId, guestCart.Currency, userCart.Currency);
        }

        Touch(userCart);

        await _store.UpdateAsync(userCart, cancellationToken);
        await _store.DeleteAsync(guestCart, cancellationToken);
    }

    public async Task<CartPricing> PriceAsync(Cart cart, CancellationToken cancellationToken = default)
    {
        if (cart is null)
        {
            return new CartPricing();
        }

        var lines = new List<CartLinePricing>();

        foreach (var line in cart.Lines)
        {
            var resolved = await ResolveAsync(line.ContentItemId, line.PriceId, line.VariantId, line.Quantity, cart.Currency, cart.ItemId, cancellationToken);
            var problem = resolved.Problem;

            if (problem is null && line.Quantity > resolved.MaxQuantity)
            {
                problem = resolved.LimitedByStock
                    ? S["Only {0} left. Lower the quantity to check out.", resolved.MaxQuantity]
                    : S["You can buy at most {0} of this product.", resolved.MaxQuantity];
            }

            problem ??= QuantityProblem(resolved, line.Quantity);

            // What the line's add-ons add to each unit, at today's add-on prices.
            var unitPrice = resolved.Price is null
                ? line.AddedUnitPrice
                : resolved.Price.UnitPrice + ProductAddOns.GetSurcharge(resolved.ProductPart, line.AddOns);

            lines.Add(new CartLinePricing
            {
                Line = line,
                ContentItem = resolved.ContentItem,
                Product = resolved.Product,
                Price = resolved.Price,
                Title = resolved.Title,
                UnitPrice = unitPrice,
                LineTotal = unitPrice * line.Quantity,
                MaxQuantity = resolved.MaxQuantity,
                ShipsFromUtc = resolved.ReleaseDateUtc,
                PriceChanged = resolved.Price is not null && unitPrice != line.AddedUnitPrice,
                AddOns = ProductAddOns.Describe(resolved.ProductPart, line.AddOns),
                Problem = problem,
            });
        }

        return new CartPricing
        {
            Cart = cart,
            Currency = cart.Currency,
            Lines = lines,
        };
    }

    private async Task<ResolvedLine> ResolveAsync(string contentItemId, string priceId, string variantId, int quantity, string currency, string cartId, CancellationToken cancellationToken)
    {
        var contentItem = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);

        if (contentItem is null)
        {
            return new ResolvedLine(S["This product is no longer available."]);
        }

        var title = contentItem.DisplayText;

        if (_options.ExcludedParts.Any(contentItem.Has))
        {
            return new ResolvedLine(S["{0} is bought on its own, not through the cart.", title]) { ContentItem = contentItem, Title = title };
        }

        var product = await _snapshotResolver.ResolveAsync(new ProductSnapshotContext(contentItem) { PriceId = priceId, VariantId = variantId, Quantity = quantity }, cancellationToken);

        if (product is null)
        {
            // A product sold in variants needs one chosen, and the one chosen must still be sold.
            if (contentItem.TryGet<ProductVariantsPart>(out var variants) && variants.HasVariants)
            {
                return new ResolvedLine(string.IsNullOrEmpty(variantId)
                    ? S["Choose the options of {0} first.", title]
                    : S["This option of {0} is no longer available.", title]) { ContentItem = contentItem, Title = title };
            }

            return new ResolvedLine(S["{0} can't be bought online.", title]) { ContentItem = contentItem, Title = title };
        }

        title = string.IsNullOrEmpty(product.Title) ? title : product.Title;

        var price = await _priceResolver.ResolveAsync(new ProductSnapshotContext(contentItem) { PriceId = priceId, VariantId = variantId, Quantity = quantity }, cancellationToken);

        if (price is null)
        {
            return new ResolvedLine(S["{0} can't be bought at this price or quantity.", title]) { ContentItem = contentItem, Product = product, Title = title };
        }

        var resolved = new ResolvedLine(null)
        {
            ContentItem = contentItem,
            Product = product,
            Price = price,
            Title = title,
            MaxQuantity = GetMaxQuantity(price),
        };

        // The product's own limits: at most so many, at least so many, and in packs.
        if (contentItem.TryGet<ProductPart>(out var productPart))
        {
            resolved = resolved with
            {
                ProductPart = productPart,
                MaxQuantity = productPart.MaxQuantity is > 0 and var max ? Math.Min(resolved.MaxQuantity, max) : resolved.MaxQuantity,
            };
        }

        // A product whose stock is counted can't be bought beyond what's available, unless it takes backorders. The
        // units this cart's own unpaid orders hold count as available to it.
        var availability = (await _availability.GetAvailabilityAsync([new ProductStockKey(contentItem.ContentItemId, product.VariantId)], cartId, cancellationToken))
            .GetValueOrDefault(new ProductStockKey(contentItem.ContentItemId, product.VariantId));

        // A pre-order says when it ships, on the cart and the order.
        if (availability?.Status == ProductAvailabilityStatus.Preorder)
        {
            resolved = resolved with { ReleaseDateUtc = availability.ReleaseDateUtc };
        }

        if (availability is not null && !availability.AllowBackorder)
        {
            if (availability.Available <= 0)
            {
                return resolved with { MaxQuantity = 0, LimitedByStock = true, Problem = S["{0} is out of stock.", title] };
            }

            if (availability.Available < resolved.MaxQuantity)
            {
                resolved = resolved with { MaxQuantity = availability.Available, LimitedByStock = true };
            }
        }

        if (price.IsRecurring)
        {
            return resolved with { Problem = S["{0} is a subscription, which is signed up for on its own.", title] };
        }

        if (price.Price?.AllowCustomAmount == true)
        {
            return resolved with { Problem = S["{0} asks for an amount, so it can't be added to the cart.", title] };
        }

        if (!string.IsNullOrEmpty(currency) && !string.Equals(price.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            return resolved with { Problem = S["{0} is priced in {1}, and your cart is in {2}.", title, price.Currency, currency] };
        }

        return resolved;
    }

    private LocalizedString QuantityProblem(ResolvedLine resolved, int quantity)
        => ProductVolumePricing.Check(resolved.ProductPart, quantity) switch
        {
            ProductQuantityRule.BelowMinimum => S["Buy at least {0} of {1}.", resolved.ProductPart.MinQuantity, resolved.Title],
            ProductQuantityRule.NotAMultiple => S["{0} is sold in packs of {1}. Choose a multiple of {1}.", resolved.Title, resolved.ProductPart.QuantityStep],
            _ => null,
        };

    private int GetMaxQuantity(PriceResult price)
    {
        if (price.Price is null)
        {
            return _options.MaxQuantity;
        }

        if (!price.Price.AllowQuantity)
        {
            return 1;
        }

        return Math.Min(price.Price.MaximumQuantity ?? _options.MaxQuantity, _options.MaxQuantity);
    }

    private Cart CreateCart()
    {
        var userId = GetUserId();
        var now = _clock.UtcNow;

        return new Cart
        {
            ItemId = UniqueId.GenerateId(),
            UserId = userId,

            // A guest is given a token only when they first add something, so browsing issues no cookie.
            GuestTokenHash = userId is null ? _tokens.Issue(GuestTokenScope, GuestTokenKey) : null,
            CreatedUtc = now,
        };
    }

    private void Touch(Cart cart)
    {
        var now = _clock.UtcNow;

        cart.ModifiedUtc = now;
        cart.ExpiresUtc = now + (cart.UserId is null ? _options.GuestCartLifetime : _options.UserCartLifetime);
    }

    private async Task<bool> SaveAsync(Cart cart, bool isNew, CancellationToken cancellationToken)
    {
        Touch(cart);

        if (isNew)
        {
            await _store.CreateAsync(cart, cancellationToken);
        }
        else
        {
            await _store.UpdateAsync(cart, cancellationToken);
        }

        try
        {
            await _session.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (ConcurrencyException)
        {
            // Another request changed the cart first. The change is dropped rather than retried, so the end of
            // the request does not try to save it again.
            await _session.CancelAsync();

            return false;
        }
    }

    private string GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    }

    private sealed record ResolvedLine(LocalizedString Problem)
    {
        public ContentItem ContentItem { get; init; }

        public ISellableProduct Product { get; init; }

        public PriceResult Price { get; init; }

        public string Title { get; init; }

        public int MaxQuantity { get; init; } = 1;

        /// <summary>
        /// Gets the product's part, whose quantity rules the line follows.
        /// </summary>
        public ProductPart ProductPart { get; init; }

        /// <summary>
        /// Gets whether <see cref="MaxQuantity"/> is what's left in stock rather than what the price allows.
        /// </summary>
        public bool LimitedByStock { get; init; }

        public DateTime? ReleaseDateUtc { get; init; }
    }
}
