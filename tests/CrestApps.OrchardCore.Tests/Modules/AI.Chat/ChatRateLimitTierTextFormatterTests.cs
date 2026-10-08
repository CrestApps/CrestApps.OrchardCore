using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Chat.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

public sealed class ChatRateLimitTierTextFormatterTests
{
    [Fact]
    public void Format_WritesOneLimitAndWindowPerLine()
    {
        var text = ChatRateLimitTierTextFormatter.Format(
        [
            new() { Limit = 5, Window = TimeSpan.FromSeconds(30) },
            new() { Limit = 500, Window = TimeSpan.FromDays(1) },
        ]);

        Assert.Equal($"5, 00:00:30{Environment.NewLine}500, 1.00:00:00", text);
    }

    [Fact]
    public void Format_WithNull_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, ChatRateLimitTierTextFormatter.Format(null));
    }

    [Fact]
    public void Format_RoundTripsThroughTryParse()
    {
        var expected = new PromptSecurityOptions().AnonymousMessageRateLimitTiers;

        Assert.True(ChatRateLimitTierTextFormatter.TryParse(ChatRateLimitTierTextFormatter.Format(expected), out var actual, out var error));
        Assert.Null(error);
        Assert.Equal(
            expected.Select(tier => (tier.Limit, tier.Window)),
            actual.Select(tier => (tier.Limit, tier.Window)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_WithBlankText_ReturnsEmptyTiers(string text)
    {
        Assert.True(ChatRateLimitTierTextFormatter.TryParse(text, out var tiers, out var error));
        Assert.Empty(tiers);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_IgnoresBlankLinesAndSurroundingWhitespace()
    {
        Assert.True(ChatRateLimitTierTextFormatter.TryParse("\n  5 ,  00:00:30  \n\n 150,01:00:00\n", out var tiers, out var error));
        Assert.Null(error);
        Assert.Collection(
            tiers,
            tier =>
            {
                Assert.Equal(5, tier.Limit);
                Assert.Equal(TimeSpan.FromSeconds(30), tier.Window);
            },
            tier =>
            {
                Assert.Equal(150, tier.Limit);
                Assert.Equal(TimeSpan.FromHours(1), tier.Window);
            });
    }

    // The error kind is passed by name because ChatRateLimitTierParseErrorKind is internal to the
    // module and cannot appear in the signature of a public test method.
    [Theory]
    [InlineData("5 00:00:30", nameof(ChatRateLimitTierParseErrorKind.MissingSeparator), 1)]
    [InlineData("5, 00:00:30\nzero, 00:05:00", nameof(ChatRateLimitTierParseErrorKind.InvalidLimit), 2)]
    [InlineData("0, 00:00:30", nameof(ChatRateLimitTierParseErrorKind.InvalidLimit), 1)]
    [InlineData("-1, 00:00:30", nameof(ChatRateLimitTierParseErrorKind.InvalidLimit), 1)]
    [InlineData("5, forever", nameof(ChatRateLimitTierParseErrorKind.InvalidWindow), 1)]
    [InlineData("5, 00:00:00", nameof(ChatRateLimitTierParseErrorKind.InvalidWindow), 1)]
    public void TryParse_WithInvalidLine_ReportsTheFirstProblem(string text, string kind, int lineNumber)
    {
        Assert.False(ChatRateLimitTierTextFormatter.TryParse(text, out var tiers, out var error));
        Assert.Empty(tiers);
        Assert.NotNull(error);
        Assert.Equal(kind, error.Kind.ToString());
        Assert.Equal(lineNumber, error.LineNumber);
    }

    [Fact]
    public void AreEquivalent_ComparesLimitsAndWindowsInOrder()
    {
        List<ChatRateLimitTier> left =
        [
            new() { Limit = 5, Window = TimeSpan.FromSeconds(30) },
            new() { Limit = 30, Window = TimeSpan.FromMinutes(5) },
        ];

        Assert.True(ChatRateLimitTierTextFormatter.AreEquivalent(left,
        [
            new() { Limit = 5, Window = TimeSpan.FromSeconds(30) },
            new() { Limit = 30, Window = TimeSpan.FromMinutes(5) },
        ]));

        Assert.False(ChatRateLimitTierTextFormatter.AreEquivalent(left,
        [
            new() { Limit = 30, Window = TimeSpan.FromMinutes(5) },
            new() { Limit = 5, Window = TimeSpan.FromSeconds(30) },
        ]));

        Assert.False(ChatRateLimitTierTextFormatter.AreEquivalent(left, [left[0]]));
        Assert.False(ChatRateLimitTierTextFormatter.AreEquivalent(left, null));
        Assert.True(ChatRateLimitTierTextFormatter.AreEquivalent(null, []));
    }
}
