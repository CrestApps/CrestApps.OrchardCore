# CrestApps Resources

Extends the Resources module with additional reusable scripts and stylesheets.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| CrestApps Resources | `CrestApps.OrchardCore.Resources` | Extends the Resources module with additional reusable scripts and stylesheets. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Public form protection

Any form anyone may post (a ticket form, a newsletter sign-up) renders two shapes by name and checks the post on the server:

- `PublicFormTrap`: a hidden, off-screen input named `PublicFormTrap.FieldName` (`website`) that people never fill. A post with a value in it is dropped (`PublicFormTrap.IsTrapped`).
- `PublicFormChallenge`: the shape of each configured `IPublicFormChallenge` that implements `IPublicFormChallengeShape`, nothing when none is set up. With Orchard Core's **ReCaptcha** feature on and its keys set, this feature registers the ReCaptcha challenge, so the shape shows the ReCaptcha widget; the form calls `VerifyAsync` on every configured challenge.

Limit how often a visitor may post with an Orchard Core Rate Limits group on the action (`RateLimitGroupAttribute` and `RateLimitsOptions.AddGroupRateLimit`).

## Documentation

See [Resources](../../CrestApps.Docs/docs/modules/resources.md) in the technical manual.
