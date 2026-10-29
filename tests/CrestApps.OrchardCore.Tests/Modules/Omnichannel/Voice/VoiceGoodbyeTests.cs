using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Telling a closing line from the rest of what the assistant says.
/// </summary>
/// <remarks>
/// On a call held in Greek the assistant's «Σας ευχαριστώ πολύ, γεια σας.» was not taken for a goodbye, so the end-call
/// tool asked for one and the caller heard it twice.
/// </remarks>
public sealed class VoiceGoodbyeTests
{
    [Theory]
    [InlineData("Thanks for calling, goodbye!")]
    [InlineData("Take care.")]
    [InlineData("Σας ευχαριστώ πολύ, γεια σας.")]
    [InlineData("Ευχαριστούμε για την κλήση, γεια σου!")]
    [InlineData("Αντίο σας.")]
    [InlineData("Καλή συνέχεια!")]
    [InlineData("Να είστε καλά, ευχαριστούμε που καλέσατε.")]
    [InlineData("Καλό σας βράδυ.")]
    public void SoundsLikeOne_WhenTheLineSaysGoodbye_ShouldBeTrue(string line)
    {
        // Act
        var goodbye = VoiceGoodbye.SoundsLikeOne(line);

        // Assert
        Assert.True(goodbye);
    }

    [Theory]
    [InlineData("Let me read that back to you.")]
    [InlineData("Γεια σας, είστε σε επικοινωνία με τον ψηφιακό βοηθό της εταιρείας.")]
    [InlineData("Σας συνδέω αμέσως με έναν ειδικό.")]
    [InlineData("Θα θέλατε να αφήσετε ένα μήνυμα;")]
    [InlineData("")]
    [InlineData(null)]
    public void SoundsLikeOne_WhenTheLineIsNotAGoodbye_ShouldBeFalse(string line)
    {
        // Act
        var goodbye = VoiceGoodbye.SoundsLikeOne(line);

        // Assert
        Assert.False(goodbye);
    }
}
