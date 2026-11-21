# Learning Digital Credentials

Feature **Digital Credentials** (`CrestApps.OrchardCore.Learning.Credentials`). It implements Open Badges 3.0 (issuer) on the W3C Verifiable Credentials Data Model 2.0. It is not certified or tested by 1EdTech.

- **Credential definition**: a content type (title, image, description, criteria, achievement type, tags, alignments, an optional Skills skill or certification alignment, validity in months) with award rules: a course completed, a quiz passed (Assessments), a learning path completed (Learning Paths), a certification issued (Learning Certifications), and awards by hand.
- **Credential award**: a catalog document per award, with the achievement copied into it, a salted SHA-256 hash of the recipient's e-mail address, the dates, the revocation and the signed credential as issued. Awarding is idempotent per definition, rule source and source id, under a lock.
- **Signed credential**: an `OpenBadgeCredential` signed as a JSON Web Token (RS256) with the credentials key set of the shared signing keys (`CrestApps.OrchardCore.SigningKeys`), whose private keys are Orchard Core Secrets.

Depends on Learning and Orchard Core Secrets. See the documentation page `modules/learning-credentials` for the endpoints, the revocation list, permissions, recipes, personal data and what is not built.
