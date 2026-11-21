using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

public sealed class XapiFormatsTests
{
    [Theory]
    [InlineData("http://adlnet.gov/expapi/verbs/completed", true)]
    [InlineData("https://w3id.org/xapi/cmi5/context/categories/cmi5", true)]
    [InlineData("urn:uuid:ec531277-b57b-4c15-8d91-d292c5b2b8f7", true)]
    [InlineData("http://example.com/ünïcödé", true)]
    [InlineData("completed", false)]
    [InlineData("/relative/path", false)]
    [InlineData("http://example.com/with space", false)]
    [InlineData("", false)]
    public void Iri_IsAbsoluteWithASchemeAndNoWhiteSpace(string value, bool expected)
        => Assert.Equal(expected, XapiFormats.IsIri(value));

    [Theory]
    [InlineData("ec531277-b57b-4c15-8d91-d292c5b2b8f7", true)]
    [InlineData("EC531277-B57B-4C15-8D91-D292C5B2B8F7", true)]
    [InlineData("ec531277b57b4c158d91d292c5b2b8f7", false)]
    [InlineData("{ec531277-b57b-4c15-8d91-d292c5b2b8f7}", false)]
    [InlineData("not-a-uuid", false)]
    public void Uuid_IsTheStandardStringForm(string value, bool expected)
        => Assert.Equal(expected, XapiFormats.IsUuid(value));

    [Theory]
    [InlineData("en-US", true)]
    [InlineData("en", true)]
    [InlineData("zh-Hant-TW", true)]
    [InlineData("x-private", true)]
    [InlineData("i-klingon", true)]
    [InlineData("en_US", false)]
    [InlineData("", false)]
    [InlineData("toolongprimary-US", false)]
    public void LanguageTag_FollowsRfc5646(string value, bool expected)
        => Assert.Equal(expected, XapiFormats.IsLanguageTag(value));

    [Theory]
    [InlineData("2013-05-18T05:32:34.804Z", "2013-05-18T05:32:34.8040000Z")]
    [InlineData("2013-05-18T05:32:34.804+00:00", "2013-05-18T05:32:34.8040000Z")]
    [InlineData("2013-05-18T07:32:34+02:00", "2013-05-18T05:32:34.0000000Z")]
    [InlineData("2013-05-18T05:32:34", "2013-05-18T05:32:34.0000000Z")]
    [InlineData("2013-05-18T05:32:34.123456789Z", "2013-05-18T05:32:34.1234567Z")]
    public void Timestamp_IsReadAsUtc(string value, string expected)
    {
        Assert.True(XapiFormats.TryParseTimestamp(value, out var utc));
        Assert.Equal(expected, utc.ToString("O"));
    }

    [Theory]
    [InlineData("2013-05-18T05:32:34-00:00")]
    [InlineData("2013-05-18")]
    [InlineData("18/05/2013 05:32")]
    [InlineData("2013-13-45T05:32:34Z")]
    [InlineData("")]
    public void Timestamp_RefusesTheUnknownZoneAndOtherFormats(string value)
        => Assert.False(XapiFormats.TryParseTimestamp(value, out _));

    [Theory]
    [InlineData("PT1H0M0S", true)]
    [InlineData("P1DT4.5S", true)]
    [InlineData("P3W", true)]
    [InlineData("PT0.01S", true)]
    [InlineData("P", false)]
    [InlineData("PT", false)]
    [InlineData("1H", false)]
    [InlineData("PT1.5", false)]
    public void Duration_FollowsIso8601(string value, bool expected)
        => Assert.Equal(expected, XapiFormats.IsDuration(value));

    [Theory]
    [InlineData("mailto:learner@example.com", true)]
    [InlineData("learner@example.com", false)]
    [InlineData("mailto:learner", false)]
    [InlineData("mailto:", false)]
    public void Mailbox_IsAMailtoIri(string value, bool expected)
        => Assert.Equal(expected, XapiFormats.IsMailbox(value));

    [Fact]
    public void Hashes_HaveTheirLengths()
    {
        Assert.True(XapiFormats.IsSha1(new string('a', 40)));
        Assert.False(XapiFormats.IsSha1(new string('g', 40)));
        Assert.True(XapiFormats.IsSha2(new string('0', 64)));
        Assert.True(XapiFormats.IsSha2(new string('0', 128)));
        Assert.False(XapiFormats.IsSha2(new string('0', 40)));
    }

    [Fact]
    public void Timestamps_AreReturnedToTheMillisecondInUtc()
    {
        var value = new DateTime(2026, 11, 2, 12, 0, 1, DateTimeKind.Utc).AddTicks(1_234_567);

        Assert.Equal("2026-11-02T12:00:01.123Z", XapiFormats.FormatTimestamp(XapiFormats.ToMilliseconds(value)));
    }
}
