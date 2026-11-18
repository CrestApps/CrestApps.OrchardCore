using System.Buffers.Binary;

namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// A PNG or JPEG image to draw on a PDF page, with its size in pixels read from its file.
/// </summary>
public sealed class PdfImage
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private PdfImage(
        byte[] data,
        PdfImageFormat format,
        int width,
        int height)
    {
        Data = data;
        Format = format;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Gets the file.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets the format of the file.
    /// </summary>
    public PdfImageFormat Format { get; }

    /// <summary>
    /// Gets the width of the image, in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the image, in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Reads a PNG or JPEG file, from its content rather than its name.
    /// </summary>
    /// <param name="data">The file.</param>
    /// <param name="image">The image, when the file is a PNG or JPEG file whose size can be read.</param>
    /// <returns><see langword="true"/> when the file is an image that can be drawn.</returns>
    public static bool TryRead(byte[] data, out PdfImage image)
    {
        image = null;

        if (data is null || data.Length < 24)
        {
            return false;
        }

        if (data.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
        {
            // The IHDR chunk comes first: its length, its type, then the width and height.
            if (data[12] != (byte)'I' || data[13] != (byte)'H' || data[14] != (byte)'D' || data[15] != (byte)'R')
            {
                return false;
            }

            var width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4));
            var height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4));

            return Create(data, PdfImageFormat.Png, width, height, out image);
        }

        if (data[0] == 0xFF && data[1] == 0xD8)
        {
            return TryReadJpegSize(data, out var width, out var height) &&
                Create(data, PdfImageFormat.Jpeg, width, height, out image);
        }

        return false;
    }

    private static bool Create(byte[] data, PdfImageFormat format, int width, int height, out PdfImage image)
    {
        image = width > 0 && height > 0
            ? new PdfImage(data, format, width, height)
            : null;

        return image is not null;
    }

    // Walks the segments of a JPEG file to its start-of-frame segment, which holds the size of the image.
    private static bool TryReadJpegSize(byte[] data, out int width, out int height)
    {
        width = 0;
        height = 0;

        var position = 2;

        while (position + 3 < data.Length)
        {
            if (data[position] != 0xFF)
            {
                return false;
            }

            var marker = data[position + 1];

            if (marker == 0xFF)
            {
                position++;

                continue;
            }

            // Markers without a length: the start of the image, a temporary marker and the restart markers.
            if (marker is 0xD8 or 0x01 or >= 0xD0 and <= 0xD7)
            {
                position += 2;

                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 2, 2));

            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                if (position + 9 > data.Length)
                {
                    return false;
                }

                height = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 5, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 7, 2));

                return true;
            }

            if (length < 2)
            {
                return false;
            }

            position += 2 + length;
        }

        return false;
    }
}
