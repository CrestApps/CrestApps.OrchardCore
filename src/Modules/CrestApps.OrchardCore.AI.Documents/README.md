# AI Documents

Provides the foundation for document processing, text extraction, and Retrieval-Augmented Generation (RAG) capabilities.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| AI Documents | `CrestApps.OrchardCore.AI.Documents` | Provides the foundation for document processing, text extraction, and Retrieval-Augmented Generation (RAG) capabilities. Enabled by the features that depend on it. |
| AI Documents for Chat Interactions | `CrestApps.OrchardCore.AI.Documents.ChatInteractions` | Provides document upload and Retrieval-Augmented Generation (RAG) support for AI Chat Interactions. |
| AI Documents for Profiles | `CrestApps.OrchardCore.AI.Documents.Profiles` | Provides document upload and Retrieval-Augmented Generation (RAG) support for AI Profiles. |
| AI Documents for Chat Sessions | `CrestApps.OrchardCore.AI.Documents.ChatSessions` | Provides document upload and Retrieval-Augmented Generation (RAG) support for AI Chat Sessions and Widgets. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.AI.Chat.Interactions`](../CrestApps.OrchardCore.AI.Chat.Interactions/README.md)
- `OrchardCore.Indexing`
- `CrestApps.OrchardCore.AI.Chat.Core`
- [`CrestApps.OrchardCore.AI.Chat`](../CrestApps.OrchardCore.AI.Chat/README.md)

## Documentation

See [AI Documents](../../CrestApps.Docs/docs/ai/documents/index.md) in the technical manual.
