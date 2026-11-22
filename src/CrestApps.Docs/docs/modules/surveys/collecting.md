---
sidebar_label: Collecting responses
title: Collecting Responses
description: "Public links, kiosks, personal invitations, embedding, the respondent routes, access rules, respondent limits, anonymity, quotas and the runner."
user_manual:
  - user-manual/surveys/share-and-invite
---

## Collectors

A collector (`SurveyCollector`) is a way in: a **web link**, a **kiosk**, an **invitation** list, or a kind of a consumer's own. Each has its own token (the public URL `/s/{token}`), opening and closing times, most responses, password (stored as a hash), hidden variable values and culture. The first publish creates the survey's default web link. A collector's QR code is served as SVG at `admin/surveys/collectors/{id}/qr.svg`.

A **kiosk** shows the ending for a few seconds, then returns to the start for the next person; it clears the resume cookie and never resumes a response.

## Respondent routes

A GET never starts a response or uses up an invitation, so link previews (chat apps) and mail scanners that follow a link create nothing. Pages that start something post a form: the page submits it at once with script, and shows a button without script.

| Route | Does |
| --- | --- |
| `GET /s/{token}` | The start page (password or CAPTCHA when needed). When the survey skips it, the page posts its start form at once |
| `POST /s/{token}/start` | Starts a response, or resumes the browser's response in progress |
| `GET`/`POST /s/r/{responseId}` | The current page; posts Next, Back or Finish. `?t=` is a resume link (see below) |
| `POST /s/r/{responseId}/resume` | Resumes from a resume link once a signed-in visitor confirmed it |
| `POST /s/r/{responseId}/save` | Autosave without checks |
| `POST /s/r/{responseId}/evaluate` | Visibility and piping for answers not yet posted |
| `POST /s/r/{responseId}/check/{questionId}` | Immediate feedback |
| `GET /s/r/{responseId}/done` | The ending |
| `GET /s/r/{responseId}/review` | The review, as the review settings allow |
| `POST /s/r/{responseId}/reopen` | Reopens a finished response when changes are allowed |
| `GET /s/i/{token}` | A personal invitation link: marks the invitation opened and shows a page that posts the start |
| `POST /s/i/{token}` | Starts the invited person's response, or resumes it from any browser |
| `GET /s/i/{token}/unsubscribe` | The unsubscribe page of an invitation e-mail; changes nothing |
| `POST /s/i/{token}/unsubscribe` | Unsubscribes the invitation's address from survey invitations (no form token, for one-click unsubscribe) |
| `GET /s/{token}/results` | Totals of all responses, when the survey shows them |
| `GET /s/r/{responseId}/results` | The same totals for the respondent of that response, for a survey reached without a public link (a course item's survey) |
| `GET /surveys/my` | The signed-in user's own responses |

Posts check the antiforgery token. When the Orchard Core **Rate Limits** feature is on, every route in the table is in the rate-limit group `Surveys.Respond`: **120 requests a minute per IP address** (a fixed one-minute window, `RespondRateLimitsStartup.RequestsPerMinute`). Admin **Preview** works the same way: opening its link shows a page that posts the start of the test response.

Every response under `/s` carries `Referrer-Policy: no-referrer`, so the tokens in collector and invitation links never reach another site in a `Referer` header.

### Resume tokens and cookies

A response is owned by the signed-in user who started it, or by whoever holds its resume token. A response started by a signed-in user on an identified survey has no resume token. The token is `{responseId}.{secret}` with a 256-bit secret, and only its SHA-256 hash is stored (the start lock key holds the hash too, never the token).

The browser holds the token in an HttpOnly, SameSite Lax cookie `sr_{surveyId}` on the tenant's `/s` path (`{PathBase}/s`), for 30 days. Each resume cookie is bound to a generation cookie `sr_g`. Signing in or out (Orchard Core `ILoginFormEvent` and `ILogoutFormEvent`) writes a new generation, so every resume cookie written before stops counting and is removed on the next respondent page. On a shared computer, the next person never resumes the previous person's response. Code that starts a response for someone (a course, for example) calls `SurveyResumeCookies.Write(surveyId, token)` from `CrestApps.OrchardCore.Surveys.Core`, and never puts the token in a URL.

A **resume link** `/s/r/{responseId}?t={token}` (for consumers that cannot set the cookie) works like this:

1. The token must be the current token of that same response. A forged token, a token of another response or a token already used gives 404.
2. When the browser already holds a different response of the survey that is in progress, the link does not replace that cookie: the page says to finish that response first (409).
3. When the visitor is signed in and the response belongs to no user account, a page asks before resuming, and resuming posts to `/s/r/{responseId}/resume`.
4. The runner issues a new token (`ISurveyResponseService.RotateResumeTokenAsync`), stores its hash, writes the cookie, and redirects to the same address without `?t=`. The link's token stops working, so a link resumes once, and whoever shared their own link loses access to the response.

A personal invitation link is the invitee's key: on an identified survey, posting it resumes the invitation's response in progress from any browser (with a new resume token there), so a scanner that opened the link first does not lock the invitee out. A response that belongs to a user account resumes only for that user. An anonymous response does not name its invitation, so it resumes only in the browser that started it.

## Access rules

`ISurveyAccessRule`s decide whether a response may start, run again when it completes (under a lock, so two tabs cannot pass a limit), and are asked through `CheckResumeAsync` before a response in progress is handed back to a returning respondent (the built-in rules allow it; a rule about who the respondent is, such as the Learning enrollment rule, checks again). The first refusal wins.

| Order | Rule | Checks |
| --- | --- | --- |
| 100 | `Status` | The survey is open |
| 200 | `AvailabilityWindow` | The survey's and the collector's opening and closing times, and accommodations' windows |
| 300 | `WeeklySchedule` | The days and hours of the weekly schedule, in the site time zone |
| 400 | `Audience` | Anyone, signed-in users with Respond to surveys, members of user audiences, or invited people only |
| 500 | `Password` | The collector's password |
| 600 | `Captcha` | A CAPTCHA for respondents who are not signed in and not invited (reCAPTCHA feature). It fails closed: with the reCAPTCHA feature off, a survey that requires a CAPTCHA refuses such respondents with a message, and the access page refuses to turn the requirement on |
| 700 | `Invitation` | One response per invitation |
| 800 | `RespondentLimits` | Total, per day, per week (from the site's first day of the week), per month, and the wait between responses; accommodations add attempts |
| 900 | `MaxResponses` | The survey's and the collector's most completed responses |
| 1000 | `Quota` | Full quotas (on completion) |

Limits count completed or started responses per respondent, recognized by the user account, the invitation, a browser cookie (weak: it can be cleared) or not at all. Test responses (preview) skip limits and windows.

## Anonymity

**Identified** responses store the user and party (and the IP address when the survey collects it). **Anonymous** responses store none of these, no invitation, and nothing else the caller sends that could name the person:

- No properties (`StartResponseRequest.Properties` are dropped).
- A context keeps its id only when its kind is registered as shared (`AddSurveyContextKind(kind, name, identifiesRespondent: false)`, such as a Learning course); any other context keeps its kind with the id `anonymous`. Hidden variables are the author's choice and are kept, so don't collect identifying values in them on an anonymous survey.
- Limits use an HMAC of the respondent under the survey's **anonymity secret**, in a participation record (`SurveyParticipation`, per day) that holds no response id, no time finer than the day (the cooldown of an anonymous survey counts from the end of the day of the last completion) and only a hash of the context.

The anonymity secret is kept in the survey document only as ASP.NET Data Protection ciphertext (purpose `CrestApps.OrchardCore.Surveys.AnonymitySecret.v1`), whose keys live outside the database, so reading the database is not enough to recompute a respondent's key. There is no fallback to the survey id. `SurveyAnonymitySecrets` reveals it, makes respondent keys and signs invitation links. The `SurveyAnonymityMigrations` step protects the secrets of existing surveys in place (keeping their values, so limits and links keep working) and clears the exact times of existing anonymous participation records. If the Data Protection keys are missing (a key ring that isn't persisted, web-farm nodes that don't share keys, a database restored on another host), the secret is never replaced on its own: the survey fails closed (starting, completing and sending invitations stop, and the error is logged) until the keys are restored. When they can't be, the survey's **Access and settings** page shows the problem and a **Reset the anonymity secret** button (Manage the survey, with a confirmation). Resetting starts the limits over, re-issues every invitation's stored token hash so sending the invitations again sends working links, and leaves links sent before unusable. The token hash is also re-issued whenever an invitation is sent or reminded again.

Anonymity can't change once a survey has responses (test responses aside): the access editor refuses the change, so responses given under one promise are never shown under the other. Delete the responses or copy the survey to change it. Outputs of anonymous surveys honor the minimum group size, never below the site's floor ([Results and reports](./results-and-reports.md#anonymous-surveys)).

## Invitations

With **Survey Invitations** on, an invitation collector invites users of the site (by user name), a user audience, a cohort, or e-mail addresses. One **Invite** reads **at most 5,000 recipients** (the audience or cohort members past that number, and the addresses past it, are not invited; invite the rest in another batch). Each invitation (`SurveyInvitation`) has a personal link `/s/i/{token}`; the token is an HMAC of the invitation id under the survey's secret, and only its hash is stored. Users get a notification (type `SurveyInvitation`), which follows the user's own notification preferences; other people get an e-mail rendered from the collector's Liquid template. The template runs in a sandbox: Fluid's own filters and only `Survey.Name`, `Survey.Title`, `Invitation.DisplayName`, `Invitation.Email`, `Invitation.ExpiresUtc`, `Link` and `UnsubscribeLink`, with a step limit; the site's Liquid objects and filters (users, content, queries) are not available. An e-mail invitation is for exactly one address: a list or a malformed address is refused by the invitation service, whoever calls it. The site sends at most **Settings > Surveys > Invitation e-mails a day** (2,000 by default) invitation and reminder e-mails a day; the rest stay pending and go out on a later day. Links may expire.

Reminders (type `SurveyReminder`) go every N days, up to a set number, to people who have not completed the survey; they stop on completion. An anonymous response never names its invitation: it keeps an `InvitationSeal`, an HMAC of the invitation id under the survey's protected secret that nothing stored links back to the invitation. Each invitation's index keeps a second HMAC of its seal under the same secret, so completing the response finds the invitation with one indexed query (invitations made before that value was kept are still matched by recomputing their seals), marks it **Completed** (so its link can't start another response) and clears the seal. Someone who reads the database sees the seal and the lookup value but can't link them without the secret.

Sending invitations needs **Manage survey invitations**, or **Manage invitations of own surveys** on the surveys the user owns. **Manage own surveys** alone does not allow it, because invitations send site e-mail.

`ISurveyInvitationService` creates and sends invitations from code, and `ISurveyLinkService.CreateLinkAsync` makes a personal link for a respondent and a context (used by the **Create Survey Link** workflow task).

### Opt-outs and unsubscribes

Survey Invitations depends on **Outreach**, so turning it on turns Outreach on. Invitations are sent by the invitation service and its hourly task rather than as Outreach runs, but every e-mail invitation and reminder honors the same opt-outs as an Outreach run:

1. Just before an e-mail invitation or reminder is sent, every `ISurveyInvitationRecipientPolicy` is asked (`AddSurveyInvitationRecipientPolicy<T>()`). The Invitations feature registers one that asks Outreach's `IOutreachRecipientPolicy` checks for the address on the `Email` consent channel. Outreach's consent policy asks the consent service, so contact preferences and address opt-outs (Omnichannel contacts), customer marketing preferences and survey unsubscribes all apply.
2. An invitation whose address opted out is not sent and gets the status **Opted out**; it gets no reminders.
3. Every invitation e-mail ends with an unsubscribe link, `/s/i/{token}/unsubscribe`, whatever the collector's message says. Opening it changes nothing; confirming it (a post, which mail apps' one-click unsubscribe may send without a form token) marks the invitation **Opted out** and records the address through `IConsentService` with the source `survey-invitations`. The Invitations feature registers that consent source; it keeps only an HMAC of the address, keyed by a site key protected with Data Protection (hashes stored before the key existed are still honored), so no survey invites the address again and every other send that asks the consent service skips it. The list is saved with a concurrency check, so two unsubscribes at once never lose one; if its key can't be read, every address counts as unsubscribed until the keys are restored.

Invitations to users of the site go to their notification inbox and are not checked against e-mail opt-outs.

## Embedding

With **Survey Embedding** on:

- `SurveyPart` (attachable) picks an open survey and one of its links, and shows the survey in the page, as a button or as a link. The `SurveyWidget` content type holds the part.
- The shortcode `[survey name="customer-satisfaction" collector="..." display="link"]` renders a button (or a link) that opens the survey (with Shortcodes on).

## Quotas

A quota counts completed responses that match its rule. When the count reaches the limit, new matching respondents are screened out with the quota's ending, or the survey closes, or nothing happens (count only). Counters change only under the survey lock, and the **Survey Quota Reached** workflow event fires.

## Responses

A response moves through `InProgress`, `Completed`, `ScreenedOut`, `Abandoned` and `Voided`. A completed response can be reopened only when the survey allows changes after finishing, and keeps its version. Unfinished responses idle longer than the survey's limit (168 hours by default) are abandoned by the maintenance task.

Consumers start responses for their own records through `ISurveyResponseService.StartAsync` with a `Context` (a `RecordRef`), their own variables and properties.
