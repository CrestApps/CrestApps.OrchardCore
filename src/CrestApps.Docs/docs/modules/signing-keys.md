---
sidebar_label: Signing keys
title: Signing keys
description: "The shared signing keys: key sets registered with AddSigningKeySet, kept in Orchard Core Secrets, published as a JSON Web Key Set through ISigningKeyRing, and rotated by one daily task."
---

The shared signing keys give a module a key pair that signs what it sends out (a token, a credential, a webhook) and the public keys a verifier reads, published as a JSON Web Key Set (JWKS) and rotated on a schedule. It is two libraries, not a feature:

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.SigningKeys.Abstractions` | `ISigningKeyRing`, `SigningKey`, `SigningKeyAlgorithms`, `SigningKeySetOptions`, `SigningKeyOptions`, `ISigningKeyImportSource`, `SigningKeyImport` and `AddSigningKeySet`. |
| `CrestApps.OrchardCore.SigningKeys.Core` | The key ring, the daily rotation task, the tenant event that makes each set's first key, and `AddSigningKeys()`. |

Three modules use it: [Agentic Commerce](./agentic-commerce)'s merchant keys (ES256), [Learning External Tools](./learning-lti)' LTI platform key (RS256) and [Learning Digital Credentials](./learning-credentials)' signing key (RS256). Never keep key pairs in a document of your own, protect them with Data Protection, or write another rotation task or JWKS publisher.

## Registering a key set

A key set is a named group of keys with one algorithm. Register it in the feature's startup, and call `AddSigningKeys()` once:

```csharp
services.AddSigningKeys();
services.AddSigningKeySet("MyModule.Tokens", options =>
{
    options.Algorithm = SigningKeyAlgorithms.RS256;
    options.RotationAge = TimeSpan.FromDays(90);
    options.RetiredLifetime = TimeSpan.FromDays(7);
    options.KeyIdPrefix = "tokens";
    options.Description = "My module: the key that signs its tokens. Made and rotated by the site; do not edit.";
});
```

The feature must depend on Orchard Core Secrets (`OrchardCore.Secrets`), which keeps the key pairs.

| Option | Meaning |
| --- | --- |
| `Algorithm` | `RS256` (a 2048-bit RSA key, kept as an Orchard Core `RsaKeySecret`; the default) or `ES256` (a P-256 key, kept as a `TextSecret` holding the private JSON Web Key). |
| `RotationAge` | How long a key signs before a new one replaces it. 90 days by default. |
| `RetiredLifetime` | How long a key that stopped signing stays published, so what it signed still verifies. 7 days by default. Its secret is removed after four times as long. |
| `KeyIdPrefix` | The start of the key ids (`kid`); the creation date and a random part follow, such as `tokens-20261120-1a2b3c4d`. |
| `Description` | The description given to the key secrets in Orchard Core Secrets. |

`AddSigningKeys()` registers the ring, its daily rotation task and the tenant event, once however many features call it.

## The key ring

`ISigningKeyRing` serves every registered set by name:

| Method | What it does |
| --- | --- |
| `GetSigningKeyAsync(keySet)` | The key that signs now, with its private half (`SigningKey`: `Kid`, `Algorithm`, `PrivateJwk`, `PublicJwk`, `CreatedUtc`). One is made when the set has none. |
| `GetPublishedKeysAsync(keySet)` | The public keys to publish: the one that signs now and those retired within the set's retired lifetime. Serve them from the module's own JWKS endpoint. |
| `RotateAsync(keySet)` | Makes a new key that signs from now on; the old one stays published for the retired lifetime. |
| `RotateIfDueAsync(keySet)` | Rotates the key when it reached the rotation age, or makes one when the set has none. |
| `ImportAsync(keySet, keys)` | Imports keys a module kept before it used the ring, with their ids, public halves and dates. |
| `HasKeysAsync(keySet)` | Whether the set has a key yet, without making one. |

The key pairs are secrets named `SigningKeys.{key set}.{kid}`. A document of the ring keeps only each set's key ids, public halves, secret names and dates. A key is made, rotated or imported under a distributed lock per set, so two requests never make two keys at once.

Sign with the key's `Kid` in the signature's `kid` header, so a verifier finds the public key in the published set.

## Rotation and the first key

- When the tenant starts, a tenant event makes (or imports) the first key of every registered set, in a scope of its own after the activation, so the first request that needs a key does not create a secret mid-transaction.
- Once a day, `SigningKeyRotationBackgroundTask` rotates the key of every set that reached its rotation age.
- Each new key also removes the secrets of the set's keys retired more than four times the retired lifetime ago: they verify nothing any more.

## Bringing older keys along

A module that kept its own keys before it moved onto the ring implements `ISigningKeyImportSource`: its `KeySet` and `GetKeysAsync()`, returning each `SigningKeyImport` (`Kid`, `PrivateJwk`, `PublicJwk` exactly as published, `CreatedUtc`, `RetiredUtc`). Before the ring makes the first key of that set, it imports them with their ids, so everything signed before the move still verifies.
