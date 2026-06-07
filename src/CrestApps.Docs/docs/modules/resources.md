---
sidebar_label: Resources
sidebar_position: 5
title: Resources
description: Extends the Resources module with additional reusable scripts and stylesheets.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Resources |
| **Feature ID** | `CrestApps.OrchardCore.Resources` |

Provides shared resources and libraries used by various CrestApps modules.

## Overview

This module provides shared frontend resources (CSS and JavaScript) that are used by other CrestApps modules. It acts as a central resource library, ensuring consistent styling and behavior across the CrestApps module ecosystem.

Other CrestApps modules declare a dependency on this feature to leverage common scripts and stylesheets without duplicating assets.

## Shared libraries

This feature registers reusable Orchard resource-manager assets that can be consumed by other CrestApps modules.

Current shared libraries include:

- `intl-tel-input` script and stylesheet resources, backed by local copied assets with CDN fallbacks
