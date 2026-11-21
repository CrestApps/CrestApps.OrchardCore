# CrestApps.OrchardCore.Learning.Xapi

The Learning Record Store of the CrestApps Learning module. The **Learning Record Store** feature implements xAPI 1.0.3 (it accepts the version headers 1.0 and 1.0.0 to 1.0.3, and refuses 2.0.0).

- **Resources**: statements (PUT, POST, GET by id, voided id or query, with the `more` link), state, activity profile, agent profile (ETags, `If-Match`, `If-None-Match`, JSON merge on POST), activities, agents and about, at `/learning/xapi/`, with the alternate request syntax.
- **Validation**: every statement is checked against xAPI 1.0.3; statements are immutable, voiding hides the voided statement from queries, `stored` and `authority` are set by the LRS, attachments are matched by SHA-2 and signed statements are checked.
- **Credentials**: an Orchard Core OpenID application whose roles hold the LRS permissions, with a bearer token of the `Api` scheme or HTTP Basic (client id and secret), and launch tokens made by a module that launches content (cmi5). The sign-in cookie never authenticates the LRS, so it answers cross-origin requests and preflights from any origin, without credentials.
- **Admin**: a list of statements under **Learning**, with filters, and a statement page.

See the [Learning Record Store documentation](https://orchardcore.crestapps.com/docs/modules/learning-xapi).
