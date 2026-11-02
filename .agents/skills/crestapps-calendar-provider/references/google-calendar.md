# Google Calendar provider notes

Google Calendar API v3, `https://www.googleapis.com/calendar/v3`. Verify every detail below against Google's current
documentation before relying on it; APIs change.

## Connecting

- OAuth 2.0 web flow at `https://accounts.google.com/o/oauth2/v2/auth`, token endpoint
  `https://oauth2.googleapis.com/token`. Ask for `access_type=offline` and `prompt=consent` the first time so a
  refresh token is returned, and use PKCE.
- Scopes: `https://www.googleapis.com/auth/calendar.events` (read and write events) plus
  `https://www.googleapis.com/auth/calendar.readonly` or `calendar.calendarlist.readonly` to list the account's
  calendars for the picker. A read-only provider needs only `calendar.events.readonly`.
- A refused refresh answers `400 invalid_grant`: throw `CalendarProviderAuthorizationException`.
- Apps in "testing" status get refresh tokens that expire after 7 days; say so in the docs.

## Reading

- `GET /calendars/{calendarId}/events` with `singleEvents=false` (keep series as series), `showDeleted=true` on
  incremental reads, `maxResults=2500`, and `pageToken` until no `nextPageToken`. The last page of a listing carries
  `nextSyncToken`: return it from `GetSyncTokenAsync` after `PullAsync`.
- Incremental: the same call with `syncToken` (and no `timeMin`/`timeMax`, which can't be combined with it).
  `410 Gone` means the token expired: return `FullSyncRequired = true`.
- Deleted or cancelled: items with `status: "cancelled"`. A cancelled **series or single event** is a deletion
  (`DeletedIds`); a cancelled **instance** (it has `recurringEventId` and `originalStartTime`) is an exception date of
  its series or a cancelled override.
- Ids: use `id` as `Uid`. Instances of a series have `recurringEventId` (the series id) and `originalStartTime`;
  return `Uid = recurringEventId` and `RecurrenceIdUtc = originalStartTime` in UTC. `iCalUID` is shared by all
  instances and by copies on other calendars, so don't use it to match.
- `etag` is the version (`ICalendarEventData.Version`).
- Times: `start.dateTime` (RFC 3339 with offset) plus `start.timeZone` (IANA). Convert the instant to wall-clock in
  that zone. All-day: `start.date` / `end.date` (end exclusive).
- Recurrence: `recurrence` is a list of lines (`RRULE:...`, `EXDATE...`, `RDATE...`). Strip `RRULE:` for `Rule`;
  parse `EXDATE` values into wall-clock `ExceptionDates`. `RDATE` has no equivalent: log and ignore.
- `transparency: "transparent"` is free; `status: "tentative"` is tentative.
- `attendees[].email`, `displayName`, `responseStatus` (`needsAction`, `accepted`, `declined`, `tentative`).

## Writing

- Create: `POST /calendars/{calendarId}/events?sendUpdates=none`. Update: `PUT .../events/{eventId}?sendUpdates=none`
  with `If-Match: <etag>` when `Mapping.Version` is set. `412 Precondition Failed` → `CalendarProviderConflictException`.
- An occurrence change: find the instance id with `GET .../events/{seriesId}/instances?originalStart=...` (or compute
  it), then `PUT` the instance.
- Delete: `DELETE .../events/{eventId}?sendUpdates=none`; `404`/`410` → success.
- Quotas: `403 rateLimitExceeded` / `usageLimits` and `429` are transient: throw so the service backs off.

## Push notifications

`POST .../events/watch` creates a channel with your HTTPS address and a `token`; Google sends `X-Goog-Channel-Token`,
`X-Goog-Resource-State` (`sync`, `exists`, `not_exists`). Check the token, make the calendar due, return 200. Channels
expire; renew them.
