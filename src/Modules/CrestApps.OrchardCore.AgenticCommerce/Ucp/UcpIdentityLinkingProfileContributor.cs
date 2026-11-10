using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Offers UCP account linking: the store's OpenID server is the OAuth authorization server, and the scopes say what a
/// linked agent may do for the buyer.
/// </summary>
public sealed class UcpIdentityLinkingProfileContributor : IUcpProfileContributor
{
    /// <inheritdoc />
    public Task ContributeAsync(UcpProfileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Capabilities.Add(new UcpCapability
        {
            Name = UcpConstants.Capabilities.IdentityLinking,
            Version = context.Version,
            Spec = UcpConstants.Url(context.Version, "specification/common/identity-linking"),
            Schema = UcpConstants.Url(context.Version, "schemas/common/identity_linking.json"),
            Config = new JsonObject
            {
                ["scopes"] = new JsonObject
                {
                    [UcpIdentityScopes.CheckoutManage] = new JsonObject
                    {
                        ["description"] = new JsonObject { ["plain"] = "Check out as you, with your saved addresses." },
                    },
                    [UcpIdentityScopes.OrderRead] = new JsonObject
                    {
                        ["description"] = new JsonObject { ["plain"] = "See your orders." },
                    },
                },
            },
        });

        return Task.CompletedTask;
    }
}
