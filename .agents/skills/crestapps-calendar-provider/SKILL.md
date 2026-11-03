---
name: crestapps-calendar-provider
description: >
  Skill for adding an external calendar provider (Google Calendar, Microsoft 365 / Outlook through Microsoft Graph,
  CalDAV, Zoom or any calendar API) to the CrestApps.OrchardCore Calendars module, so a calendar can be kept in step
  with an external calendar one way (read) or two ways (read and write back). Covers the provider contracts in
  CrestApps.OrchardCore.Calendars.Core.External — ICalendarProvider (full read, UsesAddress, IsConfigured),
  IIncrementalCalendarProvider (sync tokens, FullSyncRequired), IWritableCalendarProvider (PushAsync, DeleteAsync,
  If-Match versions), CalendarProviderConflictException, CalendarProviderAuthorizationException — what
  CalendarSyncService does for you (matching by external id, provider mappings, content fingerprints, conflict
  policies, retries with backoff, pending deletes, failure isolation, the health check), mapping events (wall-clock
  times and time zones, all-day dates, recurrence and occurrence changes, attendees, status), connecting accounts with
  OAuth and protecting tokens, the editor, module and feature layout, registration with AddCalendarProvider, tests with
  a fake remote calendar, and docs. Use this skill whenever the request is to "add a calendar provider", "sync with
  Google Calendar", "integrate Microsoft 365 / Outlook calendars", "two-way calendar sync", implement ICalendarProvider,
  or change the external calendar synchronization.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Adding an external calendar provider

The **External Calendars** feature (`CrestApps.OrchardCore.Calendars.External`) keeps a calendar in step with a
calendar somewhere else. The feature owns the whole synchronization loop; a **provider** only talks to the external
API. The built-in provider is a read-only iCalendar address (`ics`). Google Calendar and Microsoft 365 are the
providers this skill is written for.

Read first; they are authoritative:

- Contracts: `src/Core/CrestApps.OrchardCore.Calendars.Core/External/` — `ICalendarProvider.cs`,
  `IIncrementalCalendarProvider.cs`, `IWritableCalendarProvider.cs`, `CalendarProviderChanges.cs`,
  `CalendarProviderPushContext.cs`, `CalendarProviderPushResult.cs`, the two exceptions,
  `CalendarSyncConflictPolicy.cs`, `CalendarSubscription.cs`, `CalendarSyncServiceCollectionExtensions.cs`.
- The loop: `CalendarSyncService.cs` (read, write back, conflicts), `CalendarSyncChangeObserver.cs` (queues local
  changes), `CalendarSyncFingerprint.cs`, and `ICalendar/ICalendarImporter.cs` (`ICalendarImportOptions.CanUpdate`,
  `CanRemove`, `Applied`).
- The module: `src/Modules/CrestApps.OrchardCore.Calendars/ExternalStartup.cs`, `External/ICalendarUrlProvider.cs`
  (the reference provider), `Drivers/CalendarSubscriptionDisplayDriver.cs`, `Views/CalendarSubscription.Edit.cshtml`,
  `BackgroundTasks/CalendarSyncBackgroundTask.cs`, `HealthChecks/CalendarSyncHealthCheck.cs`.
- Tests to copy: `tests/CrestApps.OrchardCore.Tests/Calendars/CalendarSyncTests.cs` (a fake remote calendar with
  versions and change tokens that drives every path of the loop).
- Docs: `src/CrestApps.Docs/docs/modules/calendars.md` ("External calendars"). The general calendar extension rules
  are in the `crestapps-calendar-types` skill.
- Provider notes: [references/google-calendar.md](references/google-calendar.md) and
  [references/microsoft-graph.md](references/microsoft-graph.md).

## 1. What the feature does, and what the provider does

`CalendarSyncBackgroundTask` runs every five minutes. It asks `CalendarSyncService.ListDueAsync` which calendars are
due and runs `SyncAsync(calendarId)` for each **in its own child scope**, so one calendar failing (a provider error, a
concurrency exception on save) never stops the others. A calendar is due when its interval has passed since the last
attempt (twice as long after each failure in a row, up to 32 times), or right away when it has local changes waiting
(`PendingPush`). A subscription that `NeedsReconnect` is skipped until someone saves it again.

One run of `SyncAsync`:

1. **Read.** With an `IIncrementalCalendarProvider` and a stored `SyncToken`, `PullChangesAsync` returns only what
   changed; otherwise `PullAsync` returns everything and events missing from the result are removed here. When the
   token expired (`FullSyncRequired`), the service drops it and reads everything. After a full read it asks
   `GetSyncTokenAsync` for the next token.
2. **Import.** `ICalendarImporter` matches each `ICalendarEventData` to the event with the same **`Uid`** among the
   events this subscription imported (`Source = "External"`), creates or updates it, and records a
   `CalendarProviderMapping` on the event: `ConnectionId` (the calendar id), `Provider`, `ExternalEventId` (the
   `Uid`), `Version` (`ICalendarEventData.Version`), `ContentHash` (a fingerprint of the synchronized fields) and
   `LastSyncedUtc`. Occurrence changes (`RecurrenceIdUtc`) become overrides of their series.
3. **Conflicts** (two-way only). An event whose fingerprint no longer matches its mapping was changed here. If the
   external copy's version also moved, both sides changed: the subscription's `ConflictPolicy` decides.
   `ExternalWins` (the default) overwrites the local change; `LocalWins` keeps it and later writes it over the external
   copy. An event deleted externally but changed here is re-created externally under `LocalWins`.
4. **Write back** (two-way only, `IWritableCalendarProvider`). First the `PendingDeletes` (external copies of events
   deleted here, queued by `CalendarSyncChangeObserver`), then every event of the calendar that has no mapping yet or
   whose fingerprint changed: `PushAsync`. The service stores the returned id and version on the event, sets
   `Uid = ExternalEventId` (series only), `Source = External` and `Editable = true`, without telling observers.
   Series are pushed before their occurrence changes, and an occurrence change waits until its series has an external
   copy.
5. **Record.** `LastSyncedUtc`, `LastResult` ("n added, n changed, n removed, n unchanged, n conflicts, n refused"),
   `LastError` (refused events), `Failures = 0`. A thrown exception sets `LastError` and `Failures++` instead; the
   events already here are left as they are.

The **provider** only: reads events into `ICalendarEventData`, writes a `CalendarEvent` out, deletes by id, hands
out sync tokens, and reports conflicts and refused access with the two exceptions. It never touches stores, the
importer or the subscription's bookkeeping fields.

## 2. The contracts

| Contract | Required | What to implement |
| --- | --- | --- |
| `ICalendarProvider.Name` | Yes | Stable technical name stored on each subscription (`google`, `microsoft365`). Never rename. |
| `ICalendarProvider.DisplayName` | Yes | `LocalizationSource.Create<TProvider>("Google Calendar")`. Shown in the provider picker. |
| `ICalendarProvider.PullAsync` | Yes | Every event of the external calendar in the synchronization window (see 4). Events left out are **removed** here, so never return a partial page: page through to the end or throw. |
| `ICalendarProvider.UsesAddress` | Account providers | Return `false` when the subscription is made with an account, not `CalendarSubscription.Url`. The editor then hides the address field. |
| `ICalendarProvider.IsConfigured` | Account providers | Return whether the calendar has what you need (a connected account and a chosen calendar). An unconfigured subscription is never due and can't be enabled. |
| `IIncrementalCalendarProvider.PullChangesAsync` | Recommended | Changes since the token: `Events` (added or changed, full data), `DeletedIds`, `NextSyncToken`. Set `FullSyncRequired = true` when the API says the token is no longer valid; don't throw. |
| `IIncrementalCalendarProvider.GetSyncTokenAsync` | With the above | The token to read from after a full read (usually the token the last page of the full listing returned; cache it from `PullAsync` on the instance for the same run, the service calls it right after). |
| `IWritableCalendarProvider.PushAsync` | Two-way | `context.Mapping == null`: create; otherwise update `Mapping.ExternalEventId`, sending `Mapping.Version` as the precondition (`If-Match`). `Mapping.Version == null` means overwrite unconditionally. Return the external id and the **new** version. |
| `IWritableCalendarProvider.DeleteAsync` | Two-way | Delete `context.Mapping.ExternalEventId` (`context.Event` is null). Already gone (404/410) is success. |
| `CalendarProviderConflictException` | Two-way | Throw when the precondition failed (HTTP 412). Never resolve the conflict yourself. |
| `CalendarProviderAuthorizationException` | Account providers | Throw when access is refused for good: `invalid_grant`, a revoked refresh token, 401 after a refresh, 403 because the calendar is no longer shared. The service pauses the subscription (`NeedsReconnect`) instead of retrying. |

Everything else (timeouts, 429, 5xx, DNS) should simply throw: the service records it and backs off. Honor
`Retry-After` by throwing; don't sleep inside the provider.

### The identity rule

`ICalendarEventData.Uid` is the **external event id** and must be the same value `PushAsync` returned as
`ExternalEventId` for that event. The importer matches by it, deletions are reported by it, and the mapping stores it.
Use the API's own event id, not a shared iCalendar UID that several instances or copies can carry. For a changed
occurrence of a repeating event, return the **series'** id as `Uid` and the original start in UTC as
`RecurrenceIdUtc`.

### Versions

Put the external copy's ETag / change key in `ICalendarEventData.Version` on every read and return the new one from
every write. A provider without versions leaves it `null`: every read then counts as "changed externally", so a local
change always meets a conflict and the policy decides.

## 3. Registration, module and feature layout

Put each provider in its own module or feature, e.g. `src/Modules/CrestApps.OrchardCore.Calendars.Google` with a
feature `CrestApps.OrchardCore.Calendars.Google` that depends on `CalendarsConstants.Features.External`
(`CrestApps.OrchardCore.Calendars.External`). Reference `CrestApps.OrchardCore.Calendars.Core` (the contracts live
there) and `.Abstractions`.

```csharp
[Feature("CrestApps.OrchardCore.Calendars.Google")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(GoogleCalendarProvider.ProviderName);
        services.AddCalendarProvider<GoogleCalendarProvider>(); // also registers the sync service and change observer
        services.AddDisplayDriver<Calendar, GoogleCalendarSubscriptionDisplayDriver>();
        services.AddSiteDisplayDriver<GoogleCalendarSettingsDisplayDriver>(); // client id/secret, if per tenant
    }
}
```

- `AddCalendarProvider<T>()` registers the provider as a scoped `ICalendarProvider` and calls `AddCalendarSync()`.
  Don't register `CalendarSyncService`, the importer or the observer yourself.
- Use `IHttpClientFactory` with a named client; never `new HttpClient()`.
- Provider classes are `public sealed` only when another module needs them; otherwise `internal sealed` keeps the
  public API baseline small (`tests/CrestApps.OrchardCore.Tests/PublicApi/Baselines`).

## 4. Mapping events

Read and write the same fields the fingerprint covers (`CalendarSyncFingerprint`): title, description, location,
all-day, start, end, time zone, status, transparency, recurrence rule and exception dates, attendee emails. A field
you write but never read back (or the other way round) makes the event look changed on every run.

- **Times.** `Start`/`End` are **wall-clock** times in `TimeZoneId` (IANA, such as `America/New_York`), with
  `DateTimeKind.Unspecified`. Never put UTC in them, and never set `StartUtc`/`EndUtc` (the event manager computes
  them). Floating times: `TimeZoneId = null` (the calendar's zone applies). Convert Windows zone names with
  `TimeZoneInfo.TryConvertWindowsIdToIanaId`; when you write, convert back if the API wants Windows names.
- **All-day.** `AllDay = true`, `Start` = the first date at midnight, `End` = the day after the last date
  (exclusive), like iCalendar `DTEND;VALUE=DATE`.
- **Recurrence.** `Rule` is an RFC 5545 `RRULE` value without the `RRULE:` prefix (`FREQ=WEEKLY;BYDAY=MO`);
  `ExceptionDates` are wall-clock starts of removed occurrences. When writing, the series' `CalendarEvent.Recurrence`
  holds the same. An occurrence change is an event with `SeriesId` and `OriginalStartUtc`; `PushAsync` gets the
  series' mapping in `context.SeriesMapping` to address the instance.
- **Status.** `CalendarEventStatus` (`Confirmed`, `Tentative`, `Cancelled`). A cancelled occurrence is either an
  exception date or a cancelled override; a cancelled series is a deletion when the API has no cancelled state.
- **Transparency.** `Busy`/`Free` from the API's "show as" / transparency.
- **Attendees.** `CalendarAttendee` with `Email`, `Name`, `Response`. Don't send invitations from the provider: the
  Calendar Invitations feature does that on its own events, and the external calendar does it on its side. Prefer the
  API option that suppresses notifications on writes made by synchronization.
- **Window.** Read a bounded window (for example 1 year back, 2 years ahead) and document it. The removal step only
  touches events this subscription imported, but an event that falls out of the window on a full read is removed.

## 5. Connecting an account

Account providers need OAuth 2.0 with offline access (a refresh token) and a calendar scope. Rules:

- Keep app credentials (client id, secret, tenant) in site settings or configuration (`IShellConfiguration`), never
  in code. The secret is protected with `IDataProtectionProvider` like other CrestApps secrets.
- Keep the per-calendar connection (account email, external calendar id, **protected** refresh token, granted
  scopes) as a typed object in the calendar's properties: `calendar.Put(new GoogleCalendarConnection { ... })`,
  read with `calendar.TryGet<GoogleCalendarConnection>(out var connection)`. Protect tokens with a purpose string
  unique to the provider. Never log tokens or put them in `LastError`.
- Do the authorization code flow (with PKCE) in your own controller: an admin action that redirects to the consent
  page with a `state` bound to the calendar id and the user (anti-forgery), and a callback that exchanges the code,
  stores the connection and returns to the calendar editor. Require the calendar's Manage permission
  (`ICalendarAccessService` / the calendar permissions) on both.
- Refresh access tokens inside the provider when they expire; throw `CalendarProviderAuthorizationException` when
  the refresh is refused. Saving the subscription again clears `NeedsReconnect` and resets the backoff; reconnecting
  must also replace the stored token.
- Your editor driver (`DisplayDriver<Calendar>`) shows the Connect / Reconnect button, the connected account and a
  picker of the account's calendars only when `CalendarSubscription.Provider == Name`. The shared Subscription card
  already shows the provider picker, interval, two-way switch (only for `IWritableCalendarProvider`), conflict policy
  and last result.

## 6. Push notifications (optional)

Google watch channels and Microsoft Graph subscriptions can tell you a calendar changed. They are an optimization: the
interval read must keep working without them. When you add them, validate the request (channel token / `clientState`,
then answer Graph's validation handshake), map it to the calendar, and make the calendar due (set
`CalendarSubscription.LastAttemptUtc = null` and save with `ICalendarService.UpdateAsync`). Never synchronize inside
the webhook request. Renew channels before they expire from a background task.

## 7. Tests

Copy `CalendarSyncTests.cs`: it runs the real service, importer and event manager on SQLite through
`CalendarTestHarness` with a fake remote calendar. For a real provider, add:

- mapping tests: an API payload (stored as a JSON fixture) read into `ICalendarEventData` — timed, all-day, a
  repeating series with an exception and a moved occurrence, a cancelled occurrence, time zones (including a Windows
  zone name), attendees;
- a round trip: the event written by `PushAsync` read back gives the same fingerprint (no change on the next run);
- HTTP behavior with a fake `HttpMessageHandler`: paging to the end, the sync token flow, the expired-token answer
  (`FullSyncRequired`), 412 → `CalendarProviderConflictException`, 401/`invalid_grant` →
  `CalendarProviderAuthorizationException`, 404 on delete → success, 429/5xx → an exception (not a swallowed error);
- the editor: enabling without a connection is refused; the address field is hidden.

Then run the calendar tests, `PublicApiApprovalTests` and the architecture tests, as the repo's `AGENTS.md` says.

## 8. Docs and checklist

- Update `src/CrestApps.Docs/docs/modules/calendars.md` (External calendars: the provider, how to connect, what is
  synchronized, the window, limits) and add a changelog entry. Follow `src/CrestApps.Docs/README.md`; docs never
  mention tests or internal plans.
- Add the provider's module to the solution, the feature to the docs' feature table, and keep this skill current if
  you change a contract.

Checklist:

- [ ] `Name`, `DisplayName`; `UsesAddress`/`IsConfigured` for account providers.
- [ ] `PullAsync` pages to the end and maps every fingerprinted field; `Uid` = external id; `Version` set.
- [ ] Incremental reads with tokens and `FullSyncRequired` (when the API has them).
- [ ] `PushAsync`/`DeleteAsync` with `If-Match`; conflict and authorization exceptions; 404 delete = success.
- [ ] Wall-clock times with IANA zones; all-day dates; recurrence and occurrence changes.
- [ ] OAuth with PKCE and `state`; protected tokens on the calendar; Manage permission on connect.
- [ ] Editor driver for the connection; registration with `AddCalendarProvider<T>()`.
- [ ] Tests (mapping fixtures, HTTP behavior, round trip) and docs.
