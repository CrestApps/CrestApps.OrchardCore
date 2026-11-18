---
sidebar_label: External Tools (LTI)
title: Learning External Tools (LTI 1.3)
description: "LTI 1.3 tools in courses with the site as the platform: tool registration, launches, Deep Linking 2.0, Names and Role Provisioning Services 2.0 and Assignment and Grade Services 2.0, on Orchard Core OpenID and Secrets."
---

**Learning External Tools** (`CrestApps.OrchardCore.Learning.Lti`, with `Learning.Lti.Abstractions` and `Learning.Lti.Core`) lets courses of [Learning](./learning) use LTI tools, with the site as the LTI platform. It implements LTI 1.3 Core, Deep Linking 2.0, Names and Role Provisioning Services 2.0 and Assignment and Grade Services 2.0. It makes no certification or conformance claim (see [Conformance](#conformance)).

| Feature | Id | Depends on |
| --- | --- | --- |
| External Tools | `CrestApps.OrchardCore.Learning.Lti` | Learning, OpenID Authorization Server (`OrchardCore.OpenId.Server`), OpenID Token Validation (`OrchardCore.OpenId.Validation`), Secrets (`OrchardCore.Secrets`) |

## Set up the OpenID server

The tools' service requests use Orchard Core's OpenID server, so configure it once:

1. **Security → OpenID Connect → Settings → Authorization Server**: turn on the token endpoint and allow the client credentials flow. Use the `JWT` access token format, so the validation feature can read the tokens.
2. **Security → OpenID Connect → Settings → Token Validation**: set the tenant to this tenant (or to the tenant whose authorization server you use).

The feature's migration adds the five LTI service scopes to the OpenID scopes.

## Register a tool

**Learning → External Tools** (permission **Manage external tools**, `ManageExternalTools`, security critical) lists the tools with Orchard Core's admin list and edits them. A tool has:

| Field | What it is |
| --- | --- |
| Name, Notes | How authors pick the tool. |
| Client ID | The tool's client id on this platform. Saving the tool creates or updates an OpenID application with this id: confidential (it signs in with its key, never a secret), with the client credentials flow, the redirect URIs below, the scopes the tool may use and the tool's key set. A client id that an OpenID application not made by External Tools already uses is refused, and such an application is never changed. |
| Deployment ID | Sent with every launch. |
| Login initiation URL | The tool's OIDC login initiation URL, where every launch starts. |
| Redirect URIs | Where the tool receives launches, one a line. A launch to any other address is refused. |
| Target link URI | The default launch URL; a course item can name another. |
| Deep Linking URL | Where course staff pick content in the tool. Empty when the tool has no Deep Linking. |
| Tool's JWKS URL, or the tool's key set | The tool's public keys, as a URL (read when the tool is saved and again when the tool signs with a key id the site does not know) or a pasted JSON Web Key Set. They verify the tool's Deep Linking responses and client assertions. |
| Custom parameters | `name=value` lines added to every launch; a course item's win over the tool's. |
| What the tool learns | The privacy level: an anonymous id only, the id and the person's name, or the id, the name and the e-mail address. It applies to launches, custom parameters and the roster. |
| May read the course roster / May post grades | Turn on Names and Role Provisioning Services and Assignment and Grade Services for the tool. |
| Can be launched | Off stops every launch and refuses the tool's service requests. |

The editor shows the platform details to give the tool: the issuer (the site's base URL, or the request's address when none is set), the authorization endpoint (`/learning/lti/authorize`), the platform JWKS URL (`/learning/lti/jwks`), the token endpoint (the OpenID server's `/connect/token`), the Deep Linking return URL (`/learning/lti/deep-linking/return`), the client id and the deployment id.

Removing a tool removes its OpenID application; its course items stop launching.

### The id a tool knows a person by

A tool never gets a user id. Each tool has a random salt, and the `sub` of a launch, the roster's `user_id` and the `$User.id` variable are an HMAC of the user id with that salt. The ids differ between tools, stay the same for a tool across launches, and survive an export and import of the tool (the salt is part of the recipe step).

## The External tool course item

The **External tool** course item (`ExternalToolItem`, with `ExternalToolItemPart`) names a tool, an optional target link URI and custom parameters, a line item label and a maximum score. An enrolled learner sees a **Launch** button in the player; the launch opens in a new window.

### The launch

1. `GET /learning/lti/launch/{itemId}` checks that the person may open the item (an active enrollment, the release rules of Learning, a published item of a published course, an enabled tool) and redirects to the tool's login initiation URL with `iss`, `login_hint`, `lti_message_hint`, `target_link_uri`, `client_id` and `lti_deployment_id`. The message hint is protected with Data Protection, names the person, the tool, the item and the roles, and expires after 5 minutes.
2. The tool sends its authentication request to `/learning/lti/authorize` (GET or POST). The platform checks `scope=openid`, `response_type=id_token`, `response_mode=form_post`, `prompt=none`, the client id, a registered redirect URI, the nonce (refused when seen before), the message hint (signed, unexpired, for this tool) and that the signed-in person is the one the hint was made for.
3. The platform posts the id token and the `state` to the redirect URI. The id token is signed with the platform key (RS256) and carries the LTI 1.3 claims: message type, version `1.3.0`, deployment id, target link URI, resource link, roles, context (the course), tool platform, launch presentation (the return URL is the item in the player), custom parameters, the person's names and e-mail within the privacy level, and the Assignment and Grade Services and Names and Role Provisioning Services claims when the tool may use them.

Roles: a learner gets `Learner`; course staff get `Instructor` and, for assistants and graders, `TeachingAssistant` or `Grader`; observers get `Mentor`; a person who manages every course gets `Administrator` and `Instructor`.

The sign-in cookie is `SameSite=Lax`, so a browser leaves it off a login request a tool posts from its own site. The authorization endpoint answers such a post, when it arrives without a signed-in user, with `303 See Other` to the same request as a GET (only the authentication request's own parameters are kept), a top-level navigation that carries the cookie. A request made inside a frame of another site still arrives without the cookie and is refused, which is why launches open in a window.

Substitution variables: `$Context.id`, `$Context.title`, `$ResourceLink.id`, `$ResourceLink.title`, `$User.id`, `$Person.name.full`, `$Person.name.given`, `$Person.name.family`, `$Person.email.primary` and `$Membership.role`. A variable the privacy level withholds, or one the platform does not support, is sent as written.

### The platform key

The platform key is a key set of the shared signing keys (`SigningKeys.Abstractions`/`.Core`, also used by Agentic Commerce) (`Learning.Lti.Platform`): an `RsaKeySecret` in Orchard Core Secrets, created when the feature is enabled, rotated every 90 days, with the previous key published at `/learning/lti/jwks` for 30 more days so a tool that cached the key set still verifies a launch. No key is kept anywhere else.

## Deep Linking 2.0

A tool with a Deep Linking URL shows under **Content from external tools** on the course editor, for people who hold **Manage the courses you teach** for the course (or **Manage courses**). **Pick from** a tool sends an `LtiDeepLinkingRequest` launch to the tool, with `accept_types` `ltiResourceLink`, the return URL and a `data` value the platform signs (one hour, used once).

The tool posts its `LtiDeepLinkingResponse` JWT to `/learning/lti/deep-linking/return`. The platform checks that it is signed by one of the tool's keys, that `iss` is the client id and `aud` the platform, `exp` and `iat`, the message type and version, the deployment id, a nonce not seen before, and that `data` is its own, unexpired, for this tool and unused. A response that fails any check changes nothing. Each `ltiResourceLink` content item becomes an External tool item at the end of the course's last section, with its title, URL, custom parameters and line item (label and maximum score); the course gets a draft when it needs one. Other content item types are not added; the page names them. The tool's `msg` and `errormsg` are shown.

## Services

The services are at `/learning/lti/nrps/{courseId}/memberships` and `/learning/lti/ags/{courseId}/lineitems...`. They take only a bearer access token of Orchard Core's `Api` scheme whose client is an enabled registered tool; the sign-in cookie never opens them. The course must use the tool (an External tool item of the tool in the course).

### Service tokens

A tool gets a token from the OpenID server's token endpoint with the client credentials grant and a `private_key_jwt` client assertion (1EdTech Security Framework 4.1): `iss` and `sub` the client id, `aud` the token endpoint URL, `exp`, a `jti` not seen before, signed with one of the tool's keys. OpenIddict 7 accepts only the newer `client-authentication+jwt` form, so the feature adds an OpenIddict handler that validates the assertions of registered tools in the LTI form and leaves every other client to OpenIddict. Scopes, permissions and the token itself are OpenIddict's and Orchard Core's.

| Scope | Service |
| --- | --- |
| `https://purl.imsglobal.org/spec/lti-nrps/scope/contextmembership.readonly` | Roster |
| `https://purl.imsglobal.org/spec/lti-ags/scope/lineitem` | Line items, read and write |
| `https://purl.imsglobal.org/spec/lti-ags/scope/lineitem.readonly` | Line items, read |
| `https://purl.imsglobal.org/spec/lti-ags/scope/score` | Post scores |
| `https://purl.imsglobal.org/spec/lti-ags/scope/result.readonly` | Read results |

### Names and Role Provisioning Services 2.0

`GET .../memberships` returns the course's staff and learners as a `application/vnd.ims.lti-nrps.v2.membershipcontainer+json`, with `role` and `rlid` filters and `limit` paging (a `Link: rel="next"` header). A learner is `Active` while the enrollment is active or completed. Names and e-mail addresses follow the tool's privacy level.

### Assignment and Grade Services 2.0

- **Line items**: list (with `resource_link_id`, `resource_id`, `tag` and `limit`), get, create (201), update and delete. A line item belongs to one tool and one course; one the platform made for an External tool item is bound to it through `resourceLinkId`.
- **Scores**: `POST .../lineitems/{id}/scores` with `application/vnd.ims.lis.v1.score+json`. `userId`, `activityProgress`, `gradingProgress` and an ISO 8601 `timestamp` are required, `scoreMaximum` is required with `scoreGiven`, and the `userId` must be a member of the course. A score whose timestamp is not later than the last one stored for that person is refused with 409. The answer is 204.
- **Results**: `GET .../results`, with `user_id` and `limit`.

A `FullyGraded` score on a line item bound to an External tool item becomes that item's result in the learner's progress, as a percentage of the line item's maximum, marked as sent by the server (not reported by the browser). The gradebook and completion rules use it like any other item result.

## Data portability, personal data and workflows

- **Recipe and deployment.** The `LearningExternalTools` step moves the tools' settings (`Tools`, by id), including the subject salt, so the ids a tool knows people by do not change. The tools' OpenID applications move with Orchard Core's OpenID applications step. Scores are not exported.
- **Personal data.** The export lists, for each tool, the id it knows the person by and the privacy level, and every score a tool posted for the person. Erasure deletes the scores and warns that the tools keep their own records, which the person must ask each tool's provider to erase. A result already in the learner's progress stays, as Learning keeps it.
- **Workflow.** The **External Score Received** event starts when a tool's score is stored, for every grading progress or the ones listed, with the score id as the correlation id. Its input holds `ToolId`, `ToolName`, `ClientId`, `CourseId`, `CourseItemId`, `LineItemId`, `LineItemLabel`, `UserId`, `ScoreGiven`, `ScoreMaximum`, `Percent`, `ActivityProgress`, `GradingProgress`, `RecordedInProgress` and `TimestampUtc`.

## Extending

- `ILtiScoreObserver` (`AddLtiScoreObserver<T>()`) learns of each stored score with its tool and line item.
- `IExternalToolManager` and `IExternalToolStore` manage tools; `ILtiPlatform` describes the platform; `LtiSubjects.For` gives the id a tool knows a user by.

## Conformance

Conformance suite / certification: not run (needs an external account). The suite is 1EdTech's LTI Advantage certification suite for platforms, which needs a 1EdTech membership account. To run it, a person with that account signs in to 1EdTech's certification site, registers the suite as a tool on a test site reachable from the internet (**Learning → External Tools**, with the suite's client id, login initiation URL, redirect URIs and key set), gives the suite the platform details the editor shows, adds an External tool item of the suite to a test course with enrolled learners and staff, and runs the platform tests for LTI 1.3 Core, Deep Linking 2.0, Names and Role Provisioning Services 2.0 and Assignment and Grade Services 2.0, then keeps the report.

## Not built

- LTI 1.1 and LTI 1.3 as a tool (the site is only the platform).
- Dynamic Registration; tools are registered by hand.
- Deep Linking content item types other than `ltiResourceLink` (`link`, `file`, `html`, `image`), and Deep Linking from places other than the course editor.
- The `LtiSubmissionReviewRequest` message, Proctoring, Caliper, the Context Groups service and the Assets and Course Groups extensions.
- Line items not bound to an External tool item do not reach the learner's progress or the gradebook; they are stored for the tool only.
- Launches in a frame of the player; a launch opens in a new window.
- Audit Trail events.
