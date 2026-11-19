using CrestApps.Core.Models;
using CrestApps.OrchardCore.Checkout.AI;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Checkout.AI;

/// <summary>
/// The coupon tools need Manage coupons, save a coupon with the coupon editor's rules starting from the stored coupon,
/// take dates in the site's time zone, never return a generated code, and generate and delete codes as the coupon page
/// does.
/// </summary>
public sealed class CouponToolTests
{
    private static readonly DateTime _now = new(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryCouponStore _store = new();

    public CouponToolTests()
    {
        _store.Items["spring"] = new Coupon
        {
            ItemId = "spring",
            Code = "SPRING15",
            Kind = CouponKind.Percentage,
            Percentage = 15m,
            IsEnabled = true,
            MaxRedemptionsPerCustomer = 1,
            CreatedUtc = _now.AddDays(-2),
        };
        _store.Items["old"] = new Coupon
        {
            ItemId = "old",
            Code = "BLACKFRIDAY",
            Kind = CouponKind.FixedAmount,
            Amount = 10m,
            Currency = "USD",
            IsEnabled = false,
            MaxRedemptions = 100,
            RedemptionCount = 40,
            CreatedUtc = _now.AddDays(-200),
        };
        _store.Generated["spring"] = [new CouponCode { ItemId = "g1", CouponId = "spring", Code = "VIP-SECRET1", CreatedUtc = _now }];
    }

    public static TheoryData<string> ToolNames => new()
    {
        ListCouponsTool.TheName,
        GetCouponTool.TheName,
        SaveCouponTool.TheName,
        DeleteCouponTool.TheName,
        GenerateCouponCodesTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(CheckoutAIToolBase.NotAllowedMessage, result);
        Assert.Equal(2, _store.Items.Count);
        Assert.Single(_store.Generated["spring"]);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageCoupons(string toolName)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != CheckoutPermissions.ManageCoupons.Name);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(CheckoutAIToolBase.NotAllowedMessage, result);
        Assert.Equal(CheckoutPermissions.ManageCoupons.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.True(_store.Items["spring"].IsEnabled);
    }

    [Fact]
    public async Task ListCoupons_FiltersAsTheCouponList_AndFlagsUnboundedCoupons()
    {
        // Act
        var active = AgentToolTestContext.ReadJson(await InvokeAsync(ListCouponsTool.TheName, Build(), new() { ["status"] = "active" }));
        var disabled = AgentToolTestContext.ReadJson(await InvokeAsync(ListCouponsTool.TheName, Build(), new() { ["status"] = "disabled" }));
        var wrong = await InvokeAsync(ListCouponsTool.TheName, Build(), new() { ["status"] = "expired" });

        // Assert
        var coupon = Assert.Single(active.GetProperty("items").EnumerateArray());

        Assert.Equal("SPRING15", coupon.GetProperty("code").GetString());
        Assert.True(coupon.GetProperty("unbounded").GetBoolean());
        Assert.Equal(1, active.GetProperty("unboundedCoupons").GetInt32());
        Assert.Equal(60, Assert.Single(disabled.GetProperty("items").EnumerateArray()).GetProperty("usesLeft").GetInt32());
        Assert.StartsWith("Nothing was listed: status must be", wrong, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCoupon_CountsGeneratedCodes_NeverReturnsThem_AndDoesNotLookUpAGeneratedCode()
    {
        // Act
        var result = await InvokeAsync(GetCouponTool.TheName, Build(), new() { ["code"] = "spring15" });
        var byGeneratedCode = await InvokeAsync(GetCouponTool.TheName, Build(), new() { ["code"] = "VIP-SECRET1" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("spring", json.GetProperty("coupon").GetProperty("couponId").GetString());
        Assert.Equal(1, json.GetProperty("generatedCodes").GetProperty("total").GetInt32());
        Assert.DoesNotContain("VIP-SECRET1", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no coupon has the code", byGeneratedCode, StringComparison.Ordinal);
        Assert.DoesNotContain("spring", byGeneratedCode.Replace("'VIP-SECRET1'", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveCoupon_CreatesWithTheEditorsRules_AndDatesInTheSitesTimeZone()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveCouponTool.TheName, Build(), new()
        {
            ["code"] = "SUMMER20",
            ["percentage"] = 20,
            ["ends"] = "2026-04-30T23:59",
            ["maxUsesPerCustomer"] = 1,
        }));

        // Assert
        var created = Assert.Single(_store.Items.Values, coupon => coupon.Code == "SUMMER20");

        Assert.Equal(20m, created.Percentage);
        Assert.Equal(new DateTime(2026, 4, 30, 23, 59, 0, DateTimeKind.Utc), created.EndsUtc);
        Assert.Equal(1, created.MaxRedemptionsPerCustomer);
        Assert.True(created.IsEnabled);
        Assert.Equal(created.ItemId, json.GetProperty("coupon").GetProperty("couponId").GetString());
    }

    [Fact]
    public async Task SaveCoupon_ChangesOnlyWhatIsGiven_AndNeverTheUseCount()
    {
        // Act
        var result = await InvokeAsync(SaveCouponTool.TheName, Build(), new() { ["couponId"] = "old", ["enabled"] = true, ["maxUses"] = 0 });

        // Assert
        var coupon = _store.Items["old"];

        Assert.DoesNotContain("Nothing was", result, StringComparison.Ordinal);
        Assert.True(coupon.IsEnabled);
        Assert.Null(coupon.MaxRedemptions);
        Assert.Equal(40, coupon.RedemptionCount);
        Assert.Equal(10m, coupon.Amount);
        Assert.Equal("USD", coupon.Currency);
        Assert.Contains("neither a use limit nor an end date", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("percentage", 150, "percentage:")]
    [InlineData("code", "BLACKFRIDAY", "code:")]
    [InlineData("kind", "half", "kind must be")]
    [InlineData("ends", "next week", "write ends as")]
    public async Task SaveCoupon_RefusesWhatTheEditorRefuses_AndSavesNothing(string argument, object value, string expected)
    {
        // Act
        var result = await InvokeAsync(SaveCouponTool.TheName, Build(), new() { ["couponId"] = "spring", [argument] = value });

        // Assert
        Assert.StartsWith("Nothing was saved", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Equal("SPRING15", _store.Items["spring"].Code);
        Assert.Equal(15m, _store.Items["spring"].Percentage);
    }

    [Fact]
    public async Task GenerateCouponCodes_MakesTheCodes_AndReturnsOnlyHowMany()
    {
        // Act
        var result = await InvokeAsync(GenerateCouponCodesTool.TheName, Build(), new() { ["couponId"] = "spring", ["count"] = 200, ["prefix"] = "vip", ["usesPerCode"] = 1 });
        var tooMany = await InvokeAsync(GenerateCouponCodesTool.TheName, Build(), new() { ["couponId"] = "spring", ["count"] = 5001 });
        var badPrefix = await InvokeAsync(GenerateCouponCodesTool.TheName, Build(), new() { ["couponId"] = "spring", ["count"] = 1, ["prefix"] = "no spaces!" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(200, json.GetProperty("generated").GetInt32());
        Assert.Equal(201, _store.Generated["spring"].Count);
        Assert.DoesNotContain("VIP-", result, StringComparison.Ordinal);
        Assert.Contains("between 1 and 5000", tooMany, StringComparison.Ordinal);
        Assert.Contains("a prefix is up to 12", badPrefix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteCoupon_DeletesTheCouponAndItsCodes_AndSaysWhenNotFound()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(DeleteCouponTool.TheName, Build(), new() { ["couponId"] = "spring" }));
        var missing = await InvokeAsync(DeleteCouponTool.TheName, Build(), new() { ["couponId"] = "nope" });

        // Assert
        Assert.Equal(1, json.GetProperty("deleted").GetProperty("generatedCodes").GetInt32());
        Assert.False(_store.Items.ContainsKey("spring"));
        Assert.False(_store.Generated.ContainsKey("spring"));
        Assert.Contains("no coupon has the id 'nope'", missing, StringComparison.Ordinal);
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            ListCouponsTool.TheName => [],
            SaveCouponTool.TheName => new() { ["couponId"] = "spring", ["enabled"] = false },
            GenerateCouponCodesTool.TheName => new() { ["couponId"] = "spring", ["count"] = 5 },
            _ => new() { ["couponId"] = "spring" },
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);
        services.AddSingleton(clock.Object);
        services.AddSingleton<ICouponStore>(_store);
        services.AddSingleton(new CouponEditor(_store, new PassThroughStringLocalizer<CouponEditor>()));
        services.AddSingleton(Mock.Of<YesSqlSession>());
        services.AddSingleton<CheckoutToolScope>();

        var redemptions = new Mock<ICouponRedemptionService>();
        redemptions.Setup(value => value.ListAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<CouponRedemption>)[]);
        services.AddSingleton(redemptions.Object);

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListCouponsTool.TheName => new ListCouponsTool(),
            GetCouponTool.TheName => new GetCouponTool(),
            SaveCouponTool.TheName => new SaveCouponTool(),
            DeleteCouponTool.TheName => new DeleteCouponTool(),
            _ => new GenerateCouponCodesTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }

    private sealed class InMemoryCouponStore : ICouponStore
    {
        public Dictionary<string, Coupon> Items { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<CouponCode>> Generated { get; } = new(StringComparer.Ordinal);

        public ValueTask<Coupon> FindByIdAsync(string id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(id is not null && Items.TryGetValue(id, out var coupon) ? coupon : null);

        public ValueTask<IReadOnlyCollection<Coupon>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<Coupon>>(Items.Values.ToArray());

        public ValueTask<IReadOnlyCollection<Coupon>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<Coupon>>(Items.Values.Where(coupon => ids.Contains(coupon.ItemId)).ToArray());

        public ValueTask<PageResult<Coupon>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
            where TQuery : QueryContext
            => throw new NotSupportedException();

        public ValueTask CreateAsync(Coupon entry, CancellationToken cancellationToken = default)
        {
            Items[entry.ItemId] = entry;

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(Coupon entry, CancellationToken cancellationToken = default)
        {
            Items[entry.ItemId] = entry;

            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> DeleteAsync(Coupon entry, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Items.Remove(entry.ItemId));

        public Task<Coupon> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var normalized = code?.Trim().ToUpperInvariant();

            if (Items.Values.FirstOrDefault(coupon => coupon.Code == normalized) is { } coupon)
            {
                return Task.FromResult(coupon);
            }

            // A generated code is its coupon, as the real store returns it.
            foreach (var (couponId, codes) in Generated)
            {
                if (codes.FirstOrDefault(entry => entry.Code == normalized) is { } generated)
                {
                    return Task.FromResult(new Coupon { ItemId = generated.ItemId, Code = generated.Code, GeneratedCodeId = generated.ItemId, Description = Items[couponId].Description });
                }
            }

            return Task.FromResult<Coupon>(null);
        }

        public Task<IReadOnlyList<Coupon>> ListAutomaticAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Coupon>>(Items.Values.Where(coupon => coupon.IsAutomatic).ToArray());

        public Task<IReadOnlyList<CouponCode>> GenerateCodesAsync(Coupon coupon, int count, string prefix, int? usesPerCode, CancellationToken cancellationToken = default)
        {
            var codes = Enumerable.Range(0, count)
                .Select(index => new CouponCode { ItemId = $"c{index}", CouponId = coupon.ItemId, Code = $"{prefix}-{index:D6}", MaxRedemptions = usesPerCode })
                .ToList();

            if (!Generated.TryGetValue(coupon.ItemId, out var existing))
            {
                Generated[coupon.ItemId] = existing = [];
            }

            existing.AddRange(codes);

            return Task.FromResult<IReadOnlyList<CouponCode>>(codes);
        }

        public Task<IReadOnlyList<CouponCode>> ListGeneratedCodesAsync(string couponId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CouponCode>>(Generated.TryGetValue(couponId, out var codes) ? codes : []);

        public Task<(int Total, int Used)> CountGeneratedCodesAsync(string couponId, CancellationToken cancellationToken = default)
            => Task.FromResult(Generated.TryGetValue(couponId, out var codes) ? (codes.Count, codes.Count(code => code.RedemptionCount > 0)) : (0, 0));

        public Task DeleteGeneratedCodesAsync(string couponId, CancellationToken cancellationToken = default)
        {
            Generated.Remove(couponId);

            return Task.CompletedTask;
        }
    }
}
