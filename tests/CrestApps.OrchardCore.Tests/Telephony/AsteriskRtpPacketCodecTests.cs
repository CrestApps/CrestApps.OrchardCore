using CrestApps.OrchardCore.Asterisk.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskRtpPacketCodecTests
{
    [Fact]
    public void CreatePacket_ThenReadPayload_RoundTripsAudioAndSequence()
    {
        // Arrange
        byte[] audio = [1, 2, 3, 4, 5];

        // Act
        var packet = AsteriskRtpPacketCodec.CreatePacket(
            audio,
            sequenceNumber: 42,
            timestamp: 160,
            synchronizationSource: 123);
        var succeeded = AsteriskRtpPacketCodec.TryReadPayload(
            packet,
            out var sequenceNumber,
            out var payload);

        // Assert
        Assert.True(succeeded);
        Assert.Equal(42, sequenceNumber);
        Assert.Equal(audio, payload.ToArray());
    }

    [Fact]
    public void TryReadPayload_WhenPacketHasInvalidVersion_ReturnsFalse()
    {
        // Arrange
        var packet = new byte[12];

        // Act
        var succeeded = AsteriskRtpPacketCodec.TryReadPayload(
            packet,
            out _,
            out _);

        // Assert
        Assert.False(succeeded);
    }

    [Fact]
    public void TryReadPayload_WhenPacketIsShorterThanHeader_ReturnsFalse()
    {
        // Arrange
        var packet = new byte[11];

        // Act
        var succeeded = AsteriskRtpPacketCodec.TryReadPayload(
            packet,
            out _,
            out _);

        // Assert
        Assert.False(succeeded);
    }

    [Fact]
    public void TryReadPayload_WhenPayloadTypeIsNotMuLaw_ReturnsFalse()
    {
        // Arrange
        var packet = AsteriskRtpPacketCodec.CreatePacket(
            [1, 2],
            sequenceNumber: 1,
            timestamp: 160,
            synchronizationSource: 123);
        packet[1] = 8;

        // Act
        var succeeded = AsteriskRtpPacketCodec.TryReadPayload(
            packet,
            out _,
            out _);

        // Assert
        Assert.False(succeeded);
    }

    [Fact]
    public void TryReadPayload_WhenPacketHasHeaderExtension_ReadsPayload()
    {
        // Arrange
        byte[] packet =
        [
            0x90, 0x00,
            0x00, 0x07,
            0x00, 0x00, 0x00, 0xA0,
            0x00, 0x00, 0x00, 0x01,
            0x10, 0x00,
            0x00, 0x01,
            0x00, 0x00, 0x00, 0x00,
            0x11, 0x22,
        ];

        // Act
        var succeeded = AsteriskRtpPacketCodec.TryReadPayload(
            packet,
            out var sequenceNumber,
            out var payload);

        // Assert
        Assert.True(succeeded);
        Assert.Equal(7, sequenceNumber);
        Assert.Equal(new byte[] { 0x11, 0x22 }, payload.ToArray());
    }

    [Fact]
    public void TryReadPayload_WhenPacketHasContributingSources_ReadsPayload()
    {
        // Arrange
        byte[] packet =
        [
            0x82, 0x00,
            0x00, 0x08,
            0x00, 0x00, 0x00, 0xA0,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x02,
            0x00, 0x00, 0x00, 0x03,
            0x33, 0x44,
        ];

        // Act
        var succeeded = AsteriskRtpPacketCodec.TryReadPayload(
            packet,
            out var sequenceNumber,
            out var payload);

        // Assert
        Assert.True(succeeded);
        Assert.Equal(8, sequenceNumber);
        Assert.Equal(new byte[] { 0x33, 0x44 }, payload.ToArray());
    }

    [Fact]
    public void TryReadPayload_WhenPacketHasPadding_RemovesPadding()
    {
        // Arrange
        byte[] packet =
        [
            0xA0, 0x00,
            0x00, 0x09,
            0x00, 0x00, 0x00, 0xA0,
            0x00, 0x00, 0x00, 0x01,
            0x55, 0x66,
            0x00, 0x02,
        ];

        // Act
        var succeeded = AsteriskRtpPacketCodec.TryReadPayload(
            packet,
            out _,
            out var payload);

        // Assert
        Assert.True(succeeded);
        Assert.Equal(new byte[] { 0x55, 0x66 }, payload.ToArray());
    }

    [Fact]
    public void TryReadPayload_WhenExtensionLengthExceedsPacket_ReturnsFalse()
    {
        // Arrange
        byte[] packet =
        [
            0x90, 0x00,
            0x00, 0x0A,
            0x00, 0x00, 0x00, 0xA0,
            0x00, 0x00, 0x00, 0x01,
            0x10, 0x00,
            0x00, 0x02,
            0x00, 0x00, 0x00, 0x00,
        ];

        // Act
        var succeeded = AsteriskRtpPacketCodec.TryReadPayload(
            packet,
            out _,
            out _);

        // Assert
        Assert.False(succeeded);
    }

    [Fact]
    public void TryReadPayload_WhenPaddingLengthIsInvalid_ReturnsFalse()
    {
        // Arrange
        byte[] packet =
        [
            0xA0, 0x00,
            0x00, 0x0B,
            0x00, 0x00, 0x00, 0xA0,
            0x00, 0x00, 0x00, 0x01,
            0x11, 0x03,
        ];

        // Act
        var succeeded = AsteriskRtpPacketCodec.TryReadPayload(
            packet,
            out _,
            out _);

        // Assert
        Assert.False(succeeded);
    }
}
