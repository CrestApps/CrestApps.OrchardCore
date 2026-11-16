# CrestApps.OrchardCore.Learning.Lti

LTI 1.3 tools in the courses of the CrestApps Learning module, with the site as the platform. The **External Tools** feature implements LTI 1.3 Core, Deep Linking 2.0, Names and Role Provisioning Services 2.0 and Assignment and Grade Services 2.0. It is not certified.

- **Tools**: registered under **Learning > External Tools** (an OpenID application plus the LTI settings, the tool's key set and a privacy level), with the platform details shown for copying.
- **Launch**: the External tool course item starts the third-party initiated login; the platform's authorization endpoint answers with an id token signed with the platform key, a key set of the shared signing keys in Orchard Core Secrets, rotated with the old key still published.
- **Deep Linking**: course staff pick content in a tool; each `ltiResourceLink` becomes an External tool item.
- **Services**: tools get a token from the OpenID server's token endpoint with a `private_key_jwt` client assertion, read the roster and post scores; a fully graded score reaches the learner's progress.
- **Data**: the `LearningExternalTools` recipe and deployment steps (settings), personal data export and erasure, and the **External Score Received** workflow event.

See the [External Tools documentation](https://orchardcore.crestapps.com/docs/modules/learning-lti).
