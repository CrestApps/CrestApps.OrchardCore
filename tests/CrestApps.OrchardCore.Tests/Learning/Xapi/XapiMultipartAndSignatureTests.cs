using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Validation;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

public sealed class XapiMultipartAndSignatureTests
{
    [Fact]
    public void Multipart_IsReadAsTheSpecificationShowsIt()
    {
        // The attachment example of xAPI 1.0.3, Part Two, 2.4.11.
        var body = "--abcABC0123'()+_,-./:=?\r\n" +
            "Content-Type:application/json\r\n\r\n" +
            "{\"actor\": {\"mbox\": \"mailto:sample.agent@example.com\"}}\r\n" +
            "--abcABC0123'()+_,-./:=?\r\n" +
            "Content-Type:text/plain\r\n" +
            "Content-Transfer-Encoding:binary\r\n" +
            "X-Experience-API-Hash:495395e777cd98da653df9615d09c0fd6bb2f8d4788394cd53c56a3bfdcd848a\r\n\r\n" +
            "here is a simple attachment\r\n" +
            "--abcABC0123'()+_,-./:=?--";

        var parts = XapiMultipart.Parse(Encoding.UTF8.GetBytes(body), "multipart/mixed; boundary=\"abcABC0123'()+_,-./:=?\"");

        Assert.Equal(2, parts.Count);
        Assert.Equal("application/json", parts[0].Header("Content-Type"));
        Assert.Equal("here is a simple attachment", Encoding.UTF8.GetString(parts[1].Content));
        Assert.Equal("495395e777cd98da653df9615d09c0fd6bb2f8d4788394cd53c56a3bfdcd848a", parts[1].Header("X-Experience-API-Hash"));
        Assert.Equal("495395e777cd98da653df9615d09c0fd6bb2f8d4788394cd53c56a3bfdcd848a", XapiMultipart.Sha2(parts[1].Content, 64));
    }

    [Fact]
    public void Multipart_WithoutABoundary_IsRefused()
    {
        Assert.Null(XapiMultipart.Parse(Encoding.UTF8.GetBytes("no parts"), "multipart/mixed"));
        Assert.Null(XapiMultipart.Parse(Encoding.UTF8.GetBytes("no parts"), "multipart/mixed; boundary=xyz"));
    }

    [Fact]
    public void Multipart_IsWrittenAndReadBack()
    {
        var (body, contentType) = XapiMultipart.Write(
        [
            new XapiPart { Headers = { ["Content-Type"] = "application/json" }, Content = Encoding.UTF8.GetBytes("{}") },
            new XapiPart { Headers = { ["Content-Type"] = "application/octet-stream", ["X-Experience-API-Hash"] = "abc" }, Content = [0, 1, 2, 13, 10, 45, 45] },
        ]);

        var parts = XapiMultipart.Parse(body, contentType);

        Assert.StartsWith("multipart/mixed; boundary=", contentType);
        Assert.Equal(2, parts.Count);
        Assert.Equal(new byte[] { 0, 1, 2, 13, 10, 45, 45 }, parts[1].Content);
    }

    [Fact]
    public void Signature_ThatSignsTheStatementWithItsCertificate_IsValid()
    {
        using var rsa = RSA.Create(2048);
        var statement = XapiSamples.Simple(XapiSamples.NewId());

        Assert.Null(XapiSignatures.Verify(statement, Sign(statement, rsa, "RS256", withCertificate: true)));
        Assert.Null(XapiSignatures.Verify(statement, Sign(statement, rsa, "RS512", withCertificate: false)));
    }

    [Fact]
    public void Signature_OfAnotherStatement_OrWithAnotherAlgorithm_OrBroken_IsRefused()
    {
        using var rsa = RSA.Create(2048);
        var statement = XapiSamples.Simple(XapiSamples.NewId());
        var other = XapiSamples.Simple(XapiSamples.NewId(), verb: XapiSamples.Attempted);

        Assert.NotNull(XapiSignatures.Verify(statement, Sign(other, rsa, "RS256", withCertificate: true)));
        Assert.NotNull(XapiSignatures.Verify(statement, Sign(statement, rsa, "HS256", withCertificate: false)));
        Assert.NotNull(XapiSignatures.Verify(statement, Encoding.ASCII.GetBytes("only.two")));

        // The signature does not match the certificate in its header.
        var signed = Encoding.ASCII.GetString(Sign(statement, rsa, "RS256", withCertificate: true));
        var tampered = signed[..^4] + (signed[^4] == 'A' ? "BBBB" : "AAAA");

        Assert.NotNull(XapiSignatures.Verify(statement, Encoding.ASCII.GetBytes(tampered)));
    }

    internal static byte[] Sign(JsonObject statement, RSA rsa, string algorithm, bool withCertificate)
    {
        var header = new JsonObject { ["alg"] = algorithm };

        if (withCertificate)
        {
            var request = new CertificateRequest("CN=xAPI signer", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            header["x5c"] = new JsonArray(Convert.ToBase64String(certificate.RawData));
        }

        var payload = statement.DeepClone().AsObject();

        payload.Remove("attachments");

        var signingInput = Base64Url(Encoding.UTF8.GetBytes(header.ToJsonString())) + "." + Base64Url(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        var hash = algorithm switch
        {
            "RS384" => HashAlgorithmName.SHA384,
            "RS512" => HashAlgorithmName.SHA512,
            _ => HashAlgorithmName.SHA256,
        };
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(signingInput), hash, RSASignaturePadding.Pkcs1);

        return Encoding.ASCII.GetBytes(signingInput + "." + Base64Url(signature));
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
