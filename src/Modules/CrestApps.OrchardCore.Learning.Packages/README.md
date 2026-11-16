# CrestApps.OrchardCore.Learning.Packages

Learning packages for the CrestApps Learning module. The **Learning SCORM Packages** feature implements SCORM 1.2 (ADL SCORM 1.2, the Run-Time Environment and Content Aggregation Model) and SCORM 2004 3rd and 4th Edition (Content Aggregation Model and Run-Time Environment; Sequencing and Navigation not implemented: a package runs its first SCO, without sequencing rules, rollup or navigation requests).

- **Learning package course item**: an author with Import learning packages uploads a .zip; the archive is extracted with safe paths and limits, `imsmanifest.xml` is read, and a clean copy is kept in the protected file store.
- **Sandboxed runtime**: the package runs in a frame on an opaque origin (`Content-Security-Policy: sandbox allow-scripts allow-forms allow-popups`, no `allow-same-origin`), opened by a signed, expiring launch token in the path; it cannot read the learner's cookies, the player page or the site's endpoints.
- **SCORM run-time APIs**: `window.API` (SCORM 1.2) or `window.API_1484_11` (SCORM 2004) in every package page answers the SCO and tells the player page through `postMessage` only; the player sends the values to the server with the learner's session.
- **Server runtime state**: every value is checked again against the SCORM 1.2 or SCORM 2004 data model on the server and kept per enrollment and package.
- **Progress**: the result counts in the learner's progress through Learning, marked self-reported.

See the [Learning Packages documentation](https://orchardcore.crestapps.com/docs/modules/learning-packages).
