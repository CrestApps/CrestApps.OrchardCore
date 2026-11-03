# Microsoft 365 / Outlook (Microsoft Graph) provider notes

Microsoft Graph v1.0, `https://graph.microsoft.com/v1.0`. Verify every detail below against Microsoft's current
documentation before relying on it; APIs change.

## Connecting

- Microsoft identity platform authorization code flow with PKCE:
  `https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize` and `/token`. `{tenant}` is `common`,
  `organizations` or a tenant id from settings.
- Delegated scopes: `offline_access` (refresh token), `Calendars.ReadWrite` (or `Calendars.Read` for read-only),
  `User.Read` for the account's name. Shared or delegated calendars need `Calendars.ReadWrite.Shared`.
- A refused refresh (`invalid_grant`, `AADSTS700082` expired, `AADSTS50173` revoked) → throw
  `CalendarProviderAuthorizationException`.
- Store the protected refresh token and replace it with the new one each refresh returns (refresh tokens roll).

## Reading

- Full read and incremental read are the same API: `GET /me/calendars/{calendarId}/calendarView/delta` with
  `startDateTime` and `endDateTime` (the window). Follow `@odata.nextLink` until an `@odata.deltaLink` arrives; store
  the delta link (or its `$deltatoken`) as the sync token. Later reads call the delta link. A `410` /
  `syncStateNotFound` / `resyncRequired` answer → `FullSyncRequired = true`.
- `calendarView/delta` expands series into occurrences. To keep series as series, read `/events` (series masters,
  `type: "seriesMaster"`, with `recurrence`) for the full read and use the delta only to learn which ids changed, or
  map occurrences back to their master with `seriesMasterId`. Pick one approach and document it; the identity rule
  still holds (series `Uid` = master id, occurrence changes carry `RecurrenceIdUtc` = `originalStart`).
- Removed items appear with `@removed: { reason: "deleted" }` → `DeletedIds`.
- Ids: `id` (it can change when an item moves between folders; treat a missing id as deleted and the new one as new).
  `iCalUId` is shared and not for matching.
- `@odata.etag` (or `changeKey`) is the version.
- Times: send `Prefer: outlook.timezone="UTC"` and convert, or read `start.dateTime` + `start.timeZone`. Graph often
  returns **Windows** zone names (`Eastern Standard Time`); convert with `TimeZoneInfo.TryConvertWindowsIdToIanaId`.
  `originalStartTimeZone` tells the organizer's zone. All-day: `isAllDay: true`, dates at midnight, end exclusive.
- Recurrence is a `patternedRecurrence` object (`pattern.type` daily/weekly/absoluteMonthly/relativeMonthly/
  absoluteYearly/relativeYearly, `interval`, `daysOfWeek`, `index`, `range` endDate/numbered/noEnd). Translate to and
  from an RRULE; refuse (log and skip) what can't be expressed rather than writing a different pattern.
  `cancelledOccurrences` / exception instances give the exception dates.
- `showAs` (`free`, `tentative`, `busy`, `oof`, `workingElsewhere`) → transparency; `isCancelled` → cancelled.
- `attendees[].emailAddress.address`, `.name`, `status.response` (`none`, `accepted`, `declined`,
  `tentativelyAccepted`, `notResponded`).

## Writing

- Create: `POST /me/calendars/{calendarId}/events`. Update: `PATCH /me/events/{id}` with `If-Match: <etag>` when
  `Mapping.Version` is set; `412` → `CalendarProviderConflictException`.
- Writing attendees makes Outlook send invitations. When the event already has attendees in Outlook, avoid rewriting
  them unless they changed.
- An occurrence change: list `/me/events/{masterId}/instances?startDateTime=&endDateTime=` around `OriginalStartUtc`
  and `PATCH` that instance.
- Delete: `DELETE /me/events/{id}`; `404` → success.
- Throttling: `429` with `Retry-After`, and `503`/`504`: throw so the service backs off.

## Push notifications

`POST /subscriptions` with `changeType: "created,updated,deleted"`, the resource
`/me/calendars/{id}/events`, your HTTPS `notificationUrl`, a secret `clientState` and an expiration (at most about
three days for events). Graph first calls the URL with `validationToken`: echo it as `text/plain` within 10 seconds.
Check `clientState` on each notification, make the calendar due, return 202. Renew before expiry.
