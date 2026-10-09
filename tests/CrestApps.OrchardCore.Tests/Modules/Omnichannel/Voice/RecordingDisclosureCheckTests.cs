using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Whether a live session's opening line gave the tenant's recording disclosure word for word. A legal notice is the
/// approved words, so only differences speech transcription cannot keep (case, punctuation) are forgiven.
/// </summary>
public sealed class RecordingDisclosureCheckTests
{
    private const string Disclosure = "This call may be recorded for quality assurance and training purposes.";

    [Theory]
    [InlineData("This call may be recorded for quality assurance and training purposes. Hey Jack, this is Sarah.")]
    [InlineData("this call may be recorded for quality assurance, and training purposes")]
    [InlineData("Hi! THIS CALL MAY BE RECORDED FOR QUALITY ASSURANCE AND TRAINING PURPOSES. How are you?")]
    public void TheWholeDisclosure_IsFound_WhateverTheCaseAndPunctuation(string openingLine)
    {
        Assert.True(RecordingDisclosureCheck.WasSaid(Disclosure, openingLine));
    }

    [Theory]
    [InlineData("Just so you know, this call is recorded. Hey Jack!")]
    [InlineData("This call may be recorded for quality and training purposes.")]
    [InlineData("This call may be recorded for quality assurance and training")]
    [InlineData("This call may be recorded for quality assurance and training purposesless")]
    [InlineData("")]
    [InlineData(null)]
    public void AParaphraseAShortenedNoticeOrNothing_IsNotTheDisclosure(string openingLine)
    {
        Assert.False(RecordingDisclosureCheck.WasSaid(Disclosure, openingLine));
    }

    [Fact]
    public void AnApostrophe_DoesNotSplitAWord()
    {
        Assert.True(RecordingDisclosureCheck.WasSaid("We're recording this call.", "We’re recording this call. Hi!"));
    }

    [Fact]
    public void AnEmptyDisclosure_IsNeverFound()
    {
        Assert.False(RecordingDisclosureCheck.WasSaid("  ", "Anything at all."));
    }
}
