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

The **Telephony** module adds a provider-agnostic soft phone to Orchard Core. It exposes a SignalR hub that receives call-control requests from the browser and routes them to whichever telephony provider is configured for the tenant. The UI never talks to a provider directly, so the same soft phone works with any provider that implements the telephony abstractions (for example [DialPad](dialpad)).

In this module, **provider** means the configured **telephony backend adapter** (for example Asterisk,
DialPad, or another PBX/carrier API integration), not the user's phone company in the business or
billing sense.

## Architecture

The feature is split into three layers so that providers stay decoupled from the UI and the hub:

```text
Browser command ──SignalR──► TelephonyHub ──► ITelephonyService ──► ITelephonyProvider
                                                                          │
Provider event stream/webhook ──► server projection/reconciliation ────────┘
                                         │
                                         └──SignalR CallStateChanged──► Browser state
```

- **`CrestApps.OrchardCore.Telephony.Abstractions`** contains the provider-agnostic contracts: `ITelephonyProvider`, `ITelephonyCallStateProvider`, `ITelephonyService`, `ITelephonyProviderResolver`, `ITelephonyClient`, `ITelephonyAuthenticationProvider`, `ITelephonyAuthenticationService`, `ITelephonyUserTokenStore`, `ITelephonyInteractionStore`, `ITelephonyInteractionSynchronizationService`, the request/response and interaction models, `TelephonyProviderOptions`, `TelephonySettings`, and `TelephonyPermissions`. A provider module depends only on this package.
- **`CrestApps.OrchardCore.Telephony`** contains the `TelephonyHub`, the default service and resolver
  implementations, the site settings, and the soft phone widget.
- A **provider module** (such as DialPad or Asterisk) implements `ITelephonyProvider` and registers itself as a
  selectable provider.

If you are building another provider, see [Custom Telephony and Contact Center Providers](custom-providers.md).

## The provider contract

A telephony provider implements `ITelephonyProvider`. The interface is the adapter contract between
the shared soft phone and a concrete telephony backend, and it covers the common soft phone
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

Each provider also advertises the operations it supports through the `Capabilities` property (a `TelephonyCapabilities` flags value). The soft phone UI uses these flags to show or hide controls, and `DefaultTelephonyService` enforces the same capability before calling the provider so a hidden or forged client command fails closed.

Live agent audio is advertised separately through the optional `ITelephonyAudioProvider` contract. `TelephonyAudioCapabilities` distinguishes browser audio from an external device or provider-owned application, and `TelephonyAudioModeResolver` applies these rules:

- A browser-only provider automatically uses browser audio.
- An external-device-only provider automatically leaves microphone and speaker handling outside Orchard.
- A provider that supports both must expose a provider setting and return the administrator-selected `ConfiguredAudioMode`.
- Browser audio fails closed unless the provider also names an executable browser media adapter.

The current built-in DialPad and Asterisk Telephony providers explicitly advertise `ExternalDevice`. DialPad call control currently relies on its REST integration and provider-owned clients, while the Asterisk provider currently controls calls through ARI. Asterisk External Media is a server-side Contact Center media seam and is not an embedded browser WebRTC endpoint. Neither provider currently advertises embedded browser audio.

Call operations can also carry an optional provider-neutral metadata bag through `CallReference` and
`TelephonyCall`. This keeps the shared contracts clean while still letting integrations exchange
routing hints or contextual data for scenarios such as voicemail routing.

## Provider event normalization

Modern soft-phone behavior depends on more than keypad actions. A command response only acknowledges that the provider accepted or rejected a request; it does not change the browser's call state. While a command is awaiting that acknowledgement, the widget disables its call controls and ignores duplicate submissions so repeated clicks cannot originate or mutate the same call multiple times. Provider integrations should normalize live provider events into the Contact Center voice-event pipeline so the server becomes the source of truth for:

- call lifecycle transitions
- hold and resume
- mute and unmute
- recording lifecycle
- conference and participant-count changes

The normalized provider contract is `ProviderVoiceEvent`. Providers that can emit richer details should populate those fields and let Contact Center project the resulting state back to the soft phone instead of trying to update the browser directly.

When a provider also implements `ITelephonyCallStateProvider`, Telephony and Contact Center can query the provider's current server truth for an individual call during reconnect, page restoration, authoritative offer accept, tenant-startup recovery, provider-listener reconnect, and periodic reconciliation. If a persisted in-progress Telephony interaction no longer exists at the provider, Telephony deletes the orphaned active record and sends a disconnected state to connected clients. It performs the same cleanup when the interaction's provider is no longer registered or enabled, because the stale call can no longer be controlled and must not block the agent after a provider configuration change. A transient lookup failure does not silently delete the record because the provider did not authoritatively confirm that the call is gone.

The built-in Asterisk provider now also keeps a tenant-scoped ARI event-stream listener open inside the Orchard shell lifecycle. Server-side Asterisk hangups, hold changes, and other mapped channel events are normalized on the server, written back to persisted telephony history, and pushed through the Telephony hub so a plain soft-phone call does not stay stuck on a stale client-side state after the PBX changes it externally.

## SignalR hub

The hub is registered with the [SignalR](../modules/signalr) module's `HubRouteManager`:

```csharp
public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
{
    HubRouteManager.MapHub<TelephonyHub>(routes);
}
```

Every hub method runs in its own Orchard Core shell scope and is authorized against the `Use the telephony soft phone` permission. Authorized connections join a user destination qualified by the immutable Orchard shell name, and every server-side incoming-call or call-state projection uses that tenant-qualified destination. This remains required when a multi-node deployment uses a shared SignalR backplane because Orchard user identifiers are not globally unique across tenants. Call-control methods return a `TelephonyResult` that acknowledges the command but do not optimistically push its returned call as authoritative state. The hub exposes `GetActiveCall` for compatibility and `GetActiveCalls` for provider-authoritative multi-call restoration and recovery, while provider event projections push `CallStateChanged`, `IncomingCall` (with its contextual cards), and `ReceiveError` events through the strongly typed `ITelephonyClient` interface. It also exposes `Answer`, `Reject`, and `Voicemail` operations for a ringing inbound call.

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
- **Recent calls count** controls how many calls the **Recent** tab loads. The default is `30`, and administrators can select a value from `1` through `200`.

You can enable the soft phone on the admin, the front end, or both. The widget is rendered when its
surface is enabled and the current user has the `Use the telephony soft phone` permission, so the
soft phone only appears for authorized users.

Modules can contribute display-driver tabs and views to the widget by registering a
`DisplayDriver<SoftPhoneWidget>`. Contact Center uses this extension point to add a **Work** tab for
agent queue/campaign sign-in, sign-out, and presence controls when the current user can sign in to
Contact Center work.

### Moving and persisting the widget

The soft phone is draggable by its header. Its position, open/closed state, and the selected footer tab are saved to the browser's `localStorage`, so the widget reappears exactly where you left it after a page reload — and it is restored before the first paint, so there is no flash or jump as the page loads. You can drag the widget anywhere on the screen, including all the way to the right edge and on top of other widgets such as the AI chat widget. By **default**, when the AI chat widget is also present, the soft phone automatically offsets itself so the two widgets sit side by side instead of overlapping.

### Status and call controls

The widget reflects the live connection status reported by the hub and only enables the dial pad and call controls when the provider is **available, connected, and authenticated**:

- When no provider is enabled, the widget shows a compact warning at the top of the panel that explains how to fix the setup: enable at least one provider and set the default phone provider in site settings. The warning is shown only after the hub resolves the real provider status, so a configured tenant does not flash a false **No provider is configured** warning during page load. The keypad and call buttons stay hidden, and the warning does not stretch to fill the widget.
- When the provider requires a per-user connection, the widget shows the **Connect to provider** button (see [Authenticating users with a provider](#authenticating-users-with-a-provider)).
- During an active call the main floating toggle keeps its normal accent color and phone icon; hang-up remains on the keypad itself. The widget exposes hold/resume and transfer when the provider supports them, and only exposes mute/unmute after the selected call has reached the **Connected** state. End-user error messages stay provider-neutral even when the active provider logs provider-specific details on the server.

### Browser audio adapters

When the active provider resolves to `TelephonyAudioMode.Browser`, the soft phone lazily requests microphone permission immediately before the first dial or answer action. It obtains provider bootstrap credentials through the authorized Telephony hub, initializes the provider's registered browser media adapter, supplies the local microphone stream, and provides a remote `<audio>` element and `setRemoteStream` callback for real-time caller audio. Authoritative provider call-state events continue to control the UI and are forwarded to the adapter; local microphone tracks are enabled only for a connected, unmuted call and are stopped when the last call ends, the hub disconnects, or the page unloads.

Provider scripts register a factory by adapter name:

```js
window.telephonySoftPhone.mediaAdapters.myProvider = function (context) {
    return {
        handleCallState: function (call) {
            // Synchronize the provider SDK session with authoritative call state.
        },
        dispose: function () {
            // Disconnect the provider SDK session and release provider resources.
        }
    };
};
```

The factory receives `credentials`, `localStream`, `remoteAudioElement`, `setRemoteStream`, and `showError`. Registering a JavaScript factory alone is not sufficient: the server provider must implement `ITelephonyAudioProvider`, advertise `Browser`, return the same adapter name, and issue the provider-specific short-lived bootstrap settings needed by that executable adapter.

### Keypad, recent calls, and extension tabs

The widget's footer is a tab bar that switches the panel between built-in and contributed views:

- **Keypad** – the number field, dial pad, and call controls.
- **Recent** – the call history, listing active calls, recent inbound and outbound interactions, and missed calls (highlighted in red with a direction icon). Phone numbers are formatted for display, active calls stay visually highlighted, and the list does not add a separate **In progress** text label for them. It loads the configured number of calls, `30` by default. Selecting a recent call dials it again.
- **Contributed tabs** – modules can add their own views through Display Management. For example,
  Contact Center adds a **Work** tab for queue/campaign sign-in and presence.

Pressing **Enter** while the number field is focused starts the call. The dialed value is cleared immediately to prevent an accidental repeated Enter press. While the selected call is connected, the field remains visible and disabled with the connected phone number formatted for display. After the selected call is placed on hold, the field is cleared again and the keypad becomes available for a second outbound call. The **Active calls** list shows every provider-authoritative in-progress interaction in compact rows with the phone number and state on one line; selecting a row changes which call the individual hold, resume, mute, transfer, and hang-up controls operate on. The active-call list remains on the Keypad because it is the selection context for those controls, while the Keypad view scrolls within a bounded height instead of increasing the widget size.

Select two or more active-call checkboxes to reveal **Conference selected calls**. Any number of active calls can be selected; provider-specific participant limits are enforced by the executing provider rather than by the shared UI. After a successful merge, every selected row shows **In conference**. Asterisk adds all selected channels to one mixing bridge and clears their prior hold markers, while DialPad merges each additional call into the primary call sequentially. **Disconnect all calls** sends hang-up to every active call, while the regular hang-up button ends only the selected call. Transfer is hidden for a conference until one call row is selected explicitly, preventing an accidental transfer of the conference context. The current provider-neutral contract does not model a separate agent media leg or a leave-conference operation, so ending the selected call must not be described as leaving a conference while the other participants continue.

Providers can advertise the `Directory` capability and implement `ITelephonyDirectoryProvider`. When supported, choosing **Transfer** opens a provider-backed directory and still permits a manually entered number or extension. Asterisk lists ARI endpoints, while DialPad lists company users and prefers their extension as the transfer destination before falling back to the assigned phone number.

The history is read from the hub's `GetInteractions` method and is backed by the persisted interaction store described below, so completed history survives page reloads independently of the provider. Inbound calls are persisted as soon as they are offered, so the **Recent** tab shows inbound and outbound history instead of only calls placed from the keypad. Active-call restoration no longer trusts an `InProgress` history record or assumes that it is connected: reconnect and page load call `GetActiveCalls`, which validates every active record against `ITelephonyCallStateProvider` before rendering it. Button commands do not trigger browser state changes or short provider-polling loops; provider events remain authoritative, while startup, periodic, provider-reconnect, and page-restoration reconciliation repair missed events and delete confirmed orphaned active records. A provider event received while a page-restoration lookup is in flight takes precedence over that older lookup result, and a terminal event for an earlier call id cannot clear a newer active call. When Contact Center owns the voice interaction, normalized server-side call-session changes upsert that same telephony history and push `CallStateChanged`, so provider-driven disconnects, hold/resume changes, mute/unmute updates, and other server-side lifecycle changes clear or update the live soft-phone state immediately. Provider-side Asterisk ARI events do the same for plain Telephony calls. The widget also keeps the **Keypad** tab's natural height as the shared body height for **Recent** and contributed tabs such as Contact Center **Work**, so switching tabs does not resize the panel unless the user moves it. When a non-keypad tab needs more room than that shared height, it scrolls within the panel instead of clipping its contents.

## Incoming calls

When an inbound call is offered to a user, the soft phone raises an **incoming-call modal** with
three actions:

- **Answer** connects the call (`AnswerAsync`).
- **Send to voicemail** routes the caller to voicemail (`SendToVoicemailAsync`); it is shown only when
  the provider advertises the `Voicemail` capability.
- **Ignore** declines the ringing call on this device (`RejectAsync`).

The modal appears for a ringing **inbound** call even when the panel is closed. When Contact Center is using the soft phone for queue offers, the modal restores the current ringing offer after a page refresh or reconnect, reopens immediately when the Contact Center hub reports a new queued offer, and keeps the ringing state visible until the offer is accepted, declined, or the authoritative reservation timeout expires. Those Contact Center offer actions go through the authoritative reservation endpoints without sending an extra duplicate reject/answer device action. If the real-time revoke event arrives while the authoritative accept is still completing, the soft phone preserves the accepted call instead of clearing it back to idle. The successful accept response does not mark the browser connected; the widget waits for the normalized provider state event or provider-authoritative lookup before changing the call state. For provider-only server-side queue flows that do not register a Contact Center voice provider, Contact Center also answers the underlying telephony call during accept so the provider can emit the resulting live state. The keypad **Hangup** control stays hidden while a call is still only ringing and appears only once the provider reports that the call is connected or held.

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

See the [DialPad](dialpad) and [Asterisk](asterisk) providers for complete examples.
