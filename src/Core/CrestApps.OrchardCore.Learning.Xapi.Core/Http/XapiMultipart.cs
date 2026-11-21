using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Http;

/// <summary>
/// A part of a <c>multipart/mixed</c> body.
/// </summary>
public sealed class XapiPart
{
    /// <summary>Gets the headers of the part, by name ignoring case.</summary>
    public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the content.</summary>
    public byte[] Content { get; init; } = [];

    /// <summary>
    /// Gets a header, or an empty string.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The value.</returns>
    public string Header(string name)
        => Headers.TryGetValue(name, out var value) ? value ?? string.Empty : string.Empty;
}

/// <summary>
/// Reads and writes the <c>multipart/mixed</c> bodies xAPI uses for statements with attachments (RFC 2046): the
/// statements in the first part, then one part per attachment with its SHA-2 hash in <c>X-Experience-API-Hash</c>.
/// </summary>
public static class XapiMultipart
{
    /// <summary>
    /// Reads the parts of a body.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="contentType">The content type, with its <c>boundary</c> parameter.</param>
    /// <returns>The parts, or <see langword="null"/> when the body is not a valid multipart body.</returns>
    public static IReadOnlyList<XapiPart> Parse(byte[] body, string contentType)
    {
        ArgumentNullException.ThrowIfNull(body);

        var boundary = Boundary(contentType);

        if (string.IsNullOrEmpty(boundary))
        {
            return null;
        }

        var delimiter = Encoding.ASCII.GetBytes("--" + boundary);
        var parts = new List<XapiPart>();
        var position = IndexOf(body, delimiter, 0);

        if (position < 0)
        {
            return null;
        }

        while (true)
        {
            var afterDelimiter = position + delimiter.Length;

            // The closing delimiter ends with two hyphens.
            if (afterDelimiter + 1 < body.Length && body[afterDelimiter] == '-' && body[afterDelimiter + 1] == '-')
            {
                break;
            }

            var lineEnd = IndexOf(body, "\r\n"u8.ToArray(), afterDelimiter);

            if (lineEnd < 0)
            {
                return null;
            }

            var partStart = lineEnd + 2;
            var next = IndexOf(body, Concat("\r\n"u8.ToArray(), delimiter), partStart);

            if (next < 0)
            {
                return null;
            }

            var part = ReadPart(body.AsSpan(partStart, next - partStart));

            if (part is null)
            {
                return null;
            }

            parts.Add(part);
            position = next + 2;
        }

        return parts.Count == 0 ? null : parts;
    }

    /// <summary>
    /// Writes parts as a body.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The body and its content type.</returns>
    public static (byte[] Body, string ContentType) Write(IReadOnlyList<XapiPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var boundary = "xapi-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        using var body = new MemoryStream();

        foreach (var part in parts)
        {
            Append(body, "--" + boundary + "\r\n");

            foreach (var (name, value) in part.Headers)
            {
                Append(body, name + ": " + value + "\r\n");
            }

            Append(body, "\r\n");
            body.Write(part.Content);
            Append(body, "\r\n");
        }

        Append(body, "--" + boundary + "--\r\n");

        return (body.ToArray(), "multipart/mixed; boundary=" + boundary);
    }

    /// <summary>
    /// Computes the SHA-2 hash of a content, of the length a declared hash has.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="hexLength">The length of the hash in hex digits: 56, 64, 96 or 128.</param>
    /// <returns>The hash in lower case hex, or <see langword="null"/> for another length.</returns>
    public static string Sha2(byte[] content, int hexLength)
    {
        ArgumentNullException.ThrowIfNull(content);

        return hexLength switch
        {
            56 => Sha224(content),
            64 => Convert.ToHexStringLower(SHA256.HashData(content)),
            96 => Convert.ToHexStringLower(SHA384.HashData(content)),
            128 => Convert.ToHexStringLower(SHA512.HashData(content)),
            _ => null,
        };
    }

    // .NET has no SHA-224, so a declared SHA-224 hash can never match; an attachment needs SHA-256 or longer.
    private static string Sha224(byte[] content)
        => null;

    private static XapiPart ReadPart(ReadOnlySpan<byte> part)
    {
        var headerEnd = part.IndexOf("\r\n\r\n"u8);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (headerEnd < 0)
        {
            // A part may have no headers at all: an empty line, then the content.
            if (!part.StartsWith("\r\n"u8))
            {
                return null;
            }

            return new XapiPart { Headers = headers, Content = part[2..].ToArray() };
        }

        foreach (var line in Encoding.UTF8.GetString(part[..headerEnd]).Split("\r\n"))
        {
            var separator = line.IndexOf(':');

            if (separator <= 0)
            {
                return null;
            }

            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return new XapiPart { Headers = headers, Content = part[(headerEnd + 4)..].ToArray() };
    }

    private static string Boundary(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return null;
        }

        foreach (var parameter in contentType.Split(';').Skip(1))
        {
            var separator = parameter.IndexOf('=');

            if (separator > 0 && string.Equals(parameter[..separator].Trim(), "boundary", StringComparison.OrdinalIgnoreCase))
            {
                return parameter[(separator + 1)..].Trim().Trim('"');
            }
        }

        return null;
    }

    private static int IndexOf(byte[] source, byte[] pattern, int start)
    {
        if (start >= source.Length)
        {
            return -1;
        }

        var index = source.AsSpan(start).IndexOf(pattern);

        return index < 0 ? -1 : index + start;
    }

    private static byte[] Concat(byte[] first, byte[] second)
        => [.. first, .. second];

    private static void Append(MemoryStream stream, string text)
        => stream.Write(Encoding.UTF8.GetBytes(text));
}
