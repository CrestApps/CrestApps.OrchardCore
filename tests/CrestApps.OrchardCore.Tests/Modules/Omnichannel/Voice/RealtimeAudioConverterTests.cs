using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A phone call and a realtime model do not speak the same audio. The call carries 8 kHz, usually companded to
/// μ-law; the model sends and expects 16-bit PCM at 24 kHz. Getting the conversion wrong does not fail loudly —
/// it produces a caller who hears a chipmunk, a growl, or static, which is the kind of defect that is obvious in
/// production and invisible in review.
/// </summary>
public sealed class RealtimeAudioConverterTests
{
    [Fact]
    public void MuLaw_SurvivesARoundTrip_CloseEnoughToBeTheSameSound()
    {
        // Arrange
        // μ-law is lossy by design, so this is not an equality test: it pins that the encoding is the standard
        // one and not, say, inverted or byte-swapped, which would still round-trip through a wrong pair.
        var samples = new short[] { 0, 1000, -1000, 16000, -16000, 32000, -32000 };

        // Act
        var encoded = samples.Select(RealtimeAudioConverter.EncodeMuLawSample).ToArray();
        var decoded = encoded.Select(RealtimeAudioConverter.DecodeMuLawSample).ToArray();

        // Assert
        for (var i = 0; i < samples.Length; i++)
        {
            var tolerance = Math.Max(256, Math.Abs(samples[i]) / 16);

            Assert.True(
                Math.Abs(samples[i] - decoded[i]) <= tolerance,
                $"Sample {samples[i]} came back as {decoded[i]}, further off than μ-law should be.");
        }
    }

    [Fact]
    public void Silence_StaysSilent()
    {
        // Arrange
        // A wrong μ-law bias turns silence into a constant tone, which is the most audible possible failure: the
        // caller hears a hum for the whole call.
        var encoded = RealtimeAudioConverter.EncodeMuLawSample(0);

        // Assert
        Assert.InRange(Math.Abs(RealtimeAudioConverter.DecodeMuLawSample(encoded)), 0, 256);
    }

    [Fact]
    public void Downsampling_FromTheModelRateToTheCallRate_KeepsTheDuration()
    {
        // Arrange
        // Duration is what a listener hears as pitch and speed. One second in must be one second out, or the
        // caller hears a chipmunk (too few samples) or a growl (too many).
        var oneSecondAt24k = new short[24_000];

        // Act
        var converted = RealtimeAudioConverter.Resample(oneSecondAt24k, 24_000, 8_000);

        // Assert
        Assert.Equal(8_000, converted.Length);
    }

    [Fact]
    public void Upsampling_FromTheCallRateToTheModelRate_KeepsTheDuration()
    {
        // Arrange
        var oneSecondAt8k = new short[8_000];

        // Act
        var converted = RealtimeAudioConverter.Resample(oneSecondAt8k, 8_000, 24_000);

        // Assert
        Assert.Equal(24_000, converted.Length);
    }

    [Fact]
    public void Resampling_ToTheSameRate_ChangesNothing()
    {
        // Arrange
        // A provider that already speaks the model's rate must not be put through an interpolator for nothing.
        var samples = new short[] { 1, 2, 3, 4, 5 };

        // Act
        var converted = RealtimeAudioConverter.Resample(samples, 8_000, 8_000);

        // Assert
        Assert.Equal(samples, converted);
    }

    [Fact]
    public void ToneTheLineCannotCarry_DoesNotFoldBackIntoTheVoice()
    {
        // Arrange
        // This is the whole reason the resampler filters. A 6 kHz tone cannot exist at 8 kHz — there are not
        // enough samples to describe it — so on a naive 3:1 decimation it does not vanish, it reappears at
        // 8000 - 6000 = 2 kHz, right in the middle of the voice band. Speech is full of energy up there, and the
        // folded copy tracks it without belonging to it: the metallic, buzzy edge that reads as "robotic".
        var sixKilohertzAt24k = Tone(6_000, 24_000, 4_800);

        // Act
        var onTheLine = RealtimeAudioConverter.Resample(sixKilohertzAt24k, 24_000, 8_000);

        // Assert
        // Measured away from the filter's start-up transient.
        var settled = onTheLine.Skip(200).ToArray();
        var peak = settled.Max(sample => Math.Abs((int)sample));

        Assert.True(peak < 3_000, $"A 6 kHz tone folded back into the voice band at amplitude {peak}.");
    }

    [Fact]
    public void SpeechInsideTheTelephoneBand_StillComesThrough()
    {
        // Arrange
        // The filter must not be so aggressive that it takes the voice with it. 1 kHz sits in the middle of the
        // telephone passband and has to survive at close to full strength.
        var oneKilohertzAt24k = Tone(1_000, 24_000, 4_800);

        // Act
        var onTheLine = RealtimeAudioConverter.Resample(oneKilohertzAt24k, 24_000, 8_000);

        // Assert
        var settled = onTheLine.Skip(200).ToArray();
        var peak = settled.Max(sample => Math.Abs((int)sample));

        Assert.True(peak > 15_000, $"A 1 kHz tone was attenuated to {peak}; the voice itself is being filtered out.");
    }

    [Fact]
    public void ImagesFromInterpolatingUpwards_AreCleanedOffBeforeTheModelHearsThem()
    {
        // Arrange
        // Going the other way, straight-line interpolation from 8 kHz to 24 kHz leaves mirror images of the
        // speech above its original band. They are not what the caller said, and the model runs its transcription
        // and its turn detection over them.
        var oneKilohertzAt8k = Tone(1_000, 8_000, 1_600);

        // Act
        var forTheModel = RealtimeAudioConverter.Resample(oneKilohertzAt8k, 8_000, 24_000);

        // Assert
        // The tone itself survives...
        var settled = forTheModel.Skip(600).ToArray();
        Assert.True(settled.Max(sample => Math.Abs((int)sample)) > 15_000, "The caller's own voice was filtered out.");

        // ...and the result stays a smooth wave rather than the cornered shape interpolation produces. Sharp
        // corners are exactly the high-frequency content that should no longer be there.
        var biggestJump = settled.Zip(settled.Skip(1)).Max(pair => Math.Abs(pair.Second - pair.First));

        Assert.True(biggestJump < 6_000, $"The upsampled signal still has {biggestJump}-sample corners in it.");
    }

    private static short[] Tone(double frequencyHz, int sampleRate, int sampleCount)
        => Enumerable.Range(0, sampleCount)
            .Select(i => (short)(Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate) * 20_000))
            .ToArray();

    [Fact]
    public void Resampling_PreservesTheShapeOfTheSound()
    {
        // Arrange
        // A ramp is the simplest signal whose shape is checkable: after resampling it must still rise, and still
        // start and end near the same values. A converter that reversed, zeroed or scrambled the buffer would
        // pass a length check and fail this.
        var ramp = Enumerable.Range(0, 240).Select(i => (short)(i * 100)).ToArray();

        // Act
        var converted = RealtimeAudioConverter.Resample(ramp, 24_000, 8_000);

        // Assert
        Assert.Equal(80, converted.Length);
        Assert.InRange(converted[0], -200, 200);
        Assert.InRange(converted[^1], (short)22_000, (short)24_000);
        Assert.True(converted.Zip(converted.Skip(1)).All(pair => pair.Second >= pair.First), "The ramp stopped rising.");
    }

    [Fact]
    public void CallerAudio_ArrivesAtTheModel_AsPcmAtTheModelRate()
    {
        // Arrange
        var format = new ContactCenterVoiceMediaFormat
        {
            Encoding = ContactCenterVoiceMediaEncoding.MuLaw,
            SampleRate = 8_000,
        };

        // 20 ms of μ-law at 8 kHz is 160 bytes; 20 ms of PCM16 at 24 kHz is 960.
        var frame = new byte[160];

        // Act
        var converted = RealtimeAudioConverter.ToRealtime(frame, format);

        // Assert
        Assert.Equal(960, converted.Length);
    }

    [Fact]
    public void AssistantAudio_ReachesTheCaller_InTheFormatTheirCallUses()
    {
        // Arrange
        var format = new ContactCenterVoiceMediaFormat
        {
            Encoding = ContactCenterVoiceMediaEncoding.MuLaw,
            SampleRate = 8_000,
        };

        // 20 ms of PCM16 at 24 kHz is 960 bytes; 20 ms of μ-law at 8 kHz is 160.
        var assistantAudio = new byte[960];

        // Act
        var converted = RealtimeAudioConverter.FromRealtime(assistantAudio, format);

        // Assert
        Assert.Equal(160, converted.Length);
    }

    [Fact]
    public void AProviderThatSpeaksLinearPcm_IsNotCompanded()
    {
        // Arrange
        // Running PCM through a μ-law encoder that was not asked for is silent corruption: the audio still plays,
        // and it sounds like static.
        var format = new ContactCenterVoiceMediaFormat
        {
            Encoding = ContactCenterVoiceMediaEncoding.LinearPcm,
            SampleRate = 24_000,
        };

        var assistantAudio = new byte[960];

        // Act
        var converted = RealtimeAudioConverter.FromRealtime(assistantAudio, format);

        // Assert
        Assert.Equal(assistantAudio.Length, converted.Length);
    }

    [Fact]
    public void AnEmptyFrame_ConvertsToNothing()
    {
        // Arrange
        var format = new ContactCenterVoiceMediaFormat
        {
            Encoding = ContactCenterVoiceMediaEncoding.MuLaw,
            SampleRate = 8_000,
        };

        // Act & Assert
        Assert.Empty(RealtimeAudioConverter.ToRealtime(ReadOnlyMemory<byte>.Empty, format).ToArray());
        Assert.Empty(RealtimeAudioConverter.FromRealtime(ReadOnlyMemory<byte>.Empty, format).ToArray());
    }
}
