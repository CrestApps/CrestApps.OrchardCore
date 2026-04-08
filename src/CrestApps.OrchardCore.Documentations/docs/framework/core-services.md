---
sidebar_label: Core Services
sidebar_position: 2
title: Core Services
description: Foundation services including OData validation, shared utilities, and base types used by all CrestApps AI modules.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/core-services)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# Core Services

> Foundation services that other CrestApps features depend on.

## Quick Start

```csharp
builder.Services.AddCrestAppsCoreServices();
```

:::info
You rarely need to call this directly — `AddCrestAppsAI()` chains it automatically.
:::

## Problem & Solution

Every feature in the framework shares common concerns like OData filter validation and base service patterns. `AddCrestAppsCoreServices()` registers these shared services so that higher-level features can depend on them without duplication.

## Services Registered

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `IODataValidator` | `ODataFilterValidator` | Scoped | Validates OData `$filter` expressions |

## OData Filter Examples

OData filters are used by data source backends to narrow search results. The `IODataValidator` service validates filter expressions before they are translated and sent to the backend.

### Valid Filter Expressions

```text
category eq 'support'
status ne 'archived'
priority gt 3
createdDate ge 2024-01-01T00:00:00Z
category eq 'support' and status eq 'active'
category eq 'support' or category eq 'billing'
contains(title, 'refund')
startswith(name, 'John')
not endswith(email, '@example.com')
(priority gt 3 and status eq 'open') or category eq 'urgent'
```

### Invalid Filter Expressions

```text
category =  'support'         # Wrong operator (use 'eq', not '=')
category eq support            # Missing quotes around string value
and category eq 'support'      # Leading logical operator
category eq 'support' and      # Trailing logical operator
category eq                    # Missing value
```

### Using the Validator

```csharp
public sealed class MyDataSourceService
{
    private readonly IODataValidator _validator;

    public MyDataSourceService(IODataValidator validator)
    {
        _validator = validator;
    }

    public void ApplyFilter(string userFilter)
    {
        if (!string.IsNullOrEmpty(userFilter) && !_validator.IsValidFilter(userFilter))
        {
            throw new ArgumentException($"Invalid OData filter expression: {userFilter}");
        }

        // Filter is safe to pass to the backend
    }
}
```

## When You Need This

The OData filter validator is primarily used by the data source system. When a data source is configured with filter criteria (e.g., "only return documents where `category eq 'support'`"), the filter expression is:

1. **Validated** by `IODataValidator` to ensure it is syntactically correct
2. **Translated** by `IODataFilterTranslator` into the backend-native query language (e.g., Elasticsearch Query DSL or Azure AI Search OData)
3. **Applied** during vector search to narrow results

This prevents malformed filters from causing backend errors or unexpected behavior.

```text
User-provided OData filter
        │
        ▼
IODataValidator.IsValidFilter()  →  false? → reject with error
        │ true
        ▼
IODataFilterTranslator.Translate()  →  backend-native filter
        │
        ▼
IDataSourceContentManager.SearchAsync(..., filter)
```

## Implementing a Custom Validator

If you need stricter validation rules (e.g., allow only specific field names or operators), implement `IODataValidator`:

```csharp
public sealed class StrictODataValidator : IODataValidator
{
    private static readonly HashSet<string> _allowedFields =
    [
        "category",
        "status",
        "priority",
        "createdDate",
    ];

    public bool IsValidFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return false;
        }

        // First, check basic OData syntax (delegate to default validator logic)
        if (!IsValidODataSyntax(filter))
        {
            return false;
        }

        // Then, check that only allowed fields are referenced
        foreach (var field in _allowedFields)
        {
            // Custom field validation logic
        }

        return true;
    }

    private static bool IsValidODataSyntax(string filter)
    {
        // Basic OData expression validation
        // Check for balanced parentheses, valid operators, etc.
        return !string.IsNullOrWhiteSpace(filter);
    }
}
```

Register it to replace the default:

```csharp
services.AddScoped<IODataValidator, StrictODataValidator>();
```

## Interfaces

### `IODataValidator`

Validates OData filter strings before they are passed to data source backends (Elasticsearch, Azure AI Search).

```csharp
public interface IODataValidator
{
    bool TryValidate(string filter, out IReadOnlyList<string> errors);
}
```

## Orchard Core Integration

In Orchard Core, core services are registered automatically when any CrestApps AI module is enabled. No manual setup is required.
