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

## Chat session upload limits

- An upload that starts a chat session is authorized before the session is created, and counts against the chat session-start limit.
- A chat session holds at most 20 documents; change it with `CrestApps_AI:ChatSessionDocuments:MaxFilesPerSession`. The value is at least 1: a value below 1 is held at 1, and never removes the cap.
- A chat embedded in a frame on another site offers no session uploads, because the browser does not store the antiforgery cookie the upload routes need there.
- Every upload, to a new or an existing session, counts against the `ai-chat-document-uploads` attempt policy (60 an hour per signed-in user, or per network for an anonymous visitor); change it under `CrestApps_Throttling:AttemptPolicies:ai-chat-document-uploads`.
- With Orchard Core's Rate Limits feature on, the session upload and remove routes carry the `ai-chat-documents` rate-limit group (30 requests per 10 minutes per visitor by default).

## Documentation

See [AI Documents](../../CrestApps.Docs/docs/ai/documents/index.md) in the technical manual.
