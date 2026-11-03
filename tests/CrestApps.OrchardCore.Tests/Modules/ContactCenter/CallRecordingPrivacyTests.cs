using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class CallRecordingPrivacyTests
{
    [Theory]
    [InlineData("My card is [CARD].", true)]
    [InlineData("It's [NAME] at [ADDRESS].", true)]
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("call me on 555-123-4567", true)]
    [InlineData("I want to pay 120 dollars on the 5th", false)]
    [InlineData("Yes, that's right.", false)]
    [InlineData(null, false)]
    public void HasPersonalData_FindsPlaceholdersAndLongNumbers(string text, bool expected)
        => Assert.Equal(expected, CallRecordingAudio.HasPersonalData(text));

    [Fact]
    public void FindSpans_CoversEachCustomerLineWithADetailUntilTheNextLine()
    {
        // Arrange
        var transcript = new CallRecordingTranscript
        {
            Phrases =
            [
                new() { Speaker = CallRecordingTranscriptSpeaker.Ai, Text = "What is your card number?", OffsetSeconds = 1 },
                new() { Speaker = CallRecordingTranscriptSpeaker.Customer, Text = "It's [CARD].", OffsetSeconds = 4 },
                new() { Speaker = CallRecordingTranscriptSpeaker.Ai, Text = "Thanks. Anything else?", OffsetSeconds = 9 },
                new() { Speaker = CallRecordingTranscriptSpeaker.Customer, Text = "No, thanks.", OffsetSeconds = 12 },
                new() { Speaker = CallRecordingTranscriptSpeaker.Customer, Text = "Oh, my email is [EMAIL].", OffsetSeconds = 14 },
                new() { Speaker = CallRecordingTranscriptSpeaker.Customer, Text = "[NAME] without a time.", OffsetSeconds = null },
            ],
        };

        // Act
        var spans = CallRecordingAudio.FindSpans(transcript, durationSeconds: 20);

        // Assert
        Assert.Equal([(4 - CallRecordingAudio.LeadSeconds, 9d), (14 - CallRecordingAudio.LeadSeconds, 20d)], spans);
    }

    [Fact]
    public void Bleep_ReplacesOnlyTheSpanWithATone_AndTheWavRoundTrips()
    {
        // Arrange
        const int rate = 8000;
        var samples = Enumerable.Repeat(0.5f, rate * 3).ToArray();

        // Act
        var covered = CallRecordingAudio.Bleep(samples, rate, [(1d, 2d)]);

        using var wav = new MemoryStream();
        CallRecordingAudio.WriteWav(wav, samples, rate);
        wav.Position = 0;
        var decoded = CallRecordingAudio.Decode(wav, "wav");

        // Assert
        Assert.Equal(rate, covered);
        Assert.Equal(0.5f, samples[rate - 1]);
        Assert.Equal(0.5f, samples[2 * rate]);
        Assert.True(samples.Skip(rate).Take(rate).Max() <= 0.21f);
        Assert.NotNull(decoded);
        Assert.Equal(rate, decoded.Value.SampleRate);
        Assert.Equal(samples.Length, decoded.Value.Samples.Length);
        Assert.Equal(0.5f, decoded.Value.Samples[0], 0.001f);
    }

    [Fact]
    public void Decode_ReadsNothingItCannot()
    {
        using var stream = new MemoryStream([1, 2, 3, 4]);

        Assert.Null(CallRecordingAudio.Decode(stream, "ogg"));
        Assert.Null(CallRecordingAudio.Decode(stream, "wav"));
    }

    [Fact]
    public void Links_WorkUntilTheyExpire_AndCannotBeForged()
    {
        // Arrange
        var now = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(() => now);
        var links = new CallRecordingLinks(new EphemeralDataProtectionProvider(), clock.Object);

        // Act
        var (token, expires) = links.Create("r-1", TimeSpan.FromHours(1));
        var (_, capped) = links.Create("r-1", TimeSpan.FromDays(30));
        var valid = links.Read(token);
        var forged = links.Read(token[..^2] + (token[^1] == 'A' ? "BB" : "AA"));
        now = now.AddHours(2);
        var expired = links.Read(token);

        // Assert
        Assert.Equal(new DateTime(2026, 10, 28, 13, 0, 0, DateTimeKind.Utc), expires);
        Assert.Equal(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc), capped);
        Assert.Equal("r-1", valid);
        Assert.Null(forged);
        Assert.Null(expired);
        Assert.Null(links.Read(null));
    }
}
