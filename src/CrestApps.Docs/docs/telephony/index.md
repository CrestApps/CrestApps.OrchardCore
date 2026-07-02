---
sidebar_label: Telephony
sidebar_position: 0
title: Telephony Soft Phone
description: Provider-agnostic soft phone, SignalR hub, and telephony provider model for Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | Telephony |
| **Feature ID** | `CrestApps.OrchardCore.Telephony` |

The **Telephony** module adds a provider-agnostic soft phone to Orchard Core. It exposes a SignalR
hub that receives call-control requests from the browser and routes them to whichever telephony
provider is configured for the tenant. The UI never talks to a provider directly, so the same soft
phone works with any provider that implements the telephony abstractions (for example
[DialPad](dialpad)).

## Architecture

The feature is split into three layers so that providers stay decoupled from the UI and the hub:

```text
Browser soft phone (soft-phone.js)
        │  SignalR (invoke Dial/Hangup/Hold/...)
        ▼
TelephonyHub  ──►  ITelephonyService  ──►  ITelephonyProviderResolver  ──►  ITelephonyProvider
                                                                              (DialPad, ...)
```

- **`CrestApps.OrchardCore.Telephony.Abstractions`** contains the provider-agnostic contracts:
  `ITelephonyProvider`, `ITelephonyService`, `ITelephonyProviderResolver`, `ITelephonyClient`,
  `ITelephonyAuthenticationProvider`, `ITelephonyAuthenticationService`, `ITelephonyUserTokenStore`,
  `ITelephonyInteractionStore`, the request/response and interaction models,
  `TelephonyProviderOptions`, `TelephonySettings`, and `TelephonyPermissions`.
  A provider module depends only on this package.
- **`CrestApps.OrchardCore.Telephony`** contains the `TelephonyHub`, the default service and resolver
  implementations, the site settings, and the soft phone widget.
- A **provider module** (such as DialPad) implements `ITelephonyProvider` and registers itself as a
  selectable provider.

## The provider contract

A telephony provider implements `ITelephonyProvider`. The interface covers the common soft phone
operations:

| Operation | Method |
| --- | --- |
| Dial | `DialAsync` |
| Hang up | `HangupAsync` |
| Hold (pause) | `HoldAsync` |
| Resume | `ResumeAsync` |
| Mute / Unmute | `MuteAsync` / `UnmuteAsync` |
| Transfer | `TransferAsync` |
| Merge calls | `MergeAsync` |
| Send DTMF digits | `SendDigitsAsync` |
| Answer / Reject inbound | `AnswerAsync` / `RejectAsync` |
| Send to voicemail | `SendToVoicemailAsync` |
| Client bootstrap | `GetClientCredentialsAsync` |

Each provider also advertises the operations it supports through the `Capabilities` property (a
`TelephonyCapabilities` flags value). The soft phone UI uses these flags to show or hide controls.

## SignalR hub

The hub is registered with the [SignalR](../modules/signalr) module's `HubRouteManager`:

```csharp
public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
{
    HubRouteManager.MapHub<TelephonyHub>(routes);
}
```

Every hub method runs in its own Orchard Core shell scope and is authorized against the
`Use the telephony soft phone` permission. The hub returns a `TelephonyResult` to the caller and
pushes `CallStateChanged`, `IncomingCall` (with its contextual cards), and `ReceiveError` events to
the connected client through the strongly-typed `ITelephonyClient` interface. It also exposes
`Answer`, `Reject`, and `Voicemail` operations for a ringing inbound call.

## Site settings

Telephony settings live under **Settings → Communication → Telephony** and require the
`Manage telephony settings` permission. The screen follows the same multi-provider tab layout as the
Orchard Core SMS settings:

- The **Soft Phone** tab selects the **default provider** from the list of enabled providers (as its
  first option) and configures where the soft phone widget appears.
- Each enabled provider contributes **its own tab** (rendered by a display driver in the provider
  module) where you enable the provider and supply its credentials.

When you enable the only configured provider, it is automatically selected as the default. When you
disable the current default provider, the default is cleared and the soft phone is disabled until a
new default is chosen.

## Soft phone widget

Enable the **Telephony Soft Phone** feature
(`CrestApps.OrchardCore.Telephony.SoftPhone`) to inject a floating soft phone into the site.
Configure where it appears on the **Soft Phone** tab of the telephony settings:

- **Show the soft phone on the admin dashboard** displays the widget on admin pages. This is enabled
  by default.
- **Show the soft phone on the front end** displays the widget on the website.
- **Accent color** controls the widget's button and control colors.

You can enable the soft phone on the admin, the front end, or both. The widget is rendered when its
surface is enabled and the current user has the `Use the telephony soft phone` permission, so the
soft phone only appears for authorized users.

Modules can contribute display-driver tabs and views to the widget by registering a
`DisplayDriver<SoftPhoneWidget>`. Contact Center uses this extension point to add a **Work** tab for
agent queue/campaign sign-in, sign-out, and presence controls when the current user can sign in to
Contact Center work.

### Moving and persisting the widget

The soft phone is draggable by its header. Its position and open/closed state are saved to the
browser's `localStorage`, so the widget reappears exactly where you left it after a page reload — and
it is restored before the first paint, so there is no flash or jump as the page loads. You can drag
the widget anywhere on the screen, including all the way to the right edge and on top of other
widgets such as the AI chat widget. By **default**, when the AI chat widget is also present, the soft
phone automatically offsets itself so the two widgets sit side by side instead of overlapping.

### Status and call controls

The widget reflects the live connection status reported by the hub and only enables the dial pad and
call controls when the provider is **available, connected, and authenticated**:

- When no provider is enabled, the header status reads **Not Ready** instead of a misleading
  **Ready** status, the body shows an **unavailable** message, and the number pad and buttons are
  hidden.
- When the provider requires a per-user connection, the widget shows the **Connect to provider**
  button (see [Authenticating users with a provider](#authenticating-users-with-a-provider)).
- During an active call the main toggle button turns red and switches to a hang-up icon, and the
  widget exposes mute, hold, transfer, and merge controls based on the provider's capabilities.

### Keypad, recent calls, and extension tabs

The widget's footer is a tab bar that switches the panel between built-in and contributed views:

- **Keypad** – the number field, dial pad, and call controls.
- **Recent** – the call history, listing active calls, recent inbound and outbound interactions, and
  missed calls (highlighted in red with a direction icon). Selecting a recent call dials it again.
- **Contributed tabs** – modules can add their own views through Display Management. For example,
  Contact Center adds a **Work** tab for queue/campaign sign-in and presence.

The history is read from the hub's `GetInteractions` method and is backed by the persisted
interaction store described below, so it survives page reloads and is available independently of the
provider.

## Incoming calls

When an inbound call is offered to a user, the soft phone raises an **incoming-call modal** with
three actions:

- **Answer** connects the call (`AnswerAsync`).
- **Send to voicemail** routes the caller to voicemail (`SendToVoicemailAsync`); it is shown only when
  the provider advertises the `Voicemail` capability.
- **Ignore** declines the ringing call on this device (`RejectAsync`).

The modal appears for a ringing **inbound** call even when the panel is closed, and it hides itself
once the call connects or ends.

### Offering a call to a user

Inbound calls are pushed to a specific user through `IIncomingCallDispatcher`. It runs every
registered `IIncomingCallContextProvider`, builds the `IncomingCallContext`, and sends
`IncomingCall(call, context)` to that user's soft-phone connections through
`IHubContext<TelephonyHub, ITelephonyClient>`:

```csharp
await _incomingCallDispatcher.DispatchAsync(agentUserId, call);
```

Telephony owns the modal and the media controls; it does not decide who receives a call. An
orchestration module (such as the [Contact Center](../contact-center/index.md)) resolves the target
user, reserves the agent, and calls the dispatcher.

### Enriching the modal from another module

Other modules contribute records and shortcuts to the modal by implementing
`IIncomingCallContextProvider` and adding `IncomingCallCard` entries:

```csharp
public sealed class MyContextProvider : IIncomingCallContextProvider
{
    public Task ContributeAsync(IncomingCallContributionContext context, CancellationToken cancellationToken = default)
    {
        context.Cards.Add(new IncomingCallCard
        {
            Title = "Jane Doe",
            Subtitle = context.Call.From,
            Icon = "fa-solid fa-user",
            Url = "/Admin/Contents/ContentItems/<id>/Edit",
        });

        return Task.CompletedTask;
    }
}
```

Each card is rendered with an **Answer & open** shortcut (answers the call and opens the card's
`Url`) and an **Open** link. A provider can also set `context.Properties["acceptUrl"]` and
`context.Properties["declineUrl"]`; when present, the modal posts to them as the agent answers or
ignores the call, which lets an orchestration module track the offer lifecycle. The
[Contact Center Voice](../contact-center/index.md) feature uses this seam to list customers matched by
the caller's phone number and to drive its reservation lifecycle.

## Adding the soft phone to the website

There are two ways to add the soft phone to your site:

- **Automatically** – turn on **Show the soft phone on the front end** (and/or the admin) on the
  telephony settings. The widget is injected into the layout's `Footer` zone for authorized users.
- **Manually** – render the `SoftPhoneWidget` shape wherever you want it (for example in a theme
  layout or a template) and register its resources. This shows the base phone controls. If you need
  contributed Display Management tabs, build the widget through `IDisplayManager<SoftPhoneWidget>` the
  same way the automatic filter does.

  ```html
  <style asp-name="telephony-soft-phone" at="Head"></style>
  <script asp-name="telephony-soft-phone" at="Foot"></script>
  <script asp-name="telephony-phone-field" at="Foot"></script>

  @await DisplayAsync(await New.SoftPhoneWidget())
  ```

The `telephony-soft-phone` script depends on the `signalr` script, which the SignalR module adds
automatically.

## Authenticating users with a provider

Providers use one of two authentication scenarios, and the soft phone adapts automatically:

- **Account-level credentials** – the provider authenticates with a shared key configured by an
  administrator (for example an API token). No per-user step is needed and the dialer is shown
  immediately. This is the right model for providers such as Twilio.
- **Per-user OAuth 2.0** – the provider requires each user to connect their own account (for example
  DialPad). When the user is not yet connected, the widget shows a **Connect to provider** button
  that starts the OAuth 2.0 authorization code flow in a popup. After the user grants access, the
  tokens are stored **encrypted on the user's account** and the dialer is shown. Expired tokens are
  refreshed automatically when a refresh token is available.

On connection the widget asks the hub for the user's status (`GetConnectionStatus`). The hub reports
whether authentication is required, whether the user is connected, and which authentication scheme
the provider uses. The widget's connect experience is extensible: a provider that uses a different
per-user scenario can register a handler for its scheme on
`window.telephonySoftPhone.authHandlers`, so new authentication scenarios can be supported without
changing the widget.

The OAuth callback URL the soft phone uses is `{scheme}://{host}/Telephony/Connect/Callback`
(prefixed with the tenant URL prefix when one is configured). Register this URL as an allowed
redirect URI in your provider's OAuth application.

## Phone field click-to-dial

When the soft phone is present on a page, the [Phone Field](../modules/content-fields) editor and
display are enhanced with a **dial** button that calls the number with the soft phone. The Phone
Field views expose a neutral `data-phone-dial` placeholder, and the soft phone attaches the button to
it, so the editors are not overridden. Enable the soft phone on the admin to get click-to-dial while
editing content, and on the front end to get it on displayed phone numbers.

## Call history and reporting

Every call the hub handles is recorded through `ITelephonyInteractionStore` and indexed with
`TelephonyInteractionIndex`. Each interaction stores the provider's interaction id, the provider
name, the user id and name, the call direction and outcome, the start and end timestamps, and the
duration. Because the index covers the interaction id, provider name, user name, and call date, the
site keeps a searchable call history for reporting and history tracking that is **independent of the
provider** — the data remains even if the provider integration is later removed.

The soft phone reads this history through the hub's `GetInteractions` method to render its history
panel. Outbound calls placed from the soft phone are recorded automatically. Inbound and missed
calls are fully modeled (direction and outcome). Inbound calls reach the soft phone through
`IIncomingCallDispatcher` (see [Incoming calls](#incoming-calls)); a provider module or an
orchestration module such as the Contact Center reports the inbound event and offers the call to a
user.

## Writing a provider

To add a new provider:

1. Reference `CrestApps.OrchardCore.Telephony.Abstractions`.
2. Implement `ITelephonyProvider`.
3. Register the provider and an `IConfigureOptions<TelephonyProviderOptions>` that reflects whether
   it is enabled based on the tenant settings:

   ```csharp
   services.AddTelephonyProviderOptionsConfiguration<MyProviderOptionsConfigurations>();
   ```

4. Add a `SiteDisplayDriver<MyProviderSettings>` whose `SettingsGroupId` is
   `TelephonyConstants.SettingsGroupId` so the provider gets its own tab on the telephony settings
   screen.
5. For per-user authentication, also implement `ITelephonyAuthenticationProvider`. Declare the
   `AuthenticationScheme` (for example `TelephonyConstants.AuthenticationSchemes.OAuth2`), build the
   authorization URL, exchange the authorization code for tokens, and refresh tokens. The framework
   stores the tokens securely on the user's account and supplies them back to your provider through
   `ITelephonyAuthenticationService`. Providers that only use a shared account key do not implement
   this interface.

See the [DialPad](dialpad) provider for a complete example.
