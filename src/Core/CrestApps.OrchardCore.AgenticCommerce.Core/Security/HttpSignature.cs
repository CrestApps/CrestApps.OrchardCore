namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// The two header values of an HTTP message signature (RFC 9421).
/// </summary>
/// <param name="SignatureInput">The <c>Signature-Input</c> header value, such as <c>sig1=("@method" "@path");keyid="k"</c>.</param>
/// <param name="Signature">The <c>Signature</c> header value, such as <c>sig1=:&lt;base64&gt;:</c>.</param>
public sealed record HttpSignature(string SignatureInput, string Signature);
