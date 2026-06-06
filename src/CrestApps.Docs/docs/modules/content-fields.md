---
sidebar_label: Content Fields
sidebar_position: 2
title: Content Fields
description: Adds custom Orchard Core content field editors maintained by CrestApps.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Content Fields |
| **Feature ID** | `CrestApps.OrchardCore.ContentFields` |

Provides custom Orchard Core content field editors maintained by CrestApps.

## Overview

This module adds reusable editor variants for Orchard Core content fields without changing the underlying field types.

It follows Orchard Core's custom editor convention by shipping both:

- `TextField-InternationalTelephone.Option.cshtml`
- `TextField-InternationalTelephone.Edit.cshtml`

## Included editors

### InternationalTelephone (`TextField`)

The `InternationalTelephone` editor uses the `intl-tel-input` library to provide:

- country-aware phone number entry
- international formatting while editing
- normalization back to E.164 on submit
- local copied assets with CDN fallbacks through Orchard resource manifests

Use it from the content definition UI or through migrations with:

```csharp
.WithEditor("InternationalTelephone")
```

## Notes

- The underlying field type stays `TextField`.
- Existing values stored in E.164 format continue to edit correctly.
- The Omnichannel Management module now depends on this feature for `PhoneNumberInfoPart.Number`.
