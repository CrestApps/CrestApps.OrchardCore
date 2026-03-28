window.chatInteractionManager = function () {

    // Defaults (can be overridden by instanceConfig)
    var defaultConfig = {
        // UI defaults for generated media
        generatedImageAltText: 'Generated Image',
        generatedImageMaxWidth: 400,
        generatedChartMaxWidth: 900,
        downloadImageTitle: 'Download image',
        downloadChartTitle: 'Download chart as image',
        downloadChartButtonText: 'Download',
        codeCopiedText: 'Copied!',
        assistantLabel: 'Assistant',

        messageTemplate: `
            <div class="ai-chat-messages">
                <div v-for="(message, index) in messages" :key="index" class="ai-chat-message-item">
                    <div>
                        <div v-if="message.role === 'user'" class="ai-chat-msg-role ai-chat-msg-role-user">You</div>
                        <div v-else-if="message.role !== 'indicator'" :class="getAssistantRoleClasses(message)">
                            <span :class="getAssistantIconClasses(message, index)"><i :class="getAssistantIcon(message)"></i></span>
                            {{ getAssistantLabel(message) }}
                        </div>
                        <div class="lh-base">
                            <h4 v-if="message.title">{{ message.title }}</h4>
                            <div v-html="message.htmlContent"></div>
                            <span class="message-buttons-container" v-if="!isIndicator(message)">
                                <button class="btn btn-sm btn-link text-secondary p-0 button-message-toolbox" @click="copyResponse(message.content)" title="Click here to copy response to clipboard.">
                                    <i class="fa-solid fa-copy"></i>
                                </button>
                            </span>
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
        `,
        indicatorTemplate: `
            <div class="ai-chat-msg-role ai-chat-msg-role-assistant">
                <span class="ai-streaming-icon"><i class="fa fa-robot" style="display: inline-block;"></i></span>
                Assistant
            </div>
        `,
        // Localizable strings
        untitledText: 'Untitled',
        clearHistoryTitle: 'Clear History',
        clearHistoryMessage: 'Are you sure you want to clear the chat history? This action cannot be undone. Your documents, parameters, and tools will be preserved.',
        clearHistoryOkText: 'Yes',
        clearHistoryCancelText: 'Cancel'
    };

    // Sanitize URLs to prevent javascript: protocol injection.
    function sanitizeUrl(url) {
        if (!url) return '';
        var trimmed = url.trim();
        if (/^javascript:/i.test(trimmed) || /^vbscript:/i.test(trimmed) || /^data:text\/html/i.test(trimmed)) {
            return '';
        }
        return url;
    }

    // Safely HTML-encode a string using the DOM (avoids regex-based HTML filtering).
    function escapeHtmlEntities(text) {
        var span = document.createElement('span');
        span.textContent = text;
        return span.innerHTML;
    }

    const renderer = new marked.Renderer();

    // Modify the link rendering to open in a new tab
    renderer.link = function (data) {
        var href = sanitizeUrl(data.href);
        if (!href) return data.text || '';
        return `<a href="${href}" target="_blank" rel="noopener noreferrer">${data.text}</a>`;
    };

    // Custom code block renderer with highlight.js integration and copy button.
    renderer.code = function (data) {
        var code = data.text || '';
        var lang = (data.lang || '').trim();
        var highlighted = code;

        if (typeof hljs !== 'undefined') {
            if (lang && hljs.getLanguage(lang)) {
                try {
                    highlighted = hljs.highlight(code, { language: lang }).value;
                } catch (_) { }
            } else {
                try {
                    highlighted = hljs.highlightAuto(code).value;
                } catch (_) { }
            }
        } else {
            highlighted = escapeHtmlEntities(code);
        }

        var langDisplay = lang ? escapeHtmlEntities(lang) : 'code';
        return `<div class="ai-code-block"><div class="ai-code-header"><span class="ai-code-lang"><i class="fa-solid fa-code"></i> ${langDisplay}</span><button type="button" class="ai-code-copy-btn" title="Copy code"><i class="fa-regular fa-copy"></i></button></div><pre><code class="hljs${lang ? ' language-' + lang : ''}">${highlighted}</code></pre></div>`;
    };

    // Custom image renderer for generated images with thumbnail styling and download button.
    // Handles both URL and data-URI sources (data URIs are converted to blobs for download).
    renderer.image = function (data) {
        const src = sanitizeUrl(data.href);
        if (!src) return '';
        const alt = data.text || defaultConfig.generatedImageAltText;
        const maxWidth = defaultConfig.generatedImageMaxWidth;
        return `<div class="generated-image-container">
            <img src="${src}" alt="${alt}" class="img-thumbnail" style="max-width: ${maxWidth}px; height: auto;" />
            <div class="mt-2">
                <a href="${src}" target="_blank" download="${alt}" title="${defaultConfig.downloadImageTitle}" class="btn btn-sm btn-outline-secondary ai-download-image">
                    <i class="fa-solid fa-download"></i>
                </a>
            </div>
        </div>`;
    };

    // Chart counter for unique IDs
    let chartCounter = 0;

    // Collector for charts discovered during marked parsing.
    let _pendingCharts = [];

    function createChartHtml(chartId) {
        const chartMaxWidth = defaultConfig.generatedChartMaxWidth;

        return `<div class="chart-container" style="position: relative; width: 100%; max-width: ${chartMaxWidth}px; margin: 0 auto; height: 480px;">`
            + `<canvas id="${chartId}" class="img-thumbnail" width="${chartMaxWidth}" height="480" style="width: 100%; height: 480px;"></canvas>`
            + `</div>`
            + `<div class="mt-2">`
            + `<button type="button" class="btn btn-sm btn-outline-secondary" onclick="downloadChart('${chartId}')" title="${defaultConfig.downloadChartTitle}">`
            + `<i class="fa-solid fa-download"></i> ${defaultConfig.downloadChartButtonText}`
            + `</button>`
            + `</div>`;
    }

    // Register [chart:{...json...}] as a native marked block extension so the
    // markdown parser handles chart markers inline with surrounding text.
    marked.use({
        extensions: [{
            name: 'chart',
            level: 'block',
            start(src) {
                const idx = src.indexOf('[chart:');
                return idx >= 0 ? idx : undefined;
            },
            tokenizer(src) {
                const extracted = tryExtractChartMarker(src);
                if (!extracted || extracted.startIndex !== 0) {
                    return undefined;
                }

                const chartId = `chat_chart_${++chartCounter}`;

                return {
                    type: 'chart',
                    raw: src.substring(0, extracted.endIndex),
                    chartId: chartId,
                    json: extracted.json,
                };
            },
            renderer(token) {
                _pendingCharts.push({ chartId: token.chartId, config: token.json });
                return createChartHtml(token.chartId);
            }
        }]
    });

    // Extract a [chart:{...json...}] marker. This avoids regex issues with nested brackets.
    function tryExtractChartMarker(text) {
        const token = '[chart:';
        const start = text.indexOf(token);
        if (start < 0) {
            return null;
        }

        // Find JSON object boundary by balancing braces
        const jsonStart = start + token.length;
        let i = jsonStart;
        while (i < text.length && (text[i] === ' ' || text[i] === '\n' || text[i] === '\r' || text[i] === '\t')) {
            i++;
        }

        if (i >= text.length || text[i] !== '{') {
            return null;
        }

        let depth = 0;
        let inString = false;
        let escape = false;

        for (; i < text.length; i++) {
            const ch = text[i];

            if (inString) {
                if (escape) {
                    escape = false;
                    continue;
                }
                if (ch === '\\') {
                    escape = true;
                    continue;
                }
                if (ch === '"') {
                    inString = false;
                }
                continue;
            }

            if (ch === '"') {
                inString = true;
                continue;
            }

            if (ch === '{') {
                depth++;
            } else if (ch === '}') {
                depth--;
                if (depth === 0) {
                    const jsonEnd = i;
                    // Expect closing bracket after JSON
                    const closeBracketIndex = text.indexOf(']', jsonEnd + 1);
                    if (closeBracketIndex < 0) {
                        return null;
                    }

                    const json = text.substring(jsonStart, jsonEnd + 1).trim();
                    return {
                        startIndex: start,
                        endIndex: closeBracketIndex + 1,
                        json: json
                    };
                }
            }
        }

        return null;
    }

    function renderChartsInMessage(message) {
        if (!message || !message._pendingCharts || !message._pendingCharts.length) {
            return;
        }

        for (const c of message._pendingCharts) {
            const canvas = document.getElementById(c.chartId);
            if (!canvas) {
                continue;
            }

            if (typeof Chart === 'undefined') {
                console.error('Chart.js is not available on the page.');
                continue;
            }

            try {
                // Destroy existing chart instance if re-rendering
                if (canvas._chartInstance) {
                    canvas._chartInstance.destroy();
                }

                const cfg = typeof c.config === 'string' ? JSON.parse(c.config) : c.config;
                cfg.options ??= {};
                cfg.options.responsive = true;
                cfg.options.maintainAspectRatio = false;

                canvas._chartInstance = new Chart(canvas, cfg);
            } catch (e) {
                console.error('Error creating chart:', e);
            }
        }

        // Prevent re-render work
        message._pendingCharts = [];
    }

    // Parse markdown content via marked (which natively handles [chart:...] markers
    // through the registered extension) and collect pending chart configs for later
    // Chart.js rendering.
    function parseMarkdownContent(content, message) {
        _pendingCharts = [];
        const html = marked.parse(content, { renderer });
        message._pendingCharts = _pendingCharts.length > 0 ? [..._pendingCharts] : [];
        return DOMPurify.sanitize(html, { ADD_ATTR: ['target'] });
    }

    const initialize = (instanceConfig) => {

        const config = Object.assign({}, defaultConfig, instanceConfig);
        // Keep defaultConfig in sync so renderers use overridden values
        defaultConfig = config;

        if (!config.signalRHubUrl) {
            console.error('The signalRHubUrl is required.');
            return;
        }

        if (!config.appElementSelector) {
            console.error('The appElementSelector is required.');
            return;
        }

        if (!config.chatContainerElementSelector) {
            console.error('The chatContainerElementSelector is required.');
            return;
        }

        if (!config.inputElementSelector) {
            console.error('The inputElementSelector is required.');
            return;
        }

        if (!config.sendButtonElementSelector) {
            console.error('The sendButtonElementSelector is required.');
            return;
        }

        const app = Vue.createApp({
            data() {
                return {
                    inputElement: null,
                    buttonElement: null,
                    chatContainer: null,
                    placeholder: null,
                    isInteractionStarted: false,
                    isPlaceholderVisible: true,
                    isStreaming: false,
                    isNavigatingAway: false,
                    autoScroll: true,
                    stream: null,
                    messages: [],
                    prompt: '',
                    initialFieldValues: new Map(),
                    settingsDirty: false,
                    saveSettingsTimeout: null,
                    isRecording: false,
                    mediaRecorder: null,
                    preRecordingPrompt: '',
                    micButton: null,
                    speechToTextEnabled: config.chatMode === 'AudioInput' || config.chatMode === 'Conversation',
                    textToSpeechEnabled: config.chatMode === 'Conversation',
                    ttsVoiceName: config.ttsVoiceName || null,
                    audioChunks: [],
                    audioPlayQueue: [],
                    isPlayingAudio: false,
                    currentAudioElement: null,
                    conversationModeEnabled: config.chatMode === 'Conversation',
                    conversationButton: null,
                    isConversationMode: false,
                    notifications: [],
                };
            },
            computed: {
                lastAssistantIndex() {
                    for (var i = this.messages.length - 1; i >= 0; i--) {
                        if (this.messages[i].role === 'assistant') {
                            return i;
                        }
                    }
                    return -1;
                }
            },
            methods: {
                handleBeforeUnload() {
                    this.isNavigatingAway = true;
                },
                async startConnection() {
                    this.connection = new signalR.HubConnectionBuilder()
                        .withUrl(config.signalRHubUrl)
                        .withAutomaticReconnect()
                        .build();

                    // Allow long-running operations (e.g., multi-step MCP tool calls)
                    // without the client disconnecting prematurely.
                    this.connection.serverTimeoutInMilliseconds = 600000;
                    this.connection.keepAliveIntervalInMilliseconds = 15000;

                    this.connection.on("LoadInteraction", (data) => {
                        this.initializeInteraction(data.itemId, true);
                        this.messages = [];// Update the title field if it exists
                        const titleInput = document.querySelector('input[name="ChatInteraction.Title"]');
                        if (titleInput && data.title) {
                            titleInput.value = data.title;
                        }

                        (data.messages ?? []).forEach(msg => {
                            this.addMessage(msg);

                            this.$nextTick(() => {
                                renderChartsInMessage(msg);
                            });
                        });
                    });

                    this.connection.on("SettingsSaved", (itemId, title) => {
                        // Update the history list item if it exists
                        // Use a more specific selector to only target history list items, not other elements like the Clear History button
                        const historyItem = document.querySelector(`.chat-interaction-history-item[data-interaction-id="${itemId}"]`);
                        if (historyItem) {
                            historyItem.textContent = title || config.untitledText;
                        }
                    });

                    this.connection.on("ReceiveError", (error) => {
                        console.error("SignalR Error: ", error);

                        if (this.isRecording) {
                            this.stopRecording();
                        }
                    });

                    this.connection.on("ReceiveTranscript", (itemId, text, isFinal) => {
                        if (this.isConversationMode) {
                            if (!isFinal && text) {
                                this._conversationPartialTranscript = text;
                                var escaped = text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
                                var html = '<p class="ai-partial-transcript">' + escaped + '</p>';

                                // Show partial transcript as a live user message.
                                if (!this._conversationPartialMessage) {
                                    this.hidePlaceholder();
                                    this._conversationPartialMessage = {
                                        role: 'user',
                                        content: text,
                                        htmlContent: html,
                                        isPartial: true
                                    };
                                    this.messages.push(this._conversationPartialMessage);
                                } else {
                                    this._conversationPartialMessage.content = text;
                                    this._conversationPartialMessage.htmlContent = html;
                                }
                                this.scrollToBottom();
                            }
                            return;
                        }

                        if (text && !this._audioInputSent) {
                            this.prompt = this.preRecordingPrompt + text;
                            if (this.inputElement) {
                                this.inputElement.value = this.prompt;
                                this.inputElement.dispatchEvent(new Event('input'));
                            }
                        }
                    });

                    this.connection.on("ReceiveConversationUserMessage", (itemId, text) => {
                        if (text) {
                            this.stopAudio();

                            // If there's an interrupted assistant message still streaming,
                            // mark it as done to stop the spinner animation.
                            if (this._conversationAssistantMessage) {
                                var oldMsg = this.messages[this._conversationAssistantMessage.index];
                                if (oldMsg) {
                                    oldMsg.isStreaming = false;
                                }
                                this._conversationAssistantMessage = null;
                            }

                            // Replace the partial transcript message with the final one.
                            if (this._conversationPartialMessage) {
                                var escaped = text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
                                this._conversationPartialMessage.content = text;
                                this._conversationPartialMessage.htmlContent = '<p>' + escaped + '</p>';
                                this._conversationPartialMessage.isPartial = false;
                                this._conversationPartialMessage = null;
                            } else {
                                this.addMessage({
                                    role: 'user',
                                    content: text
                                });
                            }
                            this.scrollToBottom();
                        }
                    });

                    this.connection.on("ReceiveConversationAssistantToken", (itemId, messageId, token, responseId, appearance) => {
                        if (!this._conversationAssistantMessage) {
                            this.stopAudio();
                            this.hideTypingIndicator();

                            // Ensure no stale streaming indicators remain from prior messages.
                            for (var j = 0; j < this.messages.length; j++) {
                                if (this.messages[j].isStreaming) {
                                    this.messages[j].isStreaming = false;
                                }
                            }

                            var msgIndex = this.messages.length;
                            var newMessage = {
                                id: messageId,
                                role: "assistant",
                                content: "",
                                htmlContent: "",
                                isStreaming: true,
                                appearance: this.normalizeAssistantAppearance(appearance),
                            };
                            this.messages.push(newMessage);
                            this._conversationAssistantMessage = { index: msgIndex, content: '' };
                        }

                        this._conversationAssistantMessage.content += token;
                        var msg = this.messages[this._conversationAssistantMessage.index];
                        if (msg) {
                            if (!msg.appearance) {
                                msg.appearance = this.normalizeAssistantAppearance(appearance);
                            }
                            msg.content = this._conversationAssistantMessage.content;
                            msg.htmlContent = parseMarkdownContent(msg.content, msg);
                            this.$nextTick(() => {
                                renderChartsInMessage(msg);
                                this.scrollToBottom();
                            });
                        }
                    });

                    this.connection.on("ReceiveConversationAssistantComplete", (itemId, messageId) => {
                        if (this._conversationAssistantMessage) {
                            var msg = this.messages[this._conversationAssistantMessage.index];
                            if (msg) {
                                msg.isStreaming = false;
                            }
                            this._conversationAssistantMessage = null;
                        }
                    });

                    this.connection.on("ReceiveAudioChunk", (itemId, base64Audio, contentType) => {
                        if (base64Audio) {
                            const binaryString = atob(base64Audio);
                            const bytes = new Uint8Array(binaryString.length);
                            for (let i = 0; i < binaryString.length; i++) {
                                bytes[i] = binaryString.charCodeAt(i);
                            }
                            this.audioChunks.push(bytes);
                        }
                    });

                    this.connection.on("ReceiveAudioComplete", (itemId) => {
                        this.playCollectedAudio();
                    });

                    this.connection.on("HistoryCleared", (itemId) => {
                        // Clear messages and show placeholder
                        this.messages = [];
                        this.showPlaceholder();

                        // Hide the clear history button since there's no history now
                        const clearHistoryBtn = document.getElementById('clearHistoryBtn');
                        if (clearHistoryBtn) {
                            clearHistoryBtn.classList.add('d-none');
                        }
                    });

                    this.connection.on("ReceiveNotification", (notification) => {
                        this.receiveNotification(notification);
                    });

                    this.connection.on("UpdateNotification", (notification) => {
                        this.updateNotification(notification);
                    });

                    this.connection.on("RemoveNotification", (notificationType) => {
                        this.removeNotification(notificationType);
                    });

                    this.connection.onreconnecting(() => {
                        console.warn("SignalR: reconnecting...");
                    });

                    this.connection.onreconnected(() => {
                        console.info("SignalR: reconnected.");
                        this.reloadCurrentInteraction();
                    });

                    this.connection.onclose((error) => {
                        if (this.isNavigatingAway) {
                            return;
                        }

                        if (error) {
                            console.warn("SignalR connection closed with error:", error.message || error);
                        }
                    });

                    try {
                        await this.connection.start();
                    } catch (err) {
                        console.error("SignalR Connection Error: ", err);
                    }
                },
                addMessageInternal(message) {
                    if (message.role === 'assistant') {
                        message.appearance = this.normalizeAssistantAppearance(message.appearance);
                    }

                    if (message.content && !message.htmlContent) {
                        message.htmlContent = parseMarkdownContent(message.content, message);
                    }
                    this.fireEvent(new CustomEvent("addingChatInteractionMessage", { detail: { message: message } }));
                    this.messages.push(message);

                    this.$nextTick(() => {
                        this.fireEvent(new CustomEvent("addedChatInteractionMessage", { detail: { message: message } }));
                    });
                },
                addMessage(message) {
                    if (message.content) {
                        let processedContent = message.content.trim();

                        if (message.references && typeof message.references === "object" && Object.keys(message.references).length) {

                            // Only include references that were actually cited in the response.
                            const citedRefs = Object.entries(message.references).filter(([key]) => processedContent.includes(key));

                            if (citedRefs.length) {
                                // Sort by original index so display indices follow a natural order.
                                citedRefs.sort(([, a], [, b]) => a.index - b.index);

                                // Phase 1: Replace all markers with unique placeholders.
                                let displayIndex = 1;
                                for (const [key, value] of citedRefs) {
                                    const placeholder = `__CITE_${value.index}__`;
                                    processedContent = processedContent.replaceAll(key, placeholder);
                                    value._displayIndex = displayIndex++;
                                    value._placeholder = placeholder;
                                }

                                // Phase 2: Replace placeholders with sequential display indices.
                                for (const [, value] of citedRefs) {
                                    processedContent = processedContent.replaceAll(value._placeholder, `<sup><strong>${value._displayIndex}</strong></sup>`);
                                }

                                processedContent = processedContent.replaceAll('</strong></sup><sup>', '</strong></sup><sup>,</sup><sup>');

                                processedContent += '<br><br>';

                                for (const [key, value] of citedRefs) {
                                    const label = value.text || `[doc:${value.index}]`;
                                    processedContent += value.link
                                        ? `**${value._displayIndex}**. [${label}](${value.link})<br>`
                                        : `**${value._displayIndex}**. ${label}<br>`;
                                }
                            }
                        }

                        message.content = processedContent;
                        message.htmlContent = parseMarkdownContent(processedContent, message);
                    }

                    this.addMessageInternal(message);
                    this.hidePlaceholder();

                    this.$nextTick(() => {
                        // Render any pending charts once the DOM is updated
                        renderChartsInMessage(message);
                        this.scrollToBottom();
                    });
                },
                hidePlaceholder() {
                    if (this.placeholder) {
                        this.placeholder.classList.add('d-none');
                    }
                    this.isPlaceholderVisible = false;
                },
                showPlaceholder() {
                    if (this.placeholder) {
                        this.placeholder.classList.remove('d-none');
                    }
                    this.isPlaceholderVisible = true;
                },
                fireEvent(event) {
                    document.dispatchEvent(event);
                },
                normalizeAssistantAppearance(appearance) {
                    if (!appearance) {
                        return null;
                    }

                    var label = typeof appearance.label === 'string' ? appearance.label.trim() : '';
                    var icon = typeof appearance.icon === 'string' ? appearance.icon.trim() : '';
                    var cssClass = typeof appearance.cssClass === 'string' ? appearance.cssClass.trim() : '';
                    var disableStreamingAnimation = !!appearance.disableStreamingAnimation;

                    if (!label && !icon && !cssClass && !disableStreamingAnimation) {
                        return null;
                    }

                    return {
                        label: label,
                        icon: icon,
                        cssClass: cssClass,
                        disableStreamingAnimation: disableStreamingAnimation,
                    };
                },
                getAssistantLabel(message) {
                    var appearance = message ? this.normalizeAssistantAppearance(message.appearance) : null;
                    return appearance && appearance.label ? appearance.label : defaultConfig.assistantLabel;
                },
                getAssistantRoleClasses(message) {
                    var appearance = message ? this.normalizeAssistantAppearance(message.appearance) : null;
                    var classes = ['ai-chat-msg-role'];

                    if (appearance && appearance.cssClass) {
                        classes.push(appearance.cssClass);
                    } else {
                        classes.push('ai-chat-msg-role-assistant');
                    }

                    return classes;
                },
                getAssistantIconClasses(message, index) {
                    var appearance = message ? this.normalizeAssistantAppearance(message.appearance) : null;
                    return [this.shouldAnimateAssistantIcon(message, index) ? 'ai-streaming-icon' : 'ai-bot-icon', appearance && appearance.cssClass ? appearance.cssClass : ''];
                },
                getAssistantIcon(message) {
                    var appearance = message ? this.normalizeAssistantAppearance(message.appearance) : null;
                    return appearance && appearance.icon ? appearance.icon : 'fa fa-robot';
                },
                shouldAnimateAssistantIcon(message, index) {
                    var appearance = message ? this.normalizeAssistantAppearance(message.appearance) : null;
                    return !!message &&
                        message.isStreaming &&
                        index === this.lastAssistantIndex &&
                        !(appearance && appearance.disableStreamingAnimation);
                },
                isIndicator(message) {
                    return message.role === 'indicator';
                },
                async sendMessage() {
                    let trimmedPrompt = this.prompt.trim();

                    if (!trimmedPrompt) {
                        return;
                    }

                    // Stop any active recording before sending.
                    if (this.isRecording) {
                        this.stopRecording();
                    }

                    // Prevent stale ReceiveTranscript events from repopulating the prompt.
                    this._audioInputSent = true;

                    // Flush any pending settings save before sending a message
                    // to prevent concurrent hub calls that can cause database deadlocks.
                    await this.flushPendingSave();

                    this.addMessage({
                        role: 'user',
                        content: trimmedPrompt
                    });

                    // Show the clear history button since we now have prompts
                    const clearHistoryBtn = document.getElementById('clearHistoryBtn');
                    if (clearHistoryBtn) {
                        clearHistoryBtn.classList.remove('d-none');
                    }

                    this.streamMessage(trimmedPrompt);
                    this.inputElement.value = '';
                    this.prompt = '';
                },
                streamMessage(trimmedPrompt) {
                    if (this.stream) {
                        this.stream.dispose();
                        this.stream = null;
                    }

                    this.streamingStarted();
                    this.showTypingIndicator();
                    this.autoScroll = true;

                    var content = '';
                    var references = {};
                    var lastResponseId = null;

                    var messageIndex = this.messages.length;
                    var currentItemId = this.getItemId();

                    this.stream = this.connection.stream("SendMessage", currentItemId, trimmedPrompt)
                        .subscribe({
                            next: (chunk) => {
                                let message = this.messages[messageIndex];

                                if (!message) {
                                    if (chunk.sessionId && !currentItemId) {
                                        this.setItemId(chunk.sessionId);
                                    }

                                    this.hideTypingIndicator();
                                    messageIndex = this.messages.length;
                                    let newMessage = {
                                        role: "assistant",
                                        content: "",
                                        htmlContent: "",
                                        isStreaming: true,
                                    };

                                    this.messages.push(newMessage);
                                    message = newMessage;
                                }

                                if (chunk.references && typeof chunk.references === "object" && Object.keys(chunk.references).length) {
                                    for (const [key, value] of Object.entries(chunk.references)) {
                                        references[key] = value;
                                    }
                                }

                                if (chunk.content) {
                                    // When the responseId changes (e.g., after an internal tool call),
                                    // insert a line break to visually separate response segments.
                                    if (chunk.responseId && lastResponseId && chunk.responseId !== lastResponseId) {
                                        content += '\n\n';
                                    }

                                    if (chunk.responseId) {
                                        lastResponseId = chunk.responseId;
                                    }

                                    let processedContent = chunk.content;

                                    for (const [key, value] of Object.entries(references)) {
                                        processedContent = processedContent.replaceAll(key, `<sup><strong>${value.index}</strong></sup>`);
                                    }

                                    content += processedContent.replaceAll('</strong></sup><sup>', '</strong></sup><sup>,</sup><sup>');
                                }

                                message.content = content;

                                message.htmlContent = parseMarkdownContent(content, message);

                                this.messages[messageIndex] = message;

                                this.$nextTick(() => {
                                    renderChartsInMessage(message);
                                    this.scrollToBottom();
                                });
                            },
                            complete: () => {
                                this.processReferences(references, messageIndex);
                                this.streamingFinished();

                                let msg = this.messages[messageIndex];
                                if (msg) {
                                    msg.isStreaming = false;
                                }

                                if (!msg || !msg.content) {
                                    // No content received at all.
                                    this.hideTypingIndicator();
                                }

                                // Trigger text-to-speech only in conversation mode.
                                if (this.isConversationMode && this.textToSpeechEnabled && msg && msg.content) {
                                    this.synthesizeSpeech(msg.content);
                                }

                                this.stream?.dispose();
                                this.stream = null;
                            },
                            error: (err) => {
                                this.processReferences(references, messageIndex);
                                this.streamingFinished();

                                let msg = this.messages[messageIndex];
                                if (msg) {
                                    msg.isStreaming = false;
                                }

                                this.hideTypingIndicator();

                                if (!this.isNavigatingAway) {
                                    this.addMessage(this.getServiceDownMessage());
                                }

                                this.stream?.dispose();
                                this.stream = null;

                                console.error("Stream error:", err);
                            }
                        });
                },
                getServiceDownMessage() {
                    return {
                        role: "assistant",
                        content: "Our service is currently unavailable. Please try again later. We apologize for the inconvenience.",
                        htmlContent: "",
                    };
                },
                processReferences(references, messageIndex) {
                    if (Object.keys(references).length) {
                        let message = this.messages[messageIndex];
                        const content = message.content || '';

                        // Only include references that were actually cited in the response.
                        // Check both raw [doc:N] markers and already-rendered <sup> tags from streaming.
                        const citedRefs = Object.entries(references).filter(([key, value]) =>
                            content.includes(key) || content.includes(`<sup><strong>${value.index}</strong></sup>`)
                        );

                        if (!citedRefs.length) {
                            return;
                        }

                        // Sort by original index so display indices follow a natural order.
                        citedRefs.sort(([, a], [, b]) => a.index - b.index);

                        // Phase 1: Replace all markers with unique placeholders to avoid collisions during remapping.
                        let processed = content.trim();
                        let displayIndex = 1;
                        for (const [key, value] of citedRefs) {
                            const placeholder = `__CITE_${value.index}__`;
                            processed = processed.replaceAll(key, placeholder);
                            processed = processed.replaceAll(`<sup><strong>${value.index}</strong></sup>`, placeholder);
                            value._displayIndex = displayIndex++;
                            value._placeholder = placeholder;
                        }

                        // Phase 2: Replace placeholders with sequential display indices.
                        for (const [, value] of citedRefs) {
                            processed = processed.replaceAll(value._placeholder, `<sup><strong>${value._displayIndex}</strong></sup>`);
                        }

                        processed = processed.replaceAll('</strong></sup><sup>', '</strong></sup><sup>,</sup><sup>');

                        processed += '<br><br>';

                        for (const [key, value] of citedRefs) {
                            const label = value.text || `[doc:${value.index}]`;
                            processed += value.link
                                ? `**${value._displayIndex}**. [${label}](${value.link})<br>`
                                : `**${value._displayIndex}**. ${label}<br>`;
                        }

                        message.content = processed;
                        message.htmlContent = parseMarkdownContent(processed, message);

                        this.messages[messageIndex] = message;

                        this.scrollToBottom();
                    }
                },
                streamingStarted() {
                    var stopIcon = this.buttonElement.getAttribute('data-stop-icon');

                    if (stopIcon) {
                        this.buttonElement.replaceChildren(DOMPurify.sanitize(stopIcon, { RETURN_DOM_FRAGMENT: true }));
                    }
                },
                streamingFinished() {
                    var startIcon = this.buttonElement.getAttribute('data-start-icon');

                    if (startIcon) {
                        this.buttonElement.replaceChildren(DOMPurify.sanitize(startIcon, { RETURN_DOM_FRAGMENT: true }));
                    }

                    // Directly manipulate the DOM to stop all streaming animations.
                    if (this.chatContainer) {
                        var icons = this.chatContainer.querySelectorAll('.ai-streaming-icon');
                        for (var i = 0; i < icons.length; i++) {
                            icons[i].classList.remove('ai-streaming-icon');
                            icons[i].classList.add('ai-bot-icon');
                        }
                    }

                    // Also update Vue data for consistency.
                    for (var i = 0; i < this.messages.length; i++) {
                        if (this.messages[i].isStreaming) {
                            this.messages[i].isStreaming = false;
                        }
                    }

                    // Save any settings that were deferred during streaming.
                    if (this.settingsDirty) {
                        this.debouncedSaveSettings();
                    }
                },
                synthesizeSpeech(text) {
                    if (!this.textToSpeechEnabled || !text || !this.connection) {
                        return;
                    }

                    this.audioChunks = [];
                    this.isPlayingAudio = true;

                    this.connection.invoke("SynthesizeSpeech", this.getItemId(), text, this.ttsVoiceName)
                        .catch(err => {
                            console.error("TTS synthesis error:", err);
                            this.isPlayingAudio = false;
                        });
                },
                playCollectedAudio() {
                    if (this.audioChunks.length === 0) {
                        if (!this.isPlayingAudio && this.audioPlayQueue.length === 0) {
                            this.isPlayingAudio = false;
                        }
                        return;
                    }

                    const totalLength = this.audioChunks.reduce((sum, chunk) => sum + chunk.length, 0);
                    const combined = new Uint8Array(totalLength);
                    let offset = 0;
                    for (const chunk of this.audioChunks) {
                        combined.set(chunk, offset);
                        offset += chunk.length;
                    }
                    this.audioChunks = [];

                    const blob = new Blob([combined], { type: 'audio/mp3' });

                    // If audio is currently playing, queue this blob for later.
                    if (this.isPlayingAudio && this.currentAudioElement) {
                        this.audioPlayQueue.push(blob);
                        return;
                    }

                    this.playAudioBlob(blob);
                },
                playAudioBlob(blob) {
                    const url = URL.createObjectURL(blob);
                    const audio = new Audio(url);

                    this.currentAudioElement = audio;
                    this.isPlayingAudio = true;

                    audio.addEventListener('ended', () => {
                        URL.revokeObjectURL(url);
                        this.currentAudioElement = null;
                        this.playNextInQueue();
                    });

                    audio.addEventListener('error', () => {
                        URL.revokeObjectURL(url);
                        this.currentAudioElement = null;
                        this.playNextInQueue();
                    });

                    audio.play().catch(err => {
                        console.error("Audio playback error:", err);
                        URL.revokeObjectURL(url);
                        this.currentAudioElement = null;
                        this.isPlayingAudio = false;
                    });
                },
                playNextInQueue() {
                    if (this.audioPlayQueue.length > 0) {
                        const nextBlob = this.audioPlayQueue.shift();
                        this.playAudioBlob(nextBlob);
                    } else {
                        this.isPlayingAudio = false;
                        this.conversationModeOnAudioEnded();
                    }
                },
                stopAudio() {
                    if (this.currentAudioElement) {
                        this.currentAudioElement.pause();
                        this.currentAudioElement.currentTime = 0;
                        this.currentAudioElement = null;
                    }
                    this.audioChunks = [];
                    this.audioPlayQueue = [];
                    this.isPlayingAudio = false;
                },
                toggleConversationMode() {
                    if (this.isConversationMode) {
                        this.stopConversationMode();
                    } else {
                        this.startConversationMode();
                    }
                },
                startConversationMode() {
                    if (!this.conversationModeEnabled || this.isConversationMode || !this.connection) {
                        return;
                    }

                    this.isConversationMode = true;
                    this.updateConversationButton();
                    this._conversationPartialTranscript = '';
                    this._conversationAssistantMessage = null;
                    this._conversationPartialMessage = null;

                    // Remove any previous conversation ended notification.
                    this.removeNotification('conversation-ended');

                    navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true } })
                        .then(stream => {
                            var mimeType = MediaRecorder.isTypeSupported('audio/ogg;codecs=opus')
                                ? 'audio/ogg;codecs=opus'
                                : MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
                                    ? 'audio/webm;codecs=opus'
                                    : 'audio/webm';

                            this.mediaRecorder = new MediaRecorder(stream, {
                                mimeType: mimeType,
                                audioBitsPerSecond: 128000,
                            });

                            this._conversationSubject = new signalR.Subject();
                            this._conversationStream = stream;

                            // Create an AnalyserNode for volume-based interrupt detection.
                            var AudioCtx = window.AudioContext || window.webkitAudioContext;
                            if (AudioCtx) {
                                this._conversationAudioCtx = new AudioCtx();
                                this._conversationAnalyser = this._conversationAudioCtx.createAnalyser();
                                this._conversationAnalyser.fftSize = 256;
                                var micSource = this._conversationAudioCtx.createMediaStreamSource(stream);
                                micSource.connect(this._conversationAnalyser);
                            }

                            var pendingChunk = Promise.resolve();
                            var analyser = this._conversationAnalyser;
                            var interruptVolumeThreshold = 30;

                            this.mediaRecorder.addEventListener('dataavailable', (e) => {
                                if (e.data && e.data.size > 0) {
                                    // During TTS playback, check mic volume to detect
                                    // user interruption (speaking above threshold).
                                    if (this.isPlayingAudio && analyser) {
                                        var freqData = new Uint8Array(analyser.frequencyBinCount);
                                        analyser.getByteFrequencyData(freqData);
                                        var sum = 0;
                                        for (var k = 0; k < freqData.length; k++) { sum += freqData[k]; }
                                        var avg = sum / freqData.length;

                                        if (avg >= interruptVolumeThreshold) {
                                            // User is speaking — interrupt TTS playback.
                                            this.stopAudio();
                                        }
                                    }

                                    // Always send audio to STT — browser echo cancellation
                                    // handles speaker echo; continuous audio avoids gaps
                                    // that increase recognition latency.
                                    pendingChunk = pendingChunk.then(async () => {
                                        var data = await e.data.arrayBuffer();
                                        var uint8Array = new Uint8Array(data);
                                        var binaryString = uint8Array.reduce(function (str, byte) { return str + String.fromCharCode(byte); }, '');
                                        var base64 = btoa(binaryString);
                                        try {
                                            this._conversationSubject.next(base64);
                                        } catch (err) {
                                            // Subject may have been completed already.
                                        }
                                    });
                                }
                            });

                            this.mediaRecorder.addEventListener('stop', () => {
                                stream.getTracks().forEach(track => track.stop());
                                pendingChunk.then(() => {
                                    try {
                                        this._conversationSubject.complete();
                                    } catch (err) {
                                        // Already completed.
                                    }
                                });
                            });

                            var itemId = this.getItemId();

                            var language = document.documentElement.lang || 'en-US';
                            this.connection.send("StartConversation", itemId, this._conversationSubject, mimeType, language);
                            this.mediaRecorder.start(1000);
                            this.isRecording = true;
                        })
                        .catch(err => {
                            console.error('Microphone access denied:', err);
                            this.isConversationMode = false;
                            this.updateConversationButton();
                        });
                },
                stopConversationMode() {
                    if (!this.isConversationMode) {
                        return;
                    }

                    this.isConversationMode = false;
                    this.updateConversationButton();

                    // Signal the server to cancel all in-progress STT/TTS streams immediately.
                    if (this.connection) {
                        this.connection.invoke("StopConversation").catch(function () { });
                    }

                    if (this.isRecording && this.mediaRecorder) {
                        this.mediaRecorder.stop();
                        this.isRecording = false;
                    }

                    this.stopAudio();
                    this._conversationPartialTranscript = '';
                    this._conversationPartialMessage = null;

                    // Clean up the AudioContext used for volume monitoring.
                    if (this._conversationAudioCtx) {
                        this._conversationAudioCtx.close().catch(function () { });
                        this._conversationAudioCtx = null;
                        this._conversationAnalyser = null;
                    }

                    // Mark any in-flight assistant message as done to stop the spinner.
                    if (this._conversationAssistantMessage) {
                        var msg = this.messages[this._conversationAssistantMessage.index];
                        if (msg) {
                            msg.isStreaming = false;
                        }
                        this._conversationAssistantMessage = null;
                    }

                    // Safety net: clear all lingering streaming indicators.
                    for (var i = 0; i < this.messages.length; i++) {
                        if (this.messages[i].isStreaming) {
                            this.messages[i].isStreaming = false;
                        }
                    }

                    // Show a "conversation ended" notification system message.
                    this.receiveNotification({
                        type: 'conversation-ended',
                        content: 'Conversation ended.',
                        icon: 'fa-solid fa-circle-check',
                        dismissible: true
                    });
                },
                updateConversationButton() {
                    if (!this.conversationButton) {
                        return;
                    }

                    if (this.isConversationMode) {
                        this.conversationButton.classList.add('active', 'btn-primary');
                        this.conversationButton.classList.remove('btn-dark', 'btn-outline-secondary');
                        this.conversationButton.title = this.conversationButton.getAttribute('data-end-title') || 'End Conversation';
                        var endHtml = this.conversationButton.getAttribute('data-end-html');
                        if (endHtml) {
                            this.conversationButton.replaceChildren(DOMPurify.sanitize(endHtml, { RETURN_DOM_FRAGMENT: true }));
                        }
                    } else {
                        this.conversationButton.classList.remove('active', 'btn-primary');
                        this.conversationButton.classList.add('btn-dark');
                        this.conversationButton.title = this.conversationButton.getAttribute('data-start-title') || 'Start Conversation';
                        var startHtml = this.conversationButton.getAttribute('data-start-html');
                        if (startHtml) {
                            this.conversationButton.replaceChildren(DOMPurify.sanitize(startHtml, { RETURN_DOM_FRAGMENT: true }));
                        }
                    }
                },
                conversationModeSendPrompt() {
                    // Legacy: only used by AudioInput mode's ReceiveTranscript.
                },
                conversationModeOnAudioEnded() {
                    // Legacy: in conversation mode, continuation is handled
                    // by the persistent stream.
                },
                showTypingIndicator() {
                    this.addMessage({
                        role: 'indicator',
                        htmlContent: config.indicatorTemplate
                    });
                },
                hideTypingIndicator() {
                    const originalLength = this.messages.length;
                    this.messages = this.messages.filter(msg => msg.role !== 'indicator');
                    const removedCount = originalLength - this.messages.length;
                    return removedCount;
                },
                scrollToBottom() {
                    if (!this.autoScroll) {
                        return;
                    }
                    setTimeout(() => {
                        this.chatContainer.scrollTop = this.chatContainer.scrollHeight - this.chatContainer.clientHeight;
                    }, 50);
                },
                handleUserInput(event) {
                    this.prompt = event.target.value;
                },
                getItemId() {
                    return this.inputElement.getAttribute('data-interaction-id');
                },
                receiveNotification(notification) {
                    // Replace existing notification with same type, or add new one.
                    var idx = this.notifications.findIndex(n => n.type === notification.type);
                    if (idx >= 0) {
                        this.notifications.splice(idx, 1, notification);
                    } else {
                        this.notifications.push(notification);
                    }
                    this.scrollToBottom();
                },
                updateNotification(notification) {
                    var idx = this.notifications.findIndex(n => n.type === notification.type);
                    if (idx >= 0) {
                        this.notifications.splice(idx, 1, notification);
                        this.scrollToBottom();
                    }
                },
                removeNotification(notificationType) {
                    this.notifications = this.notifications.filter(n => n.type !== notificationType);
                },
                dismissNotification(notificationType) {
                    this.removeNotification(notificationType);
                },
                handleNotificationAction(notificationType, actionName) {
                    if (!this.connection) {
                        return;
                    }
                    var itemId = this.getItemId();
                    this.connection.invoke('HandleNotificationAction', itemId, notificationType, actionName)
                        .catch(err => console.error('Failed to handle notification action:', err));
                },
                setItemId(itemId) {
                    this.inputElement.setAttribute('data-interaction-id', itemId || '');
                },
                resetInteraction() {
                    this.setItemId('');
                    this.isInteractionStarted = false;
                    this.messages = [];
                    this.showPlaceholder();
                },
                initializeApp() {
                    this.inputElement = document.querySelector(config.inputElementSelector);
                    this.buttonElement = document.querySelector(config.sendButtonElementSelector);
                    this.chatContainer = document.querySelector(config.chatContainerElementSelector);
                    this.placeholder = document.querySelector(config.placeholderElementSelector);

                    const itemId = this.getItemId();
                    if (itemId) {
                        this.loadInteraction(itemId);
                    }

                    // Pause auto-scroll when the user manually scrolls up during streaming.
                    this.chatContainer.addEventListener('scroll', () => {
                        if (!this.stream) {
                            return;
                        }
                        const threshold = 30;
                        const atBottom = this.chatContainer.scrollHeight - this.chatContainer.clientHeight - this.chatContainer.scrollTop <= threshold;
                        this.autoScroll = atBottom;
                    });

                    this.inputElement.addEventListener('keyup', event => {
                        if (this.stream != null) {
                            return;
                        }

                        if (event.key === "Enter" && !event.shiftKey) {
                            this.buttonElement.click();
                        }
                    });

                    this.inputElement.addEventListener('input', (e) => {
                        this.handleUserInput(e);

                        if (e.target.value.trim()) {
                            this.buttonElement.removeAttribute('disabled');
                        } else {
                            this.buttonElement.setAttribute('disabled', true);
                        }
                    });

                    this.inputElement.addEventListener('paste', (e) => {
                        // Use setTimeout to allow the paste to complete before checking the value
                        setTimeout(() => {
                            this.prompt = this.inputElement.value;
                            if (this.inputElement.value.trim()) {
                                this.buttonElement.removeAttribute('disabled');
                            } else {
                                this.buttonElement.setAttribute('disabled', true);
                            }
                        }, 0);
                    });

                    this.buttonElement.addEventListener('click', () => {
                        if (this.stream != null) {
                            this.stream.dispose();
                            this.stream = null;

                            this.streamingFinished();
                            this.hideTypingIndicator();

                            // Clean up: remove empty assistant message or stop streaming animation.
                            if (this.messages.length > 0) {
                                const lastMsg = this.messages[this.messages.length - 1];
                                if (lastMsg.role === 'assistant' && !lastMsg.content) {
                                    this.messages.pop();
                                } else if (lastMsg.isStreaming) {
                                    lastMsg.isStreaming = false;
                                }
                            }

                            return;
                        }

                        this.sendMessage();
                    });

                    const chatInteractionItems = document.getElementsByClassName('chat-interaction-history-item');

                    for (var i = 0; i < chatInteractionItems.length; i++) {
                        chatInteractionItems[i].addEventListener('click', (e) => {
                            e.preventDefault();

                            var itemId = e.target.getAttribute('data-interaction-id');

                            if (!itemId) {
                                console.error('An element with the class chat-interaction-history-item with no data-interaction-id set.');
                                return;
                            }

                            this.loadInteraction(itemId);
                        });
                    }

                    for (let i = 0; i < config.messages.length; i++) {
                        this.addMessage(config.messages[i]);
                    }

                    // Delegate click for code block copy buttons.
                    if (this.chatContainer) {
                        this.chatContainer.addEventListener('click', (e) => {
                            var btn = e.target.closest('.ai-code-copy-btn');
                            if (!btn) {
                                return;
                            }

                            var block = btn.closest('.ai-code-block') || btn.closest('pre');
                            if (!block) {
                                return;
                            }

                            var codeEl = block.querySelector('code');
                            if (codeEl) {
                                navigator.clipboard.writeText(codeEl.textContent);
                                var copiedText = config.codeCopiedText || 'Copied!';
                                btn.innerHTML = '<i class="fa-solid fa-check"></i> ' + copiedText;
                                setTimeout(() => {
                                    btn.innerHTML = '<i class="fa-regular fa-copy"></i>';
                                }, 2000);
                            }
                        });
                    }

                    // Add event listeners for all settings fields with "ChatInteraction." prefix
                    // Exclude tool and MCP connection inputs (they have special handling with debouncing)
                    const settingsInputs = document.querySelectorAll(
                        'input[name^="ChatInteraction."]:not([name*=".Tools["]):not([name*=".Connections["]), ' +
                        'select[name^="ChatInteraction."]:not([name*=".Tools["]):not([name*=".Connections["]), ' +
                        'textarea[name^="ChatInteraction."]:not([name*=".Tools["]):not([name*=".Connections["])'
                    );

                    settingsInputs.forEach(input => {
                        const isCheckbox = input.type === 'checkbox';
                        const isSelect = input.tagName === 'SELECT';

                        // Checkboxes & selects save immediately
                        if (isCheckbox || isSelect) {
                            input.addEventListener('change', () => {
                                this.settingsDirty = true;
                                this.debouncedSaveSettings();
                            });
                            return;
                        }

                        // Text / textarea / number inputs → save on blur if changed
                        input.addEventListener('focus', () => {
                            this.initialFieldValues.set(input, input.value);
                        });

                        input.addEventListener('blur', () => {
                            const initialValue = this.initialFieldValues.get(input);
                            const hasChanged =
                                initialValue !== undefined && input.value !== initialValue;

                            if (hasChanged) {
                                this.settingsDirty = true;
                                this.debouncedSaveSettings();
                            }

                            this.initialFieldValues.delete(input);
                        });
                    });

                    // Add event listeners for tool checkboxes with debouncing (850ms)
                    const toolCheckboxes = document.querySelectorAll('input[type="checkbox"][name$="].IsSelected"][name^="ChatInteraction.Tools["]');
                    toolCheckboxes.forEach(checkbox => {
                        checkbox.addEventListener('change', () => {
                            this.settingsDirty = true;
                            this.debouncedSaveSettings();
                        });
                    });

                    // Add event listeners for "Select All" group toggle checkboxes with debouncing (850ms)
                    const groupToggleCheckboxes = document.querySelectorAll('input[type="checkbox"].group-toggle');
                    groupToggleCheckboxes.forEach(toggle => {
                        toggle.addEventListener('change', () => {
                            this.settingsDirty = true;
                            this.debouncedSaveSettings();
                        });
                    });

                    // Add event listeners for MCP connection checkboxes with debouncing (850ms)
                    const mcpCheckboxes = document.querySelectorAll('input[type="checkbox"][name$="].IsSelected"][name^="ChatInteraction.Connections["]');
                    mcpCheckboxes.forEach(checkbox => {
                        checkbox.addEventListener('change', () => {
                            this.settingsDirty = true;
                            this.debouncedSaveSettings();
                        });
                    });

                    // Add event listeners for agent checkboxes with debouncing (850ms)
                    const agentCheckboxes = document.querySelectorAll('input[type="checkbox"][name$="].IsSelected"][name^="ChatInteraction.Agents["]');
                    agentCheckboxes.forEach(checkbox => {
                        checkbox.addEventListener('change', () => {
                            this.settingsDirty = true;
                            this.debouncedSaveSettings();
                        });
                    });

                    // Add event listener for "Select All Agents" toggle checkbox with debouncing (850ms)
                    const agentGlobalToggle = document.querySelector('.ci-agent-global-toggle');
                    if (agentGlobalToggle) {
                        agentGlobalToggle.addEventListener('change', () => {
                            this.settingsDirty = true;
                            this.debouncedSaveSettings();
                        });
                    }

                    // Add event listener for clear history button
                    const clearHistoryBtn = document.getElementById('clearHistoryBtn');
                    if (clearHistoryBtn) {
                        clearHistoryBtn.addEventListener('click', () => {
                            const itemId = clearHistoryBtn.getAttribute('data-interaction-id');
                            if (itemId) {
                                this.clearHistory(itemId);
                            }
                        });
                    }

                    // Initialize speech-to-text microphone button.
                    if (this.speechToTextEnabled && config.micButtonElementSelector) {
                        this.micButton = document.querySelector(config.micButtonElementSelector);
                        if (this.micButton) {
                            this.micButton.style.display = '';
                            this.micButton.addEventListener('click', () => {
                                this.toggleRecording();
                            });
                        }
                    }

                    // Initialize conversation mode button.
                    if (this.conversationModeEnabled && config.conversationButtonElementSelector) {
                        this.conversationButton = document.querySelector(config.conversationButtonElementSelector);
                        if (this.conversationButton) {
                            this.conversationButton.addEventListener('click', () => {
                                this.toggleConversationMode();
                            });
                        }
                    }
                },
                loadInteraction(itemId) {
                    this.connection.invoke("LoadInteraction", itemId).catch(err => console.error(err));
                },
                reloadCurrentInteraction() {
                    const itemId = this.getItemId();
                    if (itemId) {
                        this.loadInteraction(itemId);
                    }
                },
                clearHistory(itemId) {
                    const self = this;
                    confirmDialog({
                        title: config.clearHistoryTitle,
                        message: config.clearHistoryMessage,
                        okText: config.clearHistoryOkText,
                        cancelText: config.clearHistoryCancelText,
                        callback: function (confirmed) {
                            if (confirmed) {
                                // Cancel any active stream before clearing history.
                                if (self.stream) {
                                    self.stream.dispose();
                                    self.stream = null;
                                    self.hideTypingIndicator();
                                    self.streamingFinished();
                                }

                                self.connection.invoke("ClearHistory", itemId)
                                    .catch(err => console.error('Error clearing history:', err));
                            }
                        }
                    });
                },
                debouncedSaveSettings() {
                    // Clear any existing timeout to reset the debounce timer
                    if (this.saveSettingsTimeout) {
                        clearTimeout(this.saveSettingsTimeout);
                    }

                    // Don't save while streaming — it will be saved when streaming completes.
                    if (this.stream) {
                        return;
                    }

                    // Set a new timeout to save after 850ms of no changes
                    this.saveSettingsTimeout = setTimeout(() => {
                        if (this.settingsDirty) {
                            this.saveSettings();
                            this.settingsDirty = false;
                        }
                        this.saveSettingsTimeout = null;
                    }, 850);
                },
                getSelectedToolNames() {
                    // Find all checked tool checkboxes and get the corresponding ItemId values
                    const toolNames = [];
                    const toolCheckboxes = document.querySelectorAll('input[type="checkbox"][name$="].IsSelected"][name^="ChatInteraction.Tools["]:checked');

                    toolCheckboxes.forEach(checkbox => {
                        // Extract the base name pattern to find the corresponding hidden ItemId input
                        // Checkbox name: ChatInteraction.Tools[Content Definitions][0].IsSelected
                        // Hidden name:   ChatInteraction.Tools[Content Definitions][0].ItemId
                        const baseName = checkbox.name.replace('.IsSelected', '.ItemId');
                        const hiddenInput = document.querySelector(`input[type="hidden"][name="${baseName}"]`);

                        if (hiddenInput && hiddenInput.value) {
                            toolNames.push(hiddenInput.value);
                        }
                    });

                    return toolNames;
                },
                getSelectedMcpConnectionIds() {
                    const connectionIds = [];
                    const mcpCheckboxes = document.querySelectorAll('input[type="checkbox"][name$="].IsSelected"][name^="ChatInteraction.Connections["]:checked');

                    mcpCheckboxes.forEach(checkbox => {
                        const baseName = checkbox.name.replace('.IsSelected', '.ItemId');
                        const hiddenInput = document.querySelector(`input[type="hidden"][name="${baseName}"]`);

                        if (hiddenInput && hiddenInput.value) {
                            connectionIds.push(hiddenInput.value);
                        }
                    });

                    return connectionIds;
                },
                getSelectedAgentNames() {
                    const agentNames = [];
                    const agentCheckboxes = document.querySelectorAll('input[type="checkbox"][name$="].IsSelected"][name^="ChatInteraction.Agents["]:checked');

                    agentCheckboxes.forEach(checkbox => {
                        const baseName = checkbox.name.replace('.IsSelected', '.ItemId');
                        const hiddenInput = document.querySelector(`input[type="hidden"][name="${baseName}"]`);

                        if (hiddenInput && hiddenInput.value) {
                            agentNames.push(hiddenInput.value);
                        }
                    });

                    return agentNames;
                },
                saveSettings() {
                    const itemId = this.getItemId();
                    if (!itemId) {
                        return Promise.resolve();
                    }

                    const settings = {};

                    // Collect all form inputs with the "ChatInteraction." prefix generically.
                    // This avoids coupling the JS to specific field names — new fields added by
                    // any module are automatically included.
                    const inputs = document.querySelectorAll(
                        'input[name^="ChatInteraction."]:not([type="hidden"]):not([name*=".Tools["]):not([name*=".Connections["]):not([name*=".Agents["]), ' +
                        'select[name^="ChatInteraction."]:not([name*=".Tools["]):not([name*=".Connections["]):not([name*=".Agents["]), ' +
                        'textarea[name^="ChatInteraction."]:not([name*=".Tools["]):not([name*=".Connections["]):not([name*=".Agents["])'
                    );

                    inputs.forEach(input => {
                        // Extract field name: "ChatInteraction.Title" → "title"
                        const fieldName = input.name.replace('ChatInteraction.', '');
                        const key = fieldName.charAt(0).toLowerCase() + fieldName.slice(1);

                        if (input.type === 'checkbox') {
                            settings[key] = input.checked;
                        } else if (input.type === 'number') {
                            settings[key] = input.value ? parseFloat(input.value) : null;
                        } else {
                            settings[key] = input.value || null;
                        }
                    });

                    // Add tool, MCP connection, and agent collections (special handling).
                    settings.toolNames = this.getSelectedToolNames();
                    settings.mcpConnectionIds = this.getSelectedMcpConnectionIds();
                    settings.agentNames = this.getSelectedAgentNames();

                    return this.connection.invoke("SaveSettings", itemId, settings)
                        .catch(err => console.error('Error saving settings:', err));
                },
                flushPendingSave() {
                    if (this.saveSettingsTimeout) {
                        clearTimeout(this.saveSettingsTimeout);
                        this.saveSettingsTimeout = null;
                    }

                    if (this.settingsDirty) {
                        this.settingsDirty = false;
                        return this.saveSettings();
                    }

                    return Promise.resolve();
                },
                initializeInteraction(itemId, force) {
                    if (this.isInteractionStarted && !force) {
                        return;
                    }
                    this.fireEvent(new CustomEvent("initializingChatInteraction", { detail: { itemId: itemId } }));
                    this.setItemId(itemId);
                    this.isInteractionStarted = true;
                },
                copyResponse(message) {
                    navigator.clipboard.writeText(message);
                },
                startRecording() {
                    if (this.isRecording || !this.connection) {
                        return;
                    }

                    navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true } })
                        .then(stream => {
                            var mimeType = MediaRecorder.isTypeSupported('audio/ogg;codecs=opus')
                                ? 'audio/ogg;codecs=opus'
                                : MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
                                    ? 'audio/webm;codecs=opus'
                                    : 'audio/webm';

                            this.mediaRecorder = new MediaRecorder(stream, {
                                mimeType: mimeType,
                                audioBitsPerSecond: 128000,
                            });

                            this.preRecordingPrompt = this.prompt;
                            this._audioInputSent = false;

                            var subject = new signalR.Subject();
                            var itemId = this.getItemId();
                            var pendingChunk = Promise.resolve();

                            this.mediaRecorder.addEventListener('dataavailable', (e) => {
                                if (e.data && e.data.size > 0) {
                                    pendingChunk = pendingChunk.then(async () => {
                                        var data = await e.data.arrayBuffer();
                                        var uint8Array = new Uint8Array(data);
                                        var binaryString = uint8Array.reduce(function (str, byte) { return str + String.fromCharCode(byte); }, '');
                                        var base64 = btoa(binaryString);
                                        subject.next(base64);
                                    });
                                }
                            });

                            this.mediaRecorder.addEventListener('stop', () => {
                                stream.getTracks().forEach(track => track.stop());
                                pendingChunk.then(() => subject.complete());
                            });

                            var language = document.documentElement.lang || 'en-US';
                            this.connection.send("SendAudioStream", itemId, subject, mimeType, language);
                            this.mediaRecorder.start(1000);
                            this.isRecording = true;
                            this.updateMicButton();
                        })
                        .catch(err => {
                            console.error('Microphone access denied:', err);
                        });
                },
                stopRecording() {
                    if (!this.isRecording || !this.mediaRecorder) {
                        return;
                    }

                    this.mediaRecorder.stop();
                    this.isRecording = false;
                    this.updateMicButton();
                },
                toggleRecording() {
                    if (this.isRecording) {
                        this.stopRecording();
                    } else {
                        this.startRecording();
                    }
                },
                updateMicButton() {
                    if (!this.micButton) {
                        return;
                    }

                    if (this.isRecording) {
                        this.micButton.classList.add('stt-recording');
                        this.micButton.innerHTML = '<i class="fa-solid fa-stop"></i>';
                    } else {
                        this.micButton.classList.remove('stt-recording');
                        this.micButton.innerHTML = '<i class="fa-solid fa-microphone"></i>';
                    }
                }
            },
            watch: {
                isPlayingAudio() {
                    // Reserved for future use — volume-based interrupt detection
                    // no longer mutes tracks; browser echo cancellation handles echo.
                },
                isConversationMode(active) {
                    if (this.micButton) {
                        this.micButton.style.display = active ? 'none' : (this.speechToTextEnabled ? '' : 'none');
                    }

                    if (this.buttonElement) {
                        this.buttonElement.style.display = active ? 'none' : '';
                    }

                    if (this.inputElement) {
                        this.inputElement.disabled = active;
                        if (active) {
                            this.inputElement.placeholder = '';
                        }
                    }
                }
            },
            mounted() {
                (async () => {
                    await this.startConnection();
                    this.initializeApp();
                })();

                window.addEventListener('beforeunload', this.handleBeforeUnload);
            },
            beforeUnmount() {
                window.removeEventListener('beforeunload', this.handleBeforeUnload);

                if (this.stream) {
                    this.stream.dispose();
                    this.stream = null;
                }
                if (this.connection) {
                    this.connection.stop();
                }
            },
            template: config.messageTemplate
        }).mount(config.appElementSelector);

        return app;
    };

    return {
        initialize: initialize
    };
}();

// Global function for downloading charts as images
window.downloadChart = function (chartId) {
    const canvas = document.getElementById(chartId);
    if (!canvas) {
        console.error('Chart canvas not found:', chartId);
        return;
    }

    // Create a temporary link element
    const link = document.createElement('a');
    link.download = 'chart-' + chartId + '.png';
    link.href = canvas.toDataURL('image/png');
    link.click();
};

// Intercept download clicks for data-URI images and convert to blob downloads.
document.addEventListener('click', function (e) {
    const link = e.target.closest('.ai-download-image');
    if (!link) {
        return;
    }

    const container = link.closest('.generated-image-container');
    const img = container?.querySelector('img');
    if (!img) {
        return;
    }

    const src = img.src;
    if (!src || !src.startsWith('data:')) {
        return; // Normal URL – let the default <a> behaviour handle it.
    }

    e.preventDefault();

    fetch(src)
        .then(function (res) { return res.blob(); })
        .then(function (blob) {
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = link.getAttribute('download') || 'generated-image.png';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            setTimeout(function () { URL.revokeObjectURL(url); }, 100);
        })
        .catch(function (err) { console.error('Failed to download image:', err); });
});
