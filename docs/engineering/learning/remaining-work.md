# Learning: remaining work (written 2026-11-19, paused for usage limits)

This is the hand-off for the next session. Phases 0-9 of `lms-plan.md` are built, and review rounds 1-10 are fixed. The PR branch is `ma/learning-lms`. The parent session owns the PR; send it branch heads to cherry-pick, and don't push from here.

## Where things stand

| Branch | Head | State |
| --- | --- | --- |
| `ma/learning-lms` | `d04dd56a4` | PR branch: the stack merged with main, CI fixes. Pushed by the parent session. |
| `ma/lms-usermenu-notifications` | `c937e4c42` | Three commits on `d04dd56a4`: My courses in the user menu, a notification type for every Learning notice, and a `/notifications` page for people without the admin. CI build clean; unit 2117/2117; integration 123/123. Sent to the parent to cherry-pick. |
| `ma/lms-package-content-host` | WIP commit on `c937e4c42` | Only red unit tests (`PackageContentHostTests`, `PackageFileTypesTests`). No implementation yet. Item 1 below. |

- **Test site:** `http://localhost:5137` runs an older build (`c6505af4a`, .NET 10) from `src/Startup/CrestApps.OrchardCore.Cms.Web/App_Data_Learning`, holding the recreated Moodle courses. Sign in as `admin` / `Password1!` (every test user uses `Password1!`). Restart it on the newest .NET 11 build once item 1 is done, with the content host set to `http://127.0.0.1:5137`.
- **Moodle comparison:** the local scratchpad has `moodle-gaps.md`, `moodle-inventory.json`/`.md`, `local/built.json` (production id to local id) and `moodle-files/` (78 downloaded packages and files, 703 MB, customer content: never commit). The scratchpad may be cleared, so the lists below repeat what matters.

## 1. SCORM: separate content host and import allowlist (done on `ma/lms-package-content-host`, not pushed)

Rise and Storyline 360 packages show blank content. They run in several frames that look for the SCORM API through `window.parent`, and the opaque-origin sandbox (CSP `sandbox` without `allow-same-origin`) gives each frame its own origin. 11 of 37 sampled production packages failed this way. Separately, the importer refused 24 of 37 packages for `.cur` and `.docx` files.

Build:
- **Setting:** a per-tenant **package content host** setting, for example `https://packages.example.com`, in site settings using the ocat card layout.
- **Routing:**
  - With a host set, package files are served only on that host.
  - Every other route on the content host is refused, and package routes on the main host are refused.
  - The existing signed path token (user, enrollment, item, file, expiry) authorizes requests; cookies never do.
- **Headers on the content host:** CSP `sandbox allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox`, `frame-ancestors` set to the main site's origin, plus nosniff, no-referrer and no-store.
- **Bridge page:** a wrapper page on the content host defines `window.API` and `API_1484_11`, loads the package's launch file in a same-origin child frame, and forwards each call to the player on the main origin by postMessage, checking `event.origin` strictly on both sides. The server-side data model stays as it is.
- **Without a host:** today's strict mode stays. The package page notes that multi-frame packages (Rise, Storyline 360) need a content host, and the importer warns when it finds `scormdriver`.
- **Allowlist additions:**
  - cursors and icons: `.cur`, `.ico`;
  - office documents: `.doc`/`.docx`, `.xls`/`.xlsx`, `.ppt`/`.pptx`, `.pdf`;
  - captions: `.vtt`, `.srt`;
  - fonts: `.woff`, `.woff2`, `.ttf`, `.otf`, `.eot`;
  - media: `.mp4`, `.webm`, `.mp3`, `.m4a`, `.ogg`, `.wav`;
  - images: `.svg`, `.webp`;
  - data: `.json`, `.xml`, `.xsd`, `.dtd`, `.txt`, `.csv`.

  Each is served with its MIME type. Server-executable types are never allowed.
- **Tests:**
  - unit: host routing, headers and the allowlist;
  - integration: Host-header requests;
  - JS: the bridge's origin checks;
  - a synthetic Rise-shaped package as a test asset. Never commit real packages.
- **Browser check:** content host `http://127.0.0.1:<port>` with the site browsed at `http://localhost:<port>`. Use real Rise, Storyline 360 and old Articulate packages from the scratchpad (Finding a Hobby and Top Golf are Rise; De-escalation is old Articulate).
- **Docs:** production setup, which means DNS, the tenant's `RequestUrlHost`, TLS, and auth cookies that stay host-only (no `Domain=`).

## 2. Account pages (bugs Mike found on 2026-11-19) (done on `ma/lms-package-content-host`, not pushed)

Done in `0345585e4` (the menu links follow the pages' own checks, through `IAccountMemberService.FindMembershipAsync`) and `3b2506642` (every Learning and Accounts page in the menu renders inside `CommerceAccountPage`; My transcript, My required learning, My certifications and My education credits are menu entries; `AccountMenuFrameTests` guards the frame). Found on a fresh site: Learning Commerce enabled together with its dependencies did not run its migration until the site restarted, so Course seats failed with a missing `LicensePoolIndex` table for an account admin. Not fixed here; see section 4.


- **Course seats and Account members:** both links in "Your account" give a permission error. Reproduce as `admin` and as an account member (bplearner2 in Acme Logistics; bpstaff1). Make the link and the page agree: show the link only to people the page lets in, and make sure account admins get in.
- **My credentials:** it renders without the "Your account" menu on the left. Every account page must render through the `CommerceAccountPage` frame (`CommerceConstants.Account.PageShape`; see "Storefront account" in `src/CrestApps.Docs/docs/modules/commerce.md`).
- **Other account pages:** check My learning, My transcript, My certificates, My education credits, My required learning, Notifications, Course seats and Account members.
- **Guard test:** add a test that fails for any page in the account menu that doesn't render inside the frame.

## 2b. Admin UI consistency (Mike, 2026-11-19) (done on `ma/lms-package-content-host`, not pushed)

Done in `2d5f2b94b` (selectpickers; the quiz's fixed questions search the server through `ItemSelector`; lint `learning-multiple-selects-use-selectpicker`), `025bc42c0` (cards on every Learning admin editor; lint `learning-admin-edit-views-use-cards`) and `394a2bcd6` plus `127f498d7` (the Draw at random card). Course pickers (prerequisites, path sets, workflow events) stay selectpickers with every course rendered and a search box, not a server search.


- **Multiple-select menus become bootstrap-select.** Every multiple `<select>` in the Learning modules (and any other module the stack added) uses bootstrap-select (selectpicker), as the rest of the admin does: searchable, with selected-count text. No jQuery; use the selectpicker's native API (append `<option>`s, then `.refresh()`). Long lists, such as the quiz's question picker, use a search endpoint (the reusable `ItemSelector`/`UserPicker` pattern) instead of rendering every option.
- **Cards on every edit page.**
  - Every Learning admin edit page groups its fields in ocat cards, as the other admin screens do.
  - A form with only a few fields still gets one card, so every page looks the same.
  - Buttons stay in the sticky `action-bar-bottom` (see `.agents/skills/crestapps-admin-edit-views/SKILL.md`).
  - Example of a page with no grouping today: Education credit types (`/Admin/learning/credit-types/edit/{id}`).
  - Audit every Learning editor, part editor, settings page and modal-free form, and add a guard test (or view lint) that fails for an admin edit view outside a card.
- **The quiz's "Draw at random" rules.** Today they're a bare table of fixed rows ("No rule" selects, empty tag, difficulty and count boxes). Rebuild them to match the other screens:
  - a card holding a list of rules;
  - an **Add rule** button and a remove button per row;
  - a bank selectpicker, a tag picker (the bank's tags, searchable), a difficulty selector, and a count with the bank's available-question count shown beside it;
  - validation that the count doesn't exceed what the bank can supply;
  - the hint text kept.

  Fixed empty rows go away.

## 3. Moodle migration: features needed to move all 449 production courses

Production has:
- 85 categories and 449 courses (364 visible);
- 795 SCORM packages, 624 files, 1,234 labels, 197 quizzes (1,626 multiple choice, 366 true/false, 52 essay, 20 matching, 11 drag-into-text, 1 gapselect, 1 numerical, 1 description), 111 links, 29 assignments, 15 forums, 17 MooTyper, 5 choice, 3 feedback, 2 glossary, 1 database, 1 IMS package;
- 14 learning-plan templates built on 17 competency frameworks (about 457 competencies);
- no badges, no certificate plugin and no face-to-face activities.

The course shapes:
- single SCORM (158);
- SCORM + quiz (120);
- empty (42);
- blended multi-section (34);
- SCORM + files (26);
- SCORM + facilitator files (16);
- SCORM series (15);
- files only (11);
- quiz only (8);
- assignments (5);
- other (20).

Recreated locally so far (production id to local content item id):
- 453 → `4mtftsk38fa14wcf79v7jdysx4`
- 3 → `4nfm9298pmn9mrdjbhkg9hgj4t`
- 216 → `4fhtzm1c2pnhazyaegq4bsq85r`
- 720 → `47e42p9tyf7325c1qf0k8aka5t`
- 624 → `4sbz70w928gqyx7gadseeqn957`
- 650 → `49shxcnknnevgtv6yw8jr4rwcr`
- 511 → `4se4kjnnaxppss2fbjshfbpvwd`
- 589 → `4fxyxj8edn1jc285q1tzgram5z`
- 640 → `4yng22chtkxthv27d1r2jfm74g`
- 670 → `48fwj87wp27dwxxd0360szzex4`
- 598 → `4s3qpqvr96n2d29b2026fs20nn`
- 30 → `4aq3r9p7y7kj9vwjbkqsjz1ncv`
- 581 → `4m9ydeszkw114tvecg9xpmc153`
- 668 → `49p4exnsb4tc9v0g1g22dg6dc1`
- 377 → `49ett0027hche06ewdnqfv295x`

There is also the learning path "30 Day Learning Plan" (`4m7gmtf72s7by2097cge1s274v`), and a "Course categories" taxonomy added through the admin.

Build, in this order. Items 1-6 cover almost every course; the rest is the long tail.

1. ~~**SCORM compatibility.**~~ Done: section 1 above (`ma/lms-package-content-host`); affects 371 courses.
2. **A Moodle importer.** A ContentTransfer format, like the QTI import, that reads a Moodle course backup (`.mbz`: `moodle_backup.xml`, `course/`, `sections/`, `activities/`, `files/`) and creates:
   - the course in its category;
   - the sections as outline groups;
   - the SCORM packages;
   - quizzes with their questions, through the QTI/question mapping;
   - files, links and pages;
   - completion rules;
   - enrolment methods.

   It must report unsupported items per course. A web-services path (`core_course_get_contents`) is an alternative when backups aren't available.
3. **Course categories (449 courses).** A category tree on courses (Taxonomies), with category browsing and filters in the catalog and the admin lists.
4. **Course start and end dates (449).** A course opens or closes on dates, in addition to the access length after enrolment.
5. **Cohorts (300 courses; the learning plans are assigned to cohorts).** Named, hand-managed groups of people in Users, beside the rule-based audiences. Enrol in a course or a learning path by cohort, with cohort sync: joining adds the person, leaving removes them.
6. **Inline text and media in a course (1,234 labels in 195 courses).** A "Text" course item or outline block that keeps HTML, images and video embeds, and isn't tracked for completion.
7. ~~**Quizzes (152 quiz courses):**~~ Done on `ma/lms-quiz-gaps-9bb028`: questions per page, immediate feedback (check and lock), review options per moment (the review policy stays the default), the author's question order (an Order list in the editor), survey, drag into text, select missing words (gapselect) and description question types with QTI (gap match, inline choice, no interaction) and spreadsheet import. The question picker was done in section 2b.
   - several questions per page;
   - immediate feedback per question;
   - Moodle's review options, per moment;
   - the question order the author set, not alphabetical;
   - a usable question picker (search by bank and tag);
   - survey-style questions where every answer counts;
   - drag-into-text with distractor choices;
   - gapselect (drop-down blanks);
   - description (an ungraded text block).
8. **Completion:**
   - manual completion by a named role (62 courses);
   - "no criteria" courses that never complete (139);
   - SCORM "viewed + graded + score at least N" (251 of 337 activities read).
9. **Access:**
   - restrictions to named people or profile fields (25 courses);
   - hidden items or courses that don't block a learning path from completing (the 30 Day plan holds 4 hidden courses).
10. **SCORM settings:** new window (18), attempt limits (9), grading method (10).
11. **Media:**
    - files over 30 MB, for videos;
    - `.ods` and other office formats;
    - showing the original file name instead of the GUID-prefixed stored name.
12. **Assignments:**
    - grade-only sign-off with no submission;
    - completes on hand-in and is graded;
    - up to 20 files.
13. **Small activity types (1-3 courses each):**
    - feedback survey (an ungraded essay quiz is a rough stand-in);
    - forum;
    - glossary;
    - choice;
    - MooTyper typing practice;
    - IMS content package;
    - database.

    For each, decide to build it, replace it, or drop it at migration.
14. **Competencies.** 17 frameworks with about 457 competencies, mapped onto Skills (skills with proficiency levels). Learning-plan templates become learning paths or programs whose completion grants the competency (a skill assignment).
15. **Course images, and images inside summaries and labels.** Import them into the media library.

## 4. Smaller follow-ups

- **Learning Commerce migration on a fresh site.** Enabling Learning Commerce in one bulk enable with its dependencies (Orders, Commerce, My Account) logged "Updating feature" but left `LearningCommerceMigrations` and `LearningPathProductMigrations` unrun until a restart, so the seats tables were missing. Reproduce with a new site from the Online Store recipe and one POST to `/Admin/Features` enabling the Learning features.
- **Account menu order.** The storefront account menu does not follow the items' positions (Payment methods and Account members come before Overview); it predates this work.

- **Notifications:**
  - learners without the admin can't change their notification preferences (the Notifications module's editor is only on the admin profile);
  - no notice to approvers when someone requests enrollment;
  - no notice when a class session is moved.
- **Review catch-up:** clearing an enrollment review doesn't re-run the course or path credential and credit observers. Recorded in plan section 19, round 10 item 3.
- **AI:** index-side course filtering needs an extension point in CrestApps.Core's `DataSourceRetrieval`; the post-search filter stays meanwhile.
- **Reports:** the Reports building block's `ReportIndexPages` hits the SQLite Double/Int64 read error on the attempts, answers and submissions data sets. Another session works on that paging code.
- **Duplicates with main,** reported at the merge and not refactored:
  - protected files vs Attachments;
  - enrollment approvals vs Approvals;
  - certificate numbers vs `INumberSeries`;
  - `TimeOffLedgerCopy` vs `IResumableStep`;
  - the invitation e-mail vs `ITemplatedEmailSender`;
  - audience rules vs `ConditionRule`;
  - certificate templates vs Business Documents.
- **Plan section 19:** many commit hashes are stale after the rebases and merges.
