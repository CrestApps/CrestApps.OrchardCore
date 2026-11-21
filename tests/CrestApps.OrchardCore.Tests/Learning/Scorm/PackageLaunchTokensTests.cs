using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class PackageLaunchTokensTests
{
    private static readonly PackageLaunch _launch = new("launch1", "enrollment1", "package1", "file1", "user1");

    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly TestClock _clock = new() { UtcNow = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc) };

    [Fact]
    public void Token_ReadsBackItsLaunch_AndIsSafeInAPath()
    {
        var tokens = new PackageLaunchTokens(_protection, _clock);
        var token = tokens.Create(_launch);

        Assert.Equal(_launch, tokens.Read(token));
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void TamperedToken_IsRefused()
    {
        var tokens = new PackageLaunchTokens(_protection, _clock);
        var token = tokens.Create(_launch);
        var middle = token.Length / 2;
        var tampered = token[..middle] + (token[middle] == 'A' ? 'B' : 'A') + token[(middle + 1)..];

        Assert.Null(tokens.Read(tampered));
        Assert.Null(tokens.Read(token[..^2]));
        Assert.Null(tokens.Read("not-a-token"));
        Assert.Null(tokens.Read(string.Empty));
        Assert.Null(tokens.Read(null));
    }

    [Fact]
    public void ExpiredToken_IsRefused()
    {
        var tokens = new PackageLaunchTokens(_protection, _clock);
        var token = tokens.Create(_launch);

        _clock.UtcNow = _clock.UtcNow + PackageLaunchTokens.Lifetime - TimeSpan.FromMinutes(1);
        Assert.NotNull(tokens.Read(token));

        _clock.UtcNow = _clock.UtcNow.AddMinutes(2);
        Assert.Null(tokens.Read(token));
    }

    [Fact]
    public void TokenOfAnotherKeyRing_IsRefused()
    {
        var token = new PackageLaunchTokens(new EphemeralDataProtectionProvider(), _clock).Create(_launch);

        Assert.Null(new PackageLaunchTokens(_protection, _clock).Read(token));
    }

    [Fact]
    public void ProtectedFileLinkOfTheSameKeyRing_IsNotALaunchToken()
    {
        var other = _protection.CreateProtector("CrestApps.OrchardCore.ProtectedFiles.LearningPackages").ToTimeLimitedDataProtector()
            .Protect("launch1|enrollment1|package1|file1|user1", DateTimeOffset.UtcNow.AddHours(1));

        Assert.Null(new PackageLaunchTokens(_protection, _clock).Read(other));
    }
}
