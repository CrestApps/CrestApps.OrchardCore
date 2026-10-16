namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Client templates the AI Profile Builder gives the chat interaction client.
/// </summary>
internal static class ProfileBuilderClientTemplates
{
    /// <summary>
    /// The chat client's own message template with a feedback toolbar under every finished assistant reply. The
    /// toolbar sits in its own row rather than in the chat's message buttons, which are shown only on hover and are
    /// too narrow for labelled buttons; feedback is the point of the page, so it is always visible.
    /// </summary>
    /// <remarks>
    /// A Vue template can only call the chat component's own methods, so the toolbar buttons carry data attributes
    /// that the builder script handles with one delegated click listener. <c>builderBadge</c> and
    /// <c>builderBadgeClass</c> are set on a message by the builder script once feedback is recorded for it.
    /// Everything outside the marked toolbar is kept in step with the chat client's default template.
    /// </remarks>
    public const string MessageTemplate = """
        <div class="ai-chat-messages">
            <div v-for="(message, index) in messages" :key="index" class="ai-chat-message-item" :data-builder-message-index="index">
                <div>
                    <div v-if="message.role === 'user'" class="ai-chat-msg-role ai-chat-msg-role-user">Customer</div>
                    <div v-else-if="message.role !== 'indicator'" :class="getAssistantRoleClasses(message)">
                        <span :class="getAssistantIconClasses(message, index)"><i :class="getAssistantIcon(message)"></i></span>
                        {{ getAssistantLabel(message) }}
                        <span v-if="message.builderBadge" class="badge ms-2 profile-builder-badge" :class="message.builderBadgeClass || 'text-bg-secondary'">{{ message.builderBadge }}</span>
                    </div>
                    <div class="ai-chat-message-body lh-base">
                        <h4 v-if="message.title">{{ message.title }}</h4>
                        <div v-html="message.htmlContent"></div>
                        <ol v-if="message.citationReferences && message.citationReferences.length" class="ai-chat-citation-list">
                            <li v-for="citation in message.citationReferences" :key="'citation-' + (citation.referenceKey || citation.displayIndex)" class="ai-chat-citation-item">
                                <a v-if="citation.link" :href="citation.link" :target="citation.isDownload ? null : '_blank'" :rel="citation.isDownload ? null : 'noopener noreferrer'">{{ citation.label }}</a>
                                <span v-else>{{ citation.label }}</span>
                            </li>
                        </ol>
                        <span class="message-buttons-container" v-if="!isIndicator(message)">
                            <button v-if="textToSpeechEnabled && !isConversationMode && message.role === 'assistant' && !message.isStreaming" class="btn btn-sm btn-link text-secondary p-0 me-1 button-message-toolbox" :class="{ 'tts-playing': ttsPlayingMessageIndex === index }" :data-tts-message-index="index" @click="toggleMessageTts(message, index)" :title="ttsPlayingMessageIndex === index ? 'Pause audio' : 'Read aloud'">
                                <i :class="ttsPlayingMessageIndex === index ? 'fa-solid fa-circle-pause' : 'fa-solid fa-circle-play'"></i>
                            </button>
                            <button type="button" class="btn btn-sm btn-link p-0 button-message-toolbox ai-response-copy-btn" :class="copiedMessageIndex === index ? 'text-success' : 'text-secondary'" :data-copy-message-index="index" @click="copyResponse(message, index, $event)" :title="copiedMessageIndex === index ? copiedTitle : copyTitle">
                                <i :class="copiedMessageIndex === index ? 'fa-solid fa-check' : 'fa-solid fa-copy'"></i>
                            </button>
                        </span>
                        <!-- Profile Builder toolbar -->
                        <div v-if="message.role === 'assistant' && !message.isStreaming && message.content" class="profile-builder-toolbar d-flex flex-wrap gap-3 mt-1">
                            <button type="button" class="btn btn-sm btn-link p-0 text-decoration-none profile-builder-action" data-builder-action="feedback" :data-builder-message-index="index" title="Give feedback on this reply">
                                <i class="fa-regular fa-comment-dots me-1"></i>Feedback
                            </button>
                            <button type="button" class="btn btn-sm btn-link p-0 text-decoration-none text-success profile-builder-action" data-builder-action="approve" :data-builder-message-index="index" title="This reply is what you want">
                                <i class="fa-regular fa-thumbs-up me-1"></i>Looks good
                            </button>
                        </div>
                        <!-- End Profile Builder toolbar -->
                    </div>
                </div>
            </div>
            <div v-for="notification in notifications" :key="'notif-' + notification.type" class="ai-chat-notification" :class="'ai-chat-notification-' + (notification.type || 'info') + ' ' + (notification.cssClass || '')">
                <div class="ai-chat-notification-content">
                    <i v-if="notification.icon" :class="notification.icon" class="ai-chat-notification-icon"></i>
                    <span class="ai-chat-notification-text">{{ notification.content }}</span>
                    <button v-if="notification.dismissible" class="btn btn-sm btn-link p-0 ms-2 ai-chat-notification-dismiss" @click="dismissNotification(notification.type)" title="Dismiss">
                        <i class="fa-solid fa-xmark"></i>
                    </button>
                </div>
                <div v-if="notification.actions && notification.actions.length" class="ai-chat-notification-actions">
                    <button v-for="action in notification.actions" :key="action.name" class="btn btn-sm" :class="action.cssClass || 'btn-outline-secondary'" @click="handleNotificationAction(notification.type, action.name)">
                        <i v-if="action.icon" :class="action.icon" class="me-1"></i>
                        {{ action.label }}
                    </button>
                </div>
            </div>
        </div>
        """;
}
