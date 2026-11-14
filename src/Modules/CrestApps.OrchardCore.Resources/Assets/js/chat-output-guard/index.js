// chat-output-guard: required ahead of the AIChatApp and ChatInteractionApp scripts, after DOMPurify. It keeps a model's
// answer from carrying a chat configuration that the chat scripts would start, and so from running a template of its
// own; see guard.js.
import { installChatOutputGuard } from './guard.js';

installChatOutputGuard(window);
