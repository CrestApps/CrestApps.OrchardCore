# Module scaffold for a messaging channel

Example: an **Email** channel. Replace `Email` with your channel.

## Projects

| Project | Path | Contents |
| --- | --- | --- |
| Core | `src/Core/CrestApps.OrchardCore.Omnichannel.Messaging.Email.Core` | `EmailMessagingChannel`, provider seam, webhook payload parser, inbox handler, inbound handlers. Everything testable. |
| Module | `src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging.Email` | `Manifest.cs`, `Startup.cs`, webhook endpoint, endpoint display driver + view, settings. |

Copy the csproj files of `CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Core` and
`CrestApps.OrchardCore.Omnichannel.Messaging.Sms`, and swap the provider packages. Keep
`<InternalsVisibleTo Include="CrestApps.OrchardCore.Tests" />`.

A provider-specific integration (for example a SendGrid or Meta module) may instead live in that provider's existing
module as its own feature, the way Telnyx SMS lives in the Telnyx module; the channel itself should still be one
feature that the provider features plug into.

## Manifest

```csharp
[assembly: Feature(
    Id = "CrestApps.OrchardCore.Omnichannel.Messaging.Email",
    Name = "Email Messaging Channel",
    Description = "Adds email as a channel of the Omnichannel Messaging workspace: ...",
    Category = "Contact Center",
    Dependencies =
    [
        MessagingConstants.Feature.Workspace,
        ContactCenterConstants.Feature.ProviderInbox, // durable webhook inbox
        "OrchardCore.Email",                          // the provider abstraction you send through
    ]
)]
```

Add the feature id as a constant to `MessagingConstants.Feature` (in
`src/Abstractions/CrestApps.OrchardCore.Omnichannel.Messaging.Abstractions/MessagingConstants.cs`).

## Startup

```csharp
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer) => S = stringLocalizer;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMessagingChannel<EmailMessagingChannel>();

        services.AddScoped<IProviderWebhookInboxHandler, EmailInboundInboxHandler>();
        services.AddScoped<IMessagingInboundHandler, EmailAutoResponderInboundHandler>(); // optional

        services.AddChannelEndpointSource(OmnichannelConstants.Channels.Email, source =>
        {
            source.DisplayName = S["Email"];
            source.Description = S["A mailbox that sends and receives email in the messaging workspace."];
        });

        // Only when the channel has several providers to pick per endpoint:
        // services.AddDisplayDriver<OmnichannelChannelEndpoint, EmailEndpointProviderDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.AddEmailInboundWebhookEndpoint(); // your signed webhook
}
```

What the workspace already gives your endpoints — do **not** re-implement:

- the inbound-routing editor (agent/queue target, distribution mode, auto-reply);
- normalization of the endpoint value through `NormalizeAddress` and validation through `IsValidAddress`
  (`MessagingChannelEndpointAddressPolicy`, an `IChannelEndpointAddressPolicy` the Omnichannel channel-endpoint handler consults; Phone/SMS/Email values are handled by that handler directly);
- inclusion in the composer's and broadcasts' "send from" pickers, grouped by channel.

## Solution wiring (all required, or builds and guards fail)

1. `CrestApps.OrchardCore.slnx`: add both projects (next to the SMS ones).
2. `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets/CrestApps.OrchardCore.Cms.Core.Targets.csproj`: add the module
   `ProjectReference` with `PrivateAssets="none"`.
3. `tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj`: reference both projects.
4. Architecture scan lists — add both projects next to the `Omnichannel.Messaging.Sms` entries:
   - `tests/CrestApps.OrchardCore.Tests/Architecture/FileSizeRatchetTests.cs`
   - `tests/CrestApps.OrchardCore.Tests/Architecture/UnboundedCollectionLoadTests.cs`
   - `tests/CrestApps.OrchardCore.Tests/Modules/ContactCenter/ContactCenterOperationalLogPrivacyTests.cs`
   - `tests/CrestApps.OrchardCore.Tests/Modules/ContactCenter/CallTopologyAuthorityTests.cs` (required if the project
     can reach Contact Center call types, which referencing `ContactCenter.Core` does).
   - If you add an `IProviderWebhookInboxHandler`, add one of your types to `_assemblyAnchors` in
     `Architecture/ProviderWriteIdempotencyArchitectureTests.cs`.
5. Public API baseline: create an empty
   `tests/CrestApps.OrchardCore.Tests/PublicApi/Baselines/CrestApps.OrchardCore.Omnichannel.Messaging.Email.Core.approved.txt`,
   run `PublicApiApprovalTests`, and copy the generated `.received.txt` over it after reviewing it.

## Tests

Copy the patterns in `tests/CrestApps.OrchardCore.Tests/Omnichannel/Messaging/`:

- **Channel unit tests** (like `MessagingChannelWorkspaceTests`): `NormalizeAddress` idempotence and spellings,
  `IsValidAddress`, `GetContactAddresses` ordering, `IsOptedOut`, `SendAsync` hitting a mocked provider with the
  right from/to/body and mapping refusals to `Failed`.
- **Inbound through the real processor** (like `SmsInboundProcessorTests`): build a `MessagingChannelResolver` with
  your channel, run `MessagingInboundProcessor.ProcessAsync`, assert the conversation, routing and notification
  (`Channel`, `CustomerKey`).
- **Webhook**: signature valid/invalid, payload parsing from recorded samples, status mapping.
- **Inbound handler** rules.
- **Feature activation** (`tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests/MessagingFeatureActivationTests.cs`):
  enabling your feature boots a tenant and your channel appears in `IMessagingChannelResolver.GetAll()`.

Run: `dotnet tests/CrestApps.OrchardCore.Tests/bin/Debug/net10.0/CrestApps.OrchardCore.Tests.dll -filterVSTest "FullyQualifiedName~Omnichannel.Messaging"`
and the CI build `dotnet build CrestApps.OrchardCore.slnx -c Release -warnaserror`.

## Docs

- `src/CrestApps.Docs/docs/omnichannel/messaging-workspace.md`: add the channel to the channel table and a
  "Setting up <Channel>" section (provider, endpoints, webhook, compliance rules).
- `src/CrestApps.Docs/docs/feature-reference.md`: add the feature row.
- The current changelog under `src/CrestApps.Docs/docs/changelog/`.
- Build the docs (`npm run build` in `src/CrestApps.Docs`); Docusaurus parses `.md` as MDX, so avoid bare `{…}`
  outside code and `### Title {#id}` anchors.
