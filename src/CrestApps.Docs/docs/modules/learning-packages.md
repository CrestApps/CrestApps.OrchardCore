---
sidebar_label: Learning Packages
title: Learning Packages
description: "The learning package course item of Learning: SCORM 1.2 and SCORM 2004 packages imported by authors and run by learners in a sandboxed frame, with the run-time data checked and kept on the server."
---

**Learning Packages** (`CrestApps.OrchardCore.Learning.Packages`, with `Learning.Packages.Abstractions` and `Learning.Packages.Core`) adds the **Learning package** course item to [Learning](./learning). Its **Learning SCORM Packages** feature lets an author import a package, learners run it in the course player, and what the package reports count in the learner's progress. It implements:

- SCORM 1.2 (ADL SCORM 1.2, the Run-Time Environment and Content Aggregation Model);
- SCORM 2004 3rd and 4th Edition (Content Aggregation Model and Run-Time Environment; Sequencing and Navigation not implemented: a package runs its first SCO, without sequencing rules, rollup or navigation requests).

| Feature | Id | Depends on |
| --- | --- | --- |
| Learning SCORM Packages | `CrestApps.OrchardCore.Learning.Packages.Scorm` | Learning |

One feature imports both versions; the version of a package is read from its manifest. cmi5 and xAPI packages are not supported yet; importing one is refused with a message.

## The learning package item

A learning package is a content item of the **Learning package** type (`LearningPackage`, defined in code with `TitlePart`, `HtmlBodyPart` for an introduction, `LearningPackagePart` and `CourseItemPart`). Its course item kind is `LearningPackage`. Add it to a course outline like any other course item.

Its completion mode is one of:

| Mode | The item completes when |
| --- | --- |
| **When the package reports it complete** (`PackageReported`, the default) | SCORM 1.2: `cmi.core.lesson_status` is `completed` or `passed`. SCORM 2004: `cmi.completion_status`, as evaluated against the completion threshold, is `completed`. |
| **Passed** | The package passes the learner (`passed`; in SCORM 2004 `cmi.success_status`, as evaluated against the scaled passing score); `failed` fails the item. Without a status, the item's pass mark is compared with the score. |
| **Marked done** | The learner marks the item done in the player; the package's status does not complete it. |

`LearningPackagePart` holds only what an import found; nothing in it is edited by hand:

| Property | Meaning |
| --- | --- |
| `PackageFileId`, `FileName` | The package in the protected file store and the name of the uploaded file. |
| `Standard`, `ManifestIdentifier`, `Title` | The version read from the manifest (`SCORM 1.2`, `SCORM 2004 3rd Edition` or `SCORM 2004 4th Edition`), the manifest's identifier and the title of its default organization. |
| `LaunchItemIdentifier`, `LaunchItemTitle`, `LaunchPath`, `LaunchQuery` | The item that is launched and the file and query it opens. |
| `MasteryScore`, `MaxTimeAllowed`, `TimeLimitAction`, `LaunchData` | SCORM 1.2: `adlcp:masteryscore`, `adlcp:maxtimeallowed`, `adlcp:timelimitaction` and `adlcp:datafromlms` of the launched item. SCORM 2004: no mastery score; the `attemptAbsoluteDurationLimit` of the item's `imsss:limitConditions`, `adlcp:timeLimitAction` and `adlcp:dataFromLMS`. |
| `CompletionThreshold`, `ScaledPassingScore`, `ObjectiveIds`, `HasSequencing` | SCORM 2004 only: the completion threshold (0 to 1), the scaled passing score (-1 to 1), the ids of the item's objectives, and whether the manifest has sequencing information (recorded, not applied). |
| `ScoCount`, `FileCount`, `ExpandedSize` | The SCOs of the default organization, and the files and their size. |
| `ImportedUtc`, `ImportedBy` | When and by whom the package was imported. |

## Importing a package

Save the learning package item, then choose **Import a package** in its editor (`/Admin/learning/packages/{contentItemId}/upload`). The page needs the **Import learning packages** permission (`ImportLearningPackages`, implied by Manage courses, given to administrators) and the right to edit the item. The import goes into a draft of the item, so learners keep the current package until the draft is published.

The upload is a .zip file of up to 500 MB. It is copied to a temporary folder of the tenant (`ITempDirectoryProvider`) and extracted there. Every entry is checked before anything is written, and the import is refused, with the entry named, when an entry:

- has an absolute path, a drive letter, a network share, or a `..` segment that climbs out of the package;
- has a colon (a stream or a drive), a control character, a name Windows keeps for a device, or a name ending in a dot or a space;
- is a symbolic link;
- has the same path as another entry, ignoring case.

The import is also refused when the archive has more than 10,000 entries or more than 1 GB once extracted; the bytes are counted as they are written, so the size an entry claims is never trusted. The limits are `LearningPackageOptions` (`MaxUploadBytes`, `MaxEntries`, `MaxExpandedBytes`).

Then `imsmanifest.xml` is read:

- The **version** is the metadata's `schemaversion`: `1.2`, `2004 3rd Edition` or `2004 4th Edition` (`2004 2nd Edition` and `CAM 1.3` follow the 3rd Edition's rules). Without one, the SCORM 2004 namespaces (`adlcp_v1p3`, `adlseq_v1p3`, `adlnav_v1p3`, `imsss`) make the package SCORM 2004 4th Edition, whose limits are the larger; otherwise it is SCORM 1.2. The item's editor shows the version.
- The **default organization** is the one `organizations/@default` names; without a default, or when it names no organization, the first organization is used.
- Its items are read depth first in document order. An item whose resource has `adlcp:scormtype="sco"` is a SCO; any other is an asset.
- A SCO's file is its resource's `href` under the `xml:base` of the manifest, of the `resources` element and of the resource. It opens with the query of the `href` and the item's `parameters`. A `href` outside the package, or a web address, is refused, and the file must be in the package.
- SCORM 1.2: the launched item's `adlcp:masteryscore` (0 to 100), `adlcp:maxtimeallowed`, `adlcp:timelimitaction` and `adlcp:datafromlms` are kept.
- SCORM 2004: the resource's `adlcp:scormType` says SCO or asset. The launched item's `adlcp:completionThreshold` is kept: its text in the 3rd Edition; in the 4th Edition its `minProgressMeasure` (1.0 when missing), only when `completedByMeasure` is `true`. From the item's `imsss:sequencing` (or the shared definition its `IDRef` names in `imsss:sequencingCollection`), the ids of its `imsss:objectives` (the primary objective first), the primary objective's `imsss:minNormalizedMeasure` when it is `satisfiedByMeasure` (the scaled passing score, 1.0 when missing) and the `attemptAbsoluteDurationLimit` are kept, with `adlcp:timeLimitAction` and `adlcp:dataFromLMS`. A threshold outside 0 to 1, or a measure outside -1 to 1, is refused.
- A manifest with a DTD is refused; no external entity is ever resolved.

**The first SCO is launched.** A package with one SCO, or a simple package with several SCOs whose first SCO is the course, runs as expected. A package that needs the learner to move from SCO to SCO (sequencing between SCOs) is not supported: only the first one runs, and the editor says how many SCOs the package has. SCORM 2004 sequencing rules, rollup and navigation requests are not applied; the editor says so when the manifest has sequencing information. A package of assets only, with no SCO, is refused because it cannot report progress.

A clean archive of only the extracted files, under their checked names, is kept in the protected file store (`IProtectedFileStore`), through Orchard Core's file creation pipeline, so an antivirus scanner registered there can refuse it. The temporary folder is always deleted. The package files are not in the media library and no URL serves them except the launch endpoint below.

## Running a package

The package panel of the item in the learner player shows the learner's status, score, time and when they last opened the package, and a **Launch** button (**Resume** after a session the package suspended, **Review** once the course is completed). Results are marked as reported by the browser.

### The sandbox

Package content is untrusted script. It runs in a frame on an **opaque origin**:

- The player embeds `<iframe sandbox="allow-scripts allow-forms allow-popups">`.
- Every file of the package is served with `Content-Security-Policy: sandbox allow-scripts allow-forms allow-popups`, without `allow-same-origin`. Even a package file opened directly in a browser tab runs on an opaque origin.
- The frame therefore cannot read the learner's cookies, cannot read or script the player page, and cannot call the site's endpoints as the learner.
- The responses also carry `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` and the content type of the file's extension.
- They carry `Access-Control-Allow-Origin: *`, because the package's own scripts, fonts and data files are requested from an opaque origin. What opens a file is the launch token in the path, never a cookie, so this gives nothing to a reader who does not already hold the token.

### The launch token

**Launch** posts to `/learning/packages/{itemId}/launch` with the learner's session and antiforgery token. It checks the learner's own enrollment, access window and the item's release rules, starts a session and returns a launch token. The token:

- is made with the tenant's ASP.NET Core Data Protection, with a purpose of its own;
- is bound to the launch, the enrollment, the package item, the package file and the learner;
- expires after 8 hours (`PackageLaunchTokens.Lifetime`).

The frame loads `/learning/packages/run/{token}/{path}`. The token is in the path, so the package's relative links resolve. Only files of the token's package are served, at normalized paths that cannot leave the package. A token stops opening files when the enrollment is withdrawn or no longer the learner's, or when another package is published into the item.

### The SCORM 1.2 API

The server puts the learner's run-time data and a small script at the start of every HTML page of the package. For a SCORM 1.2 package the script defines `window.API` with the eight SCORM 1.2 functions: `LMSInitialize`, `LMSFinish`, `LMSGetValue`, `LMSSetValue`, `LMSCommit`, `LMSGetLastError`, `LMSGetErrorString` and `LMSGetDiagnostic`. A SCO that searches its own window, its parents and its opener for `API` finds it. The script answers the SCO at once, with the SCORM 1.2 error codes, from its copy of the data.

### The SCORM 2004 API

For a SCORM 2004 package the script defines `window.API_1484_11` with the eight SCORM 2004 methods: `Initialize`, `Terminate`, `GetValue`, `SetValue`, `Commit`, `GetLastError`, `GetErrorString` and `GetDiagnostic`. It answers with the SCORM 2004 error codes, including the state rules of the API: `Initialize` twice is 103 and after `Terminate` 104; `Terminate` before `Initialize` is 112 and twice 113; `GetValue`, `SetValue` and `Commit` before `Initialize` are 122, 132 and 142, and after `Terminate` 123, 133 and 143. Its messages use their own protocol name, which the launch tells the player.

### Messages to the player

The script tells the player page what the SCO set, committed and finished (terminated) **only through `postMessage`**. The player takes a message only when all of these hold:

- it comes from its own frame (`event.source`);
- its origin is `"null"`;
- it carries the protocol name and the id of the current launch.

The player sends the values to `/learning/packages/runtime/{enrollmentId}/{itemId}` with the learner's session and antiforgery token: when the SCO commits or finishes, every 30 seconds, and when the page is left. The frame itself never holds a credential that can do more than this launch's runtime calls.

### The runtime state on the server

The server keeps one runtime state per enrollment and package item (the `LearningPackageRuntime` collection). A runtime call must carry the token of the latest launch by the signed-in learner, for the enrollment and item it names. A call from an older launch (another tab) or after the session finished is refused. Writes to one runtime state run under one lock per enrollment and package.

For a SCORM 1.2 package, every value is replayed through the SCORM 1.2 data model on the server (`Scorm12DataModel`), so a value the browser accepted is never trusted:

- read-only, write-only and read-write elements;
- the vocabularies of `lesson_status`, `exit`, `entry`, `credit`, the objective status and the interaction type and result;
- scores as CMIDecimal from 0 to 100, or blank;
- CMITimespan for `session_time` and `latency`, and CMITime for the interaction time;
- CMIString255 for `lesson_location`, CMIString4096 for `suspend_data` and `comments`, and CMIIdentifier for the ids;
- the student preference ranges;
- the `_children`, `_count` and `_version` keywords, and arrays that grow only at their count.

A refused value is reported with its error code: 201, 202, 203, 402, 403, 404 or 405. The API itself also answers 101 and 301.

The learner's id and name, the credit, the lesson mode, the launch data and the student data are set by the server at every launch. The entry is `ab-initio` the first time, `resume` after a session that exited with `suspend`, and empty otherwise. When a session finishes, its `session_time` is added to `total_time`. A status the package never set is then `completed`.

**Mastery score.** When the package sends a raw score and its status is `not attempted`, `completed` or `browsed`, the mastery score of the manifest decides: `passed` when the raw score reaches it, `failed` otherwise. A `passed` or `failed` the package sets itself stands, and so does `incomplete`. This only happens in credit mode.

### The SCORM 2004 runtime state

A SCORM 2004 state records the package's version (states written before SCORM 2004 have none and are SCORM 1.2). Every value is replayed through the SCORM 2004 data model on the server (`Scorm2004DataModel`), the same table the frame script uses:

- every element of the RTE data model with its access: `cmi.completion_status`, `cmi.success_status`, `cmi.score.*` (`scaled` from -1 to 1), `cmi.progress_measure` (0 to 1), `cmi.completion_threshold`, `cmi.scaled_passing_score`, `cmi.location` (1,000 characters), `cmi.suspend_data` (64,000 characters in the 4th Edition, 4,000 in the 3rd), `cmi.session_time` and `cmi.total_time`, `cmi.exit`, `cmi.entry`, `cmi.credit`, `cmi.mode`, `cmi.learner_id`, `cmi.learner_name`, `cmi.launch_data`, `cmi.max_time_allowed`, `cmi.time_limit_action`, `cmi.learner_preference.*`, `cmi.interactions.n.*`, `cmi.objectives.n.*`, `cmi.comments_from_learner.n.*` and `cmi.comments_from_lms` (read only);
- the data types: `localized_string_type` with its `{lang=}` delimiter, `long_identifier_type` and `short_identifier_type`, `real(10,7)` with each element's range, ISO 8601 timestamps from 1970 to 2038 and ISO 8601 durations;
- objective ids unique and never changed; an objective or an interaction is added by its id first; a learner response or correct response pattern needs the interaction type first and follows that type's format; true-false, likert, numeric and other interactions have one correct response;
- `_version` (`1.0`), `_children` and `_count`;
- `adl.nav.request` is checked against its vocabulary (`{target=}jump` only in the 4th Edition) and stored; every `adl.nav.request_valid` element answers `unknown`.

A refused value is reported with its error code: 301, 351, 401, 403, 404, 405, 406, 407 or 408. A value longer than the smallest permitted maximum is refused (406), so the store stays bounded.

**Status evaluation.** A read of `cmi.completion_status` with a completion threshold returns `completed` when `cmi.progress_measure` reaches it, `incomplete` when it does not, and `unknown` when no progress measure was set; a read of `cmi.success_status` with a scaled passing score returns `passed` when `cmi.score.scaled` reaches it, `failed` when it does not, and `unknown` without a scaled score. Without a threshold or passing score, the value the package set is returned. Progress uses the evaluated values.

**Attempts.** A launch after a session that exited with `suspend` resumes the attempt: `cmi.entry` is `resume` and the data is kept. Any other exit (`normal`, `logout`, `time-out` or none) ends the attempt, and the next launch starts a new one: `cmi.entry` is `ab-initio`, and the location, suspend data, statuses, scores, progress measure, total time, interactions and the learner's comments are reset, and the objectives go back to the manifest's ids. The learner's preferences and the comments from the LMS are kept. When a session terminates, its `cmi.session_time` is added to `cmi.total_time` of the attempt. The learner's id and name, the credit, the mode, the launch data, the thresholds and the time limits are set by the server at every launch. A package of the other standard published into the item starts the learner afresh.

### Progress

When the package commits or finishes, the result goes to Learning (`ILearnerProgressService.RecordResultAsync`) as the learner's own work:

- the learner's access window and the item's release rules apply;
- the score is the raw score as a percentage of the range the package set (`score.min` to `score.max`), or the raw score; in SCORM 2004, `score.scaled` times 100 when the package set it, else the raw score the same way;
- `passed` or `failed` is kept (in SCORM 2004 from the evaluated `success_status`, and the item is complete when the evaluated `completion_status` is `completed`);
- the attempts are the sessions in SCORM 1.2 and the attempts in SCORM 2004;
- the result is **marked self-reported** on the item's progress and in the completion snapshot.

A course item a learner completes through a package is therefore recorded as reported by the browser. Because a learner can change what the browser sends, such a completion does not count toward a certification by default: the **Course completed** requirement of a certification path skips a completed enrollment in which a required item's result was reported by the browser. Tick **Let a result reported by the browser count toward a certification** on the item to allow it. A completed enrollment opens the package for review (`lesson_mode` review in SCORM 1.2, `cmi.mode` review in SCORM 2004, and `credit` no-credit), and nothing it sends is kept.

## Data portability and personal data

The **LearningPackageRuntimeStates** recipe and deployment steps move the runtime states of both versions (a SCORM 2004 state with its `Standard`, `Attempts` and `Cmi2004`), naming learners by user name. The deployment step is opt-in, needs **Export learner records** and is recorded. The package content item exports with its part, but **the package file itself does not travel** with a recipe or a deployment, as for coursework files: import the package again on the target site.

A learner's personal data export lists their runtime state on each package. Erasure deletes every runtime state whose enrollment is not completed. A runtime state of a completed enrollment keeps its status, score, time and dates, and loses the learner's name and id, the suspend data, the location, the comments (the learner's comments in SCORM 2004), the language preference and the interactions. A SCORM 2004 state is exported with its version, attempts and evaluated statuses.

## Conformance testing

### SCORM 1.2

Conformance suite / certification: not run (needs an external account).

The suite for this is the ADL SCORM 1.2 Conformance Test Suite (ADL's SCORM 1.2 Test Suite, an LMS RTE test). To run it:

1. Obtain the suite from ADL (adlnet.gov).
2. Install it.
3. Import its test content packages into a site, each into a learning package item of a course.
4. Launch each package as a learner.
5. Record the logs the suite writes.

### SCORM 2004

Conformance suite / certification: not run (needs an external account).

The suite for this is ADL's SCORM 2004 4th Edition Conformance Test Suite: its LMS test, which includes the Run-Time Environment tests and the Sequencing and Navigation tests. The Sequencing and Navigation tests would fail, because sequencing is not implemented. To run it:

1. Obtain the suite from ADL (adlnet.gov).
2. Install it.
3. Import its test packages into a site, each into a learning package item of a course.
4. Launch each package as a learner.
5. Keep the logs the suite writes.

This module is not certified by ADL and makes no conformance claim.

## Not supported yet

- cmi5, xAPI and AICC packages.
- SCORM 2004 Sequencing and Navigation: sequencing rules, rollup across SCOs, navigation requests (`adl.nav.request` is stored and not acted on) and the delivery controls. In particular, a SCO that ends without reporting `cmi.completion_status` does not complete the item (a sequencing implementation would mark it complete when `completionSetByContent` is false); choose **Marked done** for such a package.
- SCORM 2004 shared data stores (`adl.data`, declared with `adlcp:data` in a 4th Edition manifest): `adl.data._count` is 0.
- SCORM 2004 global objectives shared between SCOs or packages.
- Navigation between the SCOs of a multi-SCO package; only the first SCO runs.
- Byte ranges: a large video in a package is sent whole.
- A package that moves between its own HTML pages starts each page with the data the server had when the page loaded. A value set on the page before and not yet committed reaches the server shortly after, but the new page does not see it.
- A per-learner view of the runtime state for staff, a data source for reports, workflow events and notifications.
