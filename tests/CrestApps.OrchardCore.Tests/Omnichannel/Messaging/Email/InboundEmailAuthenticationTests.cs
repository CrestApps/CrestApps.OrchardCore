using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class InboundEmailAuthenticationTests
{
    [Theory]
    [InlineData("mx.google.com; dkim=pass header.i=@contoso.com; spf=pass smtp.mailfrom=contoso.com; dmarc=pass (p=REJECT sp=REJECT dis=NONE) header.from=contoso.com", "forms@contoso.com", true)]
    [InlineData("spf=pass (sender IP is 203.0.113.5) smtp.mailfrom=contoso.com; dkim=pass (signature was verified) header.d=contoso.com;dmarc=pass action=none header.from=Contoso.com;compauth=pass reason=100", "\"Website\" <forms@contoso.com>", true)]
    [InlineData("mx.example.net; dmarc=fail (p=none) header.from=contoso.com", "forms@contoso.com", false)]
    [InlineData("mx.example.net; dmarc=pass header.from=evil.example", "forms@contoso.com", false)]
    [InlineData("mx.example.net; spf=pass smtp.mailfrom=contoso.com; dkim=pass header.d=contoso.com", "forms@contoso.com", false)]
    [InlineData("mx.example.net; dmarc=fail (header.from=contoso.com) header.from=evil.example", "forms@contoso.com", false)]
    [InlineData("dmarc=pass header.from=contoso.com", "forms@contoso.com", false)]
    [InlineData("", "forms@contoso.com", false)]
    public void HasDmarcPass_OnlyADmarcPassForTheFromDomainCounts(string header, string from, bool expected)
    {
        // The last two: a header with no receiving system's name first is not a valid report, and nothing reports nothing.
        Assert.Equal(expected, InboundEmailAuthentication.HasDmarcPass(header, from));
    }

    [Theory]
    [InlineData("{@contoso.com : pass}", "forms@contoso.com", true)]
    [InlineData("{@mailer.example : pass, @contoso.com : pass}", "forms@contoso.com", true)]
    [InlineData("{@mailer.example : pass}", "forms@contoso.com", false)]
    [InlineData("{@contoso.com : fail}", "forms@contoso.com", false)]
    [InlineData("none", "forms@contoso.com", false)]
    public void HasAlignedDkimPass_OnlyAPassingSignatureByTheFromDomainCounts(string dkim, string from, bool expected)
    {
        Assert.Equal(expected, InboundEmailAuthentication.HasAlignedDkimPass(dkim, from));
    }
}
