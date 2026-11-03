using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// What every automated call tells the model about ending it.
/// </summary>
public sealed class VoiceCallGuidanceTests
{
    [Fact]
    public void OnlyAClearYes_ConfirmsDetailsReadBack()
    {
        // Live, a model read an email address back wrongly, the reply came through garbled, and it was taken for a
        // yes: the customer was thanked and the call hung up with the wrong address.
        Assert.Contains("only a clear yes", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
        Assert.Contains("was that a yes?", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
        Assert.Contains("read the corrected details back", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
    }

    [Fact]
    public void AConfirmedDetail_IsNotReadBackAgain_AndAGoodbyeGetsAGoodbye()
    {
        // Live, a customer said yes to their email being read back; when they then said "Bye." the model read the email
        // back and asked again instead of saying goodbye.
        Assert.Contains("Once the customer has said yes to a detail, it is confirmed: never read it back or ask about it again", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
        Assert.Contains("When the customer says goodbye, the conversation has finished: say goodbye back and end the call", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
        Assert.Contains("do not ask them another question", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);

        // The yes that confirms it is still only a clear one.
        Assert.Contains("only a clear yes", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatTheModelSays_NeedsNoConfirmation_AndIsNeverMadeUp()
    {
        // Live, a model gave the customer a phone number it had made up, then asked four times whether it was right.
        Assert.Contains("details the customer gave you", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
        Assert.Contains("Information you give the customer needs no confirmation", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
        Assert.Contains("Never make up a phone number", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsBookedOrCheckedLater_UnlessAToolDoesIt()
    {
        // Live, a profile with no booking tool said it would confirm availability and get back to the customer, went
        // silent, and then told them the appointment was booked.
        Assert.Contains("Never tell the customer something is booked, confirmed or done unless one of your tools did it", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
        Assert.Contains("never say you will check something and come back to them", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);

        // Live again, with that rule: the profile said "you book appointments", and that won.
        Assert.Contains("even when your instructions say booking, scheduling or confirming is your job", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
    }

    [Fact]
    public void ACustomer_IsGreetedByTheirFirstName()
    {
        // Live, a contact stored as "Mike Demo" was greeted "Hi Mike Demo".
        var guidance = VoiceCallGuidance.WhoYouAreCalling("  Mike Demo ");

        Assert.Contains("You are calling Mike Demo.", guidance, StringComparison.Ordinal);
        Assert.Contains("by their first name, Mike, never by their full name", guidance, StringComparison.Ordinal);
        Assert.Contains("Greet them as Amani.", VoiceCallGuidance.WhoYouAreCalling("Amani"), StringComparison.Ordinal);
        Assert.Contains("never guess or invent one", VoiceCallGuidance.WhoYouAreCalling(null), StringComparison.Ordinal);
    }

    [Fact]
    public void ARecordedLiveCall_IsOpenedWithTheDisclosureAsItsFirstWords()
    {
        // Live, a realtime call told the disclosure only in its session instructions opened with its own words for it.
        var opening = VoiceCallGuidance.OpeningWithDisclosure(" This call may be recorded. ");

        Assert.Contains("first words must be exactly these", opening, StringComparison.Ordinal);
        Assert.Contains("\"This call may be recorded.\"", opening, StringComparison.Ordinal);
        Assert.Null(VoiceCallGuidance.OpeningWithDisclosure(" "));
    }

    [Fact]
    public void APhoneMenu_IsAnsweredWithOneKey_AndThePersonAfterItIsGreeted()
    {
        // Live, the model pressed the key it needed and then the key for hearing the menu again.
        Assert.Contains("Press only that key, once", VoiceCallGuidance.PhoneMenusAndScreening, StringComparison.Ordinal);
        Assert.Contains("When a person answers after the menu, greet them", VoiceCallGuidance.PhoneMenusAndScreening, StringComparison.Ordinal);
    }

    [Fact]
    public void ACallbackWithoutATime_IsAskedWhenBeforeTheGoodbye()
    {
        // Live, a customer said "can you call me later?" and the model said goodbye and hung up without asking when.
        Assert.Contains("without saying when, ask once", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGoodbye_IsOneSentence_WithNoAnnouncementBeforeIt()
    {
        // Live, a confirmed email was followed by "let me just wrap this up with you" and a two-sentence goodbye:
        // twelve seconds of talking before the line could drop.
        Assert.Contains("one short, warm closing sentence", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
        Assert.Contains("Do not say you are wrapping up", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
    }

    [Fact]
    public void NotRightNow_IsAnsweredWithAnOfferToCallBack()
    {
        // Live, "no, not right now" was wished a good day and closed as finished, so the lead was never called again.
        Assert.Contains("now is not a good time, offer to call them back", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRecordingDisclosure_IsQuotedWordForWord_AsTheStartOfTheOpening()
    {
        // A legal notice is the approved words: asked only to mention the recording, a model paraphrases it.
        var guidance = VoiceCallGuidance.RecordingDisclosure("  This call may be recorded for training.  ");

        Assert.Contains("\"This call may be recorded for training.\"", guidance, StringComparison.Ordinal);
        Assert.Contains("word for word", guidance, StringComparison.Ordinal);
        Assert.Contains("Your opening must begin", guidance, StringComparison.Ordinal);
        Assert.Contains("start your next reply with the whole sentence again", guidance, StringComparison.Ordinal);
        Assert.Contains("never say it again", guidance, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void NoRecordingDisclosure_GivesNoGuidance(string disclosure)
    {
        Assert.Null(VoiceCallGuidance.RecordingDisclosure(disclosure));
    }

    [Fact]
    public void TheVoicemailMessage_IsOneShortMessageWithNoQuestions_InTheLanguageOfTheCall()
    {
        // Live, the model asked the voicemail whether the customer wanted a new or used vehicle. A live session leaves
        // the message in its own voice, so the instruction names no language and leaves it in the call's.
        Assert.Contains("one to three short sentences", VoiceCallGuidance.LeavingAVoicemail, StringComparison.Ordinal);
        Assert.Contains("do not ask any questions", VoiceCallGuidance.LeavingAVoicemail, StringComparison.Ordinal);
        Assert.Contains("say you will try them again", VoiceCallGuidance.LeavingAVoicemail, StringComparison.Ordinal);
        Assert.Contains("in the language you have been speaking on this call", VoiceCallGuidance.LeavingAVoicemail, StringComparison.Ordinal);
        Assert.DoesNotContain("English", VoiceCallGuidance.LeavingAVoicemail, StringComparison.Ordinal);

        // And it is the one instruction both ways of holding a call share.
        Assert.EndsWith(VoiceCallGuidance.LeavingAVoicemail, VoiceAgentConversationLoop.LeavingAVoicemail, StringComparison.Ordinal);
    }
}
