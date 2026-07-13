using System.Buffers.Binary;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal static class AsteriskRtpPacketCodec
{
    private const int HeaderLength = 12;
    private const byte Version = 2;

    public static byte[] CreatePacket(
        ReadOnlySpan<byte> payload,
        ushort sequenceNumber,
        uint timestamp,
        uint synchronizationSource)
    {
        var packet = new byte[HeaderLength + payload.Length];
        packet[0] = Version << 6;
        packet[1] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), sequenceNumber);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4), timestamp);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(8), synchronizationSource);
        payload.CopyTo(packet.AsSpan(HeaderLength));

        return packet;
    }

    public static bool TryReadPayload(
        ReadOnlyMemory<byte> packet,
        out ushort sequenceNumber,
        out ReadOnlyMemory<byte> payload)
    {
        sequenceNumber = 0;
        payload = default;

        if (packet.Length < HeaderLength)
        {
            return false;
        }

        var span = packet.Span;

        if (span[0] >> 6 != Version)
        {
            return false;
        }

        if ((span[1] & 0x7F) != 0)
        {
            return false;
        }

        var contributingSourceCount = span[0] & 0x0F;
        var headerLength = HeaderLength + (contributingSourceCount * sizeof(uint));

        if ((span[0] & 0x10) != 0)
        {
            if (packet.Length < headerLength + sizeof(uint))
            {
                return false;
            }

            var extensionLength = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(headerLength + 2, 2));
            headerLength += sizeof(uint) + (extensionLength * sizeof(uint));
        }

        if (packet.Length < headerLength)
        {
            return false;
        }

        var payloadLength = packet.Length - headerLength;

        if ((span[0] & 0x20) != 0)
        {
            var paddingLength = span[packet.Length - 1];

            if (paddingLength == 0 || paddingLength > payloadLength)
            {
                return false;
            }

            payloadLength -= paddingLength;
        }

        sequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(2, 2));
        payload = packet.Slice(headerLength, payloadLength);

        return true;
    }
}
