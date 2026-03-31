window.openAIChatManager = function () {

    // Defaults (can be overridden by instanceConfig)
    var defaultConfig = {
        // UI defaults for generated media
        generatedImageAltText: 'Generated Image',
        generatedImageMaxWidth: 400,
        generatedChartMaxWidth: 900,
        downloadImageTitle: 'Download image',
        downloadChartTitle: 'Download chart as image',
        downloadChartButtonText: 'Download',
        userLabel: 'You',
        assistantLabel: 'Assistant',
        thumbsUpTitle: 'Thumbs up',
        thumbsDownTitle: 'Thumbs down',
        copyTitle: 'Click here to copy response to clipboard.',
        codeCopiedText: 'Copied!',
        messageTemplate: `
        <div class="ai-chat-messages">
            <div v-for="(message, index) in messages" :key="'msg-' + index" class="ai-chat-message-item">
                <div>
                    <div v-if="message.role === 'user'" class="ai-chat-msg-role ai-chat-msg-role-user">{{ userLabel }}</div>
                    <div v-else-if="message.role !== 'indicator'" :class="getAssistantRoleClasses(message)">
                        <span :class="getAssistantIconClasses(message, index)"><i :class="getAssistantIcon(message)"></i></span>
                        {{ getAssistantLabel(message) }}
                    </div>
                    <div class="lh-base">
                        <h4 v-if="message.title">{{ message.title }}</h4>
                        <div v-html="message.htmlContent"></div>
                        <span class="message-buttons-container" v-if="!isIndicator(message)">
                            <template v-if="metricsEnabled && message.role === 'assistant'">
                                <span class="ai-chat-message-assistant-feedback" :data-message-id="message.id">
                                    <button class="btn btn-sm btn-link text-success p-0 me-2 button-message-toolbox rate-up-btn" @click="rateMessage(message, true, $event)" :title="thumbsUpTitle">
                                        <i class="fa-regular fa-thumbs-up"></i>
                                    </button>
                                    <button class="btn btn-sm btn-link text-danger p-0 me-2 button-message-toolbox rate-down-btn" @click="rateMessage(message, false, $event)" :title="thumbsDownTitle">
                                        <i class="fa-regular fa-thumbs-down"></i>
                                    </button>
                                </span>
                            </template>
                            <button class="btn btn-sm btn-link text-secondary p-0 button-message-toolbox" @click="copyResponse(message.content)" :title="copyTitle">
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
    `
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
                    isSessionStarted: false,
                    isPlaceholderVisible: true,
                    chatWidgetStateName: null,
                    chatWidgetStateSession: null,
                    chatHistorySection: null,
                    widgetIsInitialized: false,
                    isStreaming: false,
                    isNavigatingAway: false,
                    autoScroll: true,
                    stream: null,
                    messages: [],
                    notifications: [],
                    prompt: '',
                    documents: config.existingDocuments || [],
                    isUploading: false,
                    uploadErrors: [],
                    isDragOver: false,
                    documentBar: null,
                    metricsEnabled: !!config.metricsEnabled,
                    userLabel: config.userLabel,
                    assistantLabel: config.assistantLabel,
                    thumbsUpTitle: config.thumbsUpTitle,
                    thumbsDownTitle: config.thumbsDownTitle,
                    copyTitle: config.copyTitle,
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
                    ttsButton: null,
                    conversationModeEnabled: config.chatMode === 'Conversation',
                    conversationButton: null,
                    isConversationMode: false,
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
                handleDragOver(e) {
                    if (!config.sessionDocumentsEnabled) return;
                    e.preventDefault();
                    e.stopPropagation();
                    this.isDragOver = true;
                    var inputArea = this.inputElement ? this.inputElement.closest('.ai-admin-widget-input, .text-bg-light') : null;
                    if (inputArea) inputArea.classList.add('ai-chat-drag-over');
                },
                handleDragLeave(e) {
                    if (!config.sessionDocumentsEnabled) return;
                    e.preventDefault();
                    e.stopPropagation();
                    this.isDragOver = false;
                    var inputArea = this.inputElement ? this.inputElement.closest('.ai-admin-widget-input, .text-bg-light') : null;
                    if (inputArea) inputArea.classList.remove('ai-chat-drag-over');
                },
                handleDrop(e) {
                    if (!config.sessionDocumentsEnabled) return;
                    e.preventDefault();
                    e.stopPropagation();
                    this.isDragOver = false;
                    var inputArea = this.inputElement ? this.inputElement.closest('.ai-admin-widget-input, .text-bg-light') : null;
                    if (inputArea) inputArea.classList.remove('ai-chat-drag-over');
                    if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                        this.uploadFiles(e.dataTransfer.files);
                    }
                },
                triggerFileInput() {
                    if (!config.sessionDocumentsEnabled) return;
                    var fileInput = document.getElementById('ai-chat-doc-input');
                    if (fileInput) fileInput.click();
                },
                handleFileInputChange(e) {
                    var files = e.target.files;
                    if (files && files.length > 0) {
                        this.uploadFiles(files);
                    }
                    e.target.value = '';
                },
                async uploadFiles(files) {
                    if (!config.uploadDocumentUrl) return;

                    var sessionId = this.getSessionId();
                    var profileId = this.getProfileId();

                    if (!sessionId && !profileId) {
                        console.warn('Cannot upload documents without a session or profile.');
                        return;
                    }

                    this.isUploading = true;
                    this.uploadErrors = [];
                    this.renderDocumentBar();
                    try {
                        var formData = new FormData();
                        if (sessionId) {
                            formData.append('sessionId', sessionId);
                        } else {
                            formData.append('profileId', profileId);
                        }
                        for (var i = 0; i < files.length; i++) {
                            formData.append('files', files[i]);
                        }

                        var response = await fetch(config.uploadDocumentUrl, {
                            method: 'POST',
                            body: formData
                        });

                        if (!response.ok) {
                            var errorText = await response.text();
                            console.error('Upload failed:', errorText);
                            this.uploadErrors = [{ fileName: '', error: 'Upload failed. Please try again.' }];
                            return;
                        }

                        var result = await response.json();

                        // If the server created a new session, initialize it.
                        if (result.sessionId && !sessionId) {
                            this.initializeSession(result.sessionId);
                        }

                        if (result.uploaded && result.uploaded.length > 0) {
                            for (var j = 0; j < result.uploaded.length; j++) {
                                this.documents.push(result.uploaded[j]);
                            }
                        }
                        if (result.failed && result.failed.length > 0) {
                            this.uploadErrors = result.failed;
                        }
                    } catch (err) {
                        console.error('Upload error:', err);
                        this.uploadErrors = [{ fileName: '', error: 'Upload failed. Please try again.' }];
                    } finally {
                        this.isUploading = false;
                        this.renderDocumentBar();
                    }
                },
                async removeDocument(doc) {
                    if (!config.removeDocumentUrl) return;

                    try {
                        var sessionId = this.getSessionId();
                        var response = await fetch(config.removeDocumentUrl, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ itemId: sessionId, documentId: doc.documentId })
                        });

                        if (response.ok) {
                            var idx = this.documents.indexOf(doc);
                            if (idx > -1) this.documents.splice(idx, 1);
                        } else {
                            var errorText = await response.text();
                            console.error('Failed to remove document:', response.status, errorText);
                        }
                    } catch (err) {
                        console.error('Remove document error:', err);
                    }
                },
                formatFileSize(bytes) {
                    if (bytes < 1024) return bytes + ' B';
                    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
                    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
                },
                renderDocumentBar() {
                    if (!this.documentBar) return;

                    if (!config.sessionDocumentsEnabled) {
                        this.documentBar.classList.add('d-none');
                        return;
                    }

                    this.documentBar.classList.remove('d-none');

                    var html = '<div class="ai-chat-doc-bar d-flex flex-wrap align-items-center gap-1 p-2">';

                    for (var i = 0; i < this.documents.length; i++) {
                        var doc = this.documents[i];
                        var name = doc.fileName || 'Document';
                        if (name.length > 20) name = name.substring(0, 17) + '...';
                        html += '<span class="badge bg-secondary bg-opacity-25 text-dark d-inline-flex align-items-center gap-1 px-2 py-1" style="font-size: 0.8rem;" title="' + this.escapeHtml(doc.fileName || '') + '">';
                        html += '<i class="fa-solid fa-file-lines" style="font-size: 0.7rem;"></i> ';
                        html += this.escapeHtml(name);
                        html += ' <button type="button" class="btn-close btn-close-sm ms-1" style="font-size: 0.5rem;" data-doc-index="' + i + '" aria-label="Remove"></button>';
                        html += '</span>';
                    }

                    for (var m = 0; m < this.uploadErrors.length; m++) {
                        var failedItem = this.uploadErrors[m];
                        var failedName = failedItem.fileName || 'File';
                        var errorMsg = failedItem.error || 'Upload failed';
                        if (failedName.length > 15) failedName = failedName.substring(0, 12) + '...';
                        html += '<span class="badge bg-danger bg-opacity-25 text-danger d-inline-flex align-items-center gap-1 px-2 py-1" style="font-size: 0.8rem;" title="' + this.escapeHtml((failedItem.fileName || '') + ': ' + errorMsg) + '">';
                        html += '<i class="fa-solid fa-circle-exclamation" style="font-size: 0.7rem;"></i> ';
                        html += this.escapeHtml(failedName);
                        html += ' <button type="button" class="btn-close btn-close-sm ms-1" style="font-size: 0.5rem;" data-error-index="' + m + '" aria-label="Dismiss"></button>';
                        html += '</span>';
                    }

                    if (this.isUploading) {
                        html += '<span class="badge bg-info bg-opacity-25 text-dark d-inline-flex align-items-center gap-1 px-2 py-1" style="font-size: 0.8rem;">';
                        html += '<span class="spinner-border spinner-border-sm" style="width: 0.7rem; height: 0.7rem;"></span> Uploading...';
                        html += '</span>';
                    }

                    html += '<button type="button" class="btn btn-sm btn-outline-secondary rounded-pill ai-chat-doc-add-btn d-inline-flex align-items-center gap-1" style="font-size: 0.75rem; padding: 0.15rem 0.5rem;" title="Attach documents">';
                    html += '<i class="fa-solid fa-paperclip"></i>';
                    if (this.documents.length === 0 && !this.isUploading) {
                        html += ' <span>Attach files</span>';
                    }
                    html += '</button>';

                    html += '</div>';

                    this.documentBar.replaceChildren(DOMPurify.sanitize(html, { RETURN_DOM_FRAGMENT: true }));

                    // Bind remove handlers
                    var self = this;
                    var closeButtons = this.documentBar.querySelectorAll('[data-doc-index]');
                    for (var j = 0; j < closeButtons.length; j++) {
                        closeButtons[j].addEventListener('click', (function (idx) {
                            return function (e) {
                                e.preventDefault();
                                e.stopPropagation();
                                var docToRemove = self.documents[idx];
                                if (docToRemove) self.removeDocument(docToRemove);
                            };
                        })(parseInt(closeButtons[j].getAttribute('data-doc-index'))));
                    }

                    // Bind error dismiss handlers
                    var errorCloseButtons = this.documentBar.querySelectorAll('[data-error-index]');
                    for (var n = 0; n < errorCloseButtons.length; n++) {
                        errorCloseButtons[n].addEventListener('click', (function (idx) {
                            return function (e) {
                                e.preventDefault();
                                e.stopPropagation();
                                self.uploadErrors.splice(idx, 1);
                                self.renderDocumentBar();
                            };
                        })(parseInt(errorCloseButtons[n].getAttribute('data-error-index'))));
                    }

                    // Bind add button
                    var addBtn = this.documentBar.querySelector('.ai-chat-doc-add-btn');
                    if (addBtn) {
                        addBtn.addEventListener('click', function (e) {
                            e.preventDefault();
                            self.triggerFileInput();
                        });
                    }
                },
                escapeHtml(text) {
                    var div = document.createElement('div');
                    div.textContent = text;
                    return div.innerHTML;
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
                    return appearance && appearance.label ? appearance.label : this.assistantLabel;
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
                async startConnection() {
                    this.connection = new signalR.HubConnectionBuilder()
                        .withUrl(config.signalRHubUrl)
                        .withAutomaticReconnect()
                        .build();

                    // Allow long-running operations (e.g., multi-step MCP tool calls)
                    // without the client disconnecting prematurely.
                    this.connection.serverTimeoutInMilliseconds = 600000;
                    this.connection.keepAliveIntervalInMilliseconds = 15000;

                    this.connection.on("LoadSession", (data) => {
                        this.initializeSession(data.sessionId, true);
                        this.messages = [];
                        this.documents = data.documents || [];

                        (data.messages ?? []).forEach(msg => {
                            this.addMessage(msg);

                            this.$nextTick(() => {
                                renderChartsInMessage(msg);
                            });
                        });

                        // Update feedback icons in the DOM after all messages have rendered.
                        this.$nextTick(() => {
                            this.refreshAllFeedbackIcons();
                        });

                        // When the session is new (no messages) and an initial prompt is configured,
                        // automatically send it as the first user message to trigger an AI response.
                        if (this.messages.length === 0 && config.initialPrompt) {
                            this.prompt = config.initialPrompt;
                            this.sendMessage();
                        }
                    });

                    this.connection.on("ReceiveError", (error) => {
                        console.error("SignalR Error: ", error);

                        if (this.isRecording) {
                            this.stopRecording();
                        }

                        // If this is a widget with a stale cached session (e.g., profile was deleted),
                        // clear the cached session and start fresh with the current profile.
                        if (this.widgetIsInitialized && !this.isSessionStarted && !this._attemptedSessionRecovery) {
                            this._attemptedSessionRecovery = true;
                            localStorage.removeItem(this.chatWidgetStateSession);
                            this.startNewSession();
                        }
                    });

                    this.connection.on("MessageRated", (messageId, userRating) => {
                        var msg = this.messages.find(m => m.id === messageId);
                        if (msg) {
                            msg.userRating = userRating;
                        }
                    });

                    this.connection.on("ReceiveTranscript", (sessionId, text, isFinal) => {
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

                    this.connection.on("ReceiveConversationUserMessage", (sessionId, text) => {
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

                    this.connection.on("ReceiveConversationAssistantToken", (sessionId, messageId, token, responseId, appearance) => {
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
                                userRating: null,
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

                    this.connection.on("ReceiveConversationAssistantComplete", (sessionId, messageId) => {
                        if (this._conversationAssistantMessage) {
                            var msg = this.messages[this._conversationAssistantMessage.index];
                            if (msg) {
                                msg.isStreaming = false;
                            }
                            this._conversationAssistantMessage = null;
                        }
                    });

                    this.connection.on("ReceiveAudioChunk", (sessionId, base64Audio, contentType) => {
                        if (base64Audio) {
                            const binaryString = atob(base64Audio);
                            const bytes = new Uint8Array(binaryString.length);
                            for (let i = 0; i < binaryString.length; i++) {
                                bytes[i] = binaryString.charCodeAt(i);
                            }
                            this.audioChunks.push(bytes);
                        }
                    });

                    this.connection.on("ReceiveAudioComplete", (sessionId) => {
                        this.playCollectedAudio();
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

                        if (this.isSessionStarted) {
                            this.reloadCurrentSession();
                        } else if (config.autoCreateSession) {
                            this.startNewSession();
                        }
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
                    this.fireEvent(new CustomEvent("addingOpenAIPromotMessage", { detail: { message: message } }));
                    this.messages.push(message);

                    this.$nextTick(() => {
                        this.fireEvent(new CustomEvent("addedOpenAIPromotMessage", { detail: { message: message } }));
                    });
                },
                addMessage(message) {

                    // Ensure userRating is always defined for Vue reactivity.
                    if (message.userRating === undefined) {
                        message.userRating = null;
                    }

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

                                // if we have multiple references, add a comma to ensure we don't concatenate numbers.
                                processedContent = processedContent.replaceAll('</strong></sup><sup>', '</strong></sup><sup>,</sup><sup>');

                                processedContent += '<br><br>';

                                for (const [key, value] of citedRefs) {
                                    const label = value.text || key;
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
                addMessages(messages) {

                    for (let i = 0; i < messages.length; i++) {
                        this.addMessageInternal(messages[i]);
                    }

                    this.hidePlaceholder();
                    this.$nextTick(() => {
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
                isOrchestratorAvailable() {
                    return config.isOrchestratorAvailable !== false;
                },
                applyOrchestratorAvailability() {
                    if (this.isOrchestratorAvailable()) {
                        return true;
                    }

                    const unavailableMessage = config.orchestratorUnavailableMessage || "This orchestrator is not currently available.";

                    if (this.inputElement) {
                        this.inputElement.disabled = true;
                        this.inputElement.value = '';
                        this.inputElement.placeholder = unavailableMessage;
                    }

                    if (this.buttonElement) {
                        this.buttonElement.disabled = true;
                    }

                    if (this.micButtonElement) {
                        this.micButtonElement.disabled = true;
                        this.micButtonElement.style.display = 'none';
                    }

                    if (this.conversationButtonElement) {
                        this.conversationButtonElement.disabled = true;
                        this.conversationButtonElement.style.display = 'none';
                    }

                    return false;
                },
                fireEvent(event) {
                    document.dispatchEvent(event);
                },
                isIndicator(message) {
                    return message.role === 'indicator';
                },
                sendMessage() {
                    if (!this.applyOrchestratorAvailability()) {
                        return;
                    }

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

                    this.addMessage({
                        role: 'user',
                        content: trimmedPrompt
                    });

                    this.streamMessage(this.getProfileId(), trimmedPrompt, null);
                    this.inputElement.value = '';
                    this.prompt = '';
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
                            var profileId = this.getProfileId();
                            var sessionId = this.getSessionId() || '';
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
                            this.connection.send("SendAudioStream", profileId, sessionId, subject, mimeType, language);
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
                },
                streamMessage(profileId, trimmedPrompt, sessionProfileId) {

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

                    // Get the index after showing typing indicator.
                    var messageIndex = this.messages.length;
                    var currentSessionId = this.getSessionId();

                    this.stream = this.connection.stream("SendMessage", profileId, trimmedPrompt, currentSessionId, sessionProfileId)
                        .subscribe({
                            next: (chunk) => {
                                let message = this.messages[messageIndex];

                                if (!message) {

                                    if (chunk.sessionId && !currentSessionId) {
                                        this.initializeSession(chunk.sessionId);
                                    }

                                    this.hideTypingIndicator();
                                    // Re-assign the index after hiding the typing indicator.
                                    messageIndex = this.messages.length;
                                    let newMessage = {
                                        id: chunk.messageId,
                                        role: "assistant",
                                        title: chunk.title,
                                        content: "",
                                        htmlContent: "",
                                        isStreaming: true,
                                        userRating: null,
                                    };

                                    this.messages.push(newMessage);

                                    message = newMessage;
                                }

                                if (chunk.title && (!message.title || message.title !== chunk.title)) {
                                    message.title = chunk.title;
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

                                    // Append processed content to the message.
                                    // if we have multiple references, add a comma to ensure we don't concatenate numbers.
                                    content += processedContent.replaceAll('</strong></sup><sup>', '</strong></sup><sup>,</sup><sup>');
                                }

                                // Update the existing message
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
                    let newMessage = {
                        role: "assistant",
                        content: "Our service is currently unavailable. Please try again later. We apologize for the inconvenience.",
                        htmlContent: "",
                    };

                    return newMessage;
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
                            const label = value.text || key;
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

                    if (this.inputElement) {
                        this.inputElement.setAttribute('disabled', 'disabled');
                    }
                },
                streamingFinished() {
                    var startIcon = this.buttonElement.getAttribute('data-start-icon');

                    if (startIcon) {
                        this.buttonElement.replaceChildren(DOMPurify.sanitize(startIcon, { RETURN_DOM_FRAGMENT: true }));
                    }

                    if (this.inputElement) {
                        this.inputElement.removeAttribute('disabled');
                        this.inputElement.focus();
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
                },
                synthesizeSpeech(text) {
                    if (!this.textToSpeechEnabled || !text || !this.connection) {
                        return;
                    }

                    this.audioChunks = [];
                    this.isPlayingAudio = true;

                    this.connection.invoke("SynthesizeSpeech", this.getProfileId(), this.getSessionId(), text, this.ttsVoiceName)
                        .catch(err => {
                            console.error("TTS synthesis error:", err);
                            this.isPlayingAudio = false;
                        });
                },
                playCollectedAudio() {
                    if (this.audioChunks.length === 0) {
                        if (!this.currentAudioElement && this.audioPlayQueue.length === 0) {
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

                    // If audio is already playing, queue this blob for sequential playback.
                    if (this.currentAudioElement) {
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
                        var nextBlob = this.audioPlayQueue.shift();
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
                            // During TTS playback, detect when the user speaks above
                            // the threshold to stop TTS (interrupt). Audio chunks are
                            // always forwarded — browser echo cancellation handles
                            // speaker echo so the STT stream has no gaps.
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

                            var profileId = this.getProfileId();
                            var sessionId = this.getSessionId() || '';
                            var language = document.documentElement.lang || 'en-US';

                            this.connection.send("StartConversation", profileId, sessionId, this._conversationSubject, mimeType, language);
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
                    // Legacy: in conversation mode, audio playback continuation
                    // is handled by the persistent stream. This method is only
                    // called from playCollectedAudio for non-conversation TTS.
                },
                generatePrompt(element) {
                    if (!element) {
                        console.error('The element paramter is required.');

                        return;
                    }

                    let templateProfileId = element.getAttribute('data-profile-id');
                    let sessionId = this.getSessionId();
                    let sessionProfileId = this.getProfileId();

                    if (!templateProfileId || !sessionId) {

                        console.error('The given element is missing data-profile-id or the session has not yet started.');
                        return;
                    }

                    // streamMessage() already shows the typing indicator.
                    this.streamMessage(templateProfileId, null, sessionProfileId);
                },
                createSessionUrl(baseUrl, param, value) {

                    const fullUrl = baseUrl.toLowerCase().startsWith('http') ? baseUrl : window.location.origin + baseUrl;
                    const url = new URL(fullUrl);

                    url.searchParams.set(param, value);

                    return url.toString();
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
                receiveNotification(notification) {
                    if (!notification || !notification.type) {
                        return;
                    }
                    var existingIndex = this.notifications.findIndex(n => n.type === notification.type);
                    if (existingIndex >= 0) {
                        this.notifications.splice(existingIndex, 1, notification);
                    } else {
                        this.notifications.push(notification);
                    }
                    this.$nextTick(() => {
                        this.scrollToBottom();
                    });
                },
                updateNotification(notification) {
                    if (!notification || !notification.type) {
                        return;
                    }
                    var existingIndex = this.notifications.findIndex(n => n.type === notification.type);
                    if (existingIndex >= 0) {
                        this.notifications.splice(existingIndex, 1, notification);
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
                    var sessionId = this.getSessionId();
                    this.connection.invoke("HandleNotificationAction", sessionId, notificationType, actionName).catch(function (err) {
                        console.error("Error handling notification action:", err);
                    });
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
                getProfileId() {
                    return this.inputElement.getAttribute('data-profile-id');
                },
                setSessionId(sessionId) {
                    this.inputElement.setAttribute('data-session-id', sessionId || '');
                },
                resetSession() {
                    this.stopRecording();
                    this.setSessionId('');
                    this.isSessionStarted = false;
                    this.sessionRating = null;
                    if (this.widgetIsInitialized) {
                        localStorage.removeItem(this.chatWidgetStateSession);
                    }
                    this.messages = [];
                    this.documents = [];
                    if (!config.autoCreateSession) {
                        this.showPlaceholder();
                    }

                    if (config.autoCreateSession) {
                        this.startNewSession();
                    }
                },
                startNewSession() {
                    if (!this.applyOrchestratorAvailability()) {
                        return;
                    }

                    const profileId = this.getProfileId();
                    if (!profileId || !this.connection) {
                        return;
                    }

                    this.connection.invoke("StartSession", profileId, null).catch(err => console.error(err));
                },
                initializeApp() {
                    this.inputElement = document.querySelector(config.inputElementSelector);
                    this.buttonElement = document.querySelector(config.sendButtonElementSelector);
                    this.chatContainer = document.querySelector(config.chatContainerElementSelector);
                    this.placeholder = document.querySelector(config.placeholderElementSelector);
                    this.applyOrchestratorAvailability();

                    const sessionId = this.getSessionId();
                    if (!config.widget && sessionId) {
                        this.loadSession(sessionId);
                    } else if (this.isOrchestratorAvailable() && config.autoCreateSession && !config.widget && !sessionId) {
                        this.startNewSession();
                    }

                    // Initialize document bar if enabled.
                    if (config.sessionDocumentsEnabled && config.documentBarSelector) {
                        this.documentBar = document.querySelector(config.documentBarSelector);
                        if (this.documentBar) {
                            this.renderDocumentBar();

                            // Create hidden file input for document uploads.
                            var fileInput = document.createElement('input');
                            fileInput.type = 'file';
                            fileInput.id = 'ai-chat-doc-input';
                            fileInput.className = 'd-none';
                            fileInput.multiple = true;
                            if (config.allowedExtensions) {
                                fileInput.accept = config.allowedExtensions;
                            }
                            fileInput.addEventListener('change', (e) => this.handleFileInputChange(e));
                            this.documentBar.parentElement.appendChild(fileInput);

                            // Set up drag-and-drop on the input area.
                            var inputArea = this.inputElement ? this.inputElement.closest('.ai-admin-widget-input, .text-bg-light') : null;
                            if (inputArea) {
                                inputArea.addEventListener('dragover', (e) => this.handleDragOver(e));
                                inputArea.addEventListener('dragleave', (e) => this.handleDragLeave(e));
                                inputArea.addEventListener('drop', (e) => this.handleDrop(e));
                            }
                        }
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

                    this.inputElement.addEventListener('keydown', event => {

                        if (this.stream != null) {
                            return;
                        }

                        if (event.key === "Enter" && !event.shiftKey) {
                            event.preventDefault();
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

                    const promptGenerators = document.getElementsByClassName('profile-generated-prompt');

                    for (var i = 0; i < promptGenerators.length; i++) {
                        promptGenerators[i].addEventListener('click', (e) => {
                            e.preventDefault();
                            this.generatePrompt(e.target);
                        });
                    }

                    const chatSessions = document.getElementsByClassName('chat-session-history-item');

                    for (var i = 0; i < chatSessions.length; i++) {
                        chatSessions[i].addEventListener('click', (e) => {
                            e.preventDefault();

                            var sessionId = e.target.getAttribute('data-session-id');

                            if (!sessionId) {
                                console.error('an element with the class chat-session-history-item with no data-session-id set.');

                                return;
                            }

                            this.loadSession(sessionId);
                            this.showChatScreen();
                        });
                    }

                    for (let i = 0; i < config.messages.length; i++) {
                        this.addMessage(config.messages[i]);
                    }

                    // Update feedback icons in the DOM after initial messages have rendered.
                    this.$nextTick(() => {
                        this.refreshAllFeedbackIcons();
                    });

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
                loadSession(sessionId) {
                    this.connection.invoke("LoadSession", sessionId).catch(err => console.error(err));
                },
                reloadCurrentSession() {

                    var sessionId = this.getSessionId();
                    if (sessionId) {
                        this.loadSession(sessionId);
                    }
                },
                initializeSession(sessionId, force) {
                    if (this.isSessionStarted && !force) {
                        return
                    }
                    this.fireEvent(new CustomEvent("initializingSessionOpenAIChat", { detail: { sessionId: sessionId } }))
                    this.setSessionId(sessionId);
                    this.isSessionStarted = true;

                    if (this.widgetIsInitialized) {
                        localStorage.setItem(this.chatWidgetStateSession, sessionId);
                    }
                },
                initializeWidget() {

                    if (!config.widget.chatWidgetContainer) {
                        console.error('The widget chatWidgetContainer is required.');
                        return;
                    }

                    if (!config.widget.chatWidgetStateName) {
                        console.error('The widget chatWidgetStateName is required.');
                        return;
                    }

                    const chatWidgetContainer = document.querySelector(config.widget.chatWidgetContainer);

                    if (!chatWidgetContainer) {
                        return;
                    }

                    if (config.widget.chatHistorySection) {
                        this.chatHistorySection = document.querySelector(config.widget.chatHistorySection);
                    }

                    this.chatWidgetStateName = config.widget.chatWidgetStateName;
                    this.chatWidgetStateSession = config.widget.chatWidgetStateName + 'Session';
                    this.widgetIsInitialized = true;

                    // Auto-load the last session so the user always sees previous chat history.
                    this.reloadCurrentSession();

                    if (config.autoCreateSession && !this.getSessionId()) {
                        this.startNewSession();
                    }

                    if (config.widget.showHistoryButton && this.chatHistorySection) {

                        const showHistoryButton = document.querySelector(config.widget.showHistoryButton);

                        if (showHistoryButton) {
                            showHistoryButton.addEventListener('click', () => {
                                this.chatHistorySection.classList.toggle('show');
                            });
                        }

                        if (config.widget.closeHistoryButton) {
                            var closeHistoryButton = document.querySelector(config.widget.closeHistoryButton);

                            if (closeHistoryButton) {
                                closeHistoryButton.addEventListener('click', () => {
                                    this.showChatScreen();
                                });
                            }
                        }
                    }

                    if (config.widget.newChatButton) {
                        const newChatButton = document.querySelector(config.widget.newChatButton);

                        if (newChatButton) {
                            newChatButton.addEventListener('click', () => {
                                this.resetSession();
                                this.showChatScreen();
                            });
                        }
                    }
                },
                showChatScreen() {

                    if (!this.chatHistorySection) {
                        return;
                    }

                    this.chatHistorySection.classList.remove('show');
                },
                getSessionId() {
                    let sessionId = this.inputElement.getAttribute('data-session-id');

                    if (!sessionId && this.widgetIsInitialized) {
                        sessionId = localStorage.getItem(this.chatWidgetStateSession);
                    }

                    return sessionId;
                },
                copyResponse(message) {
                    navigator.clipboard.writeText(message);
                },
                updateFeedbackIcons(container, userRating) {
                    if (!container) {
                        return;
                    }

                    var upBtn = container.querySelector('.rate-up-btn');
                    var downBtn = container.querySelector('.rate-down-btn');

                    // Font Awesome SVG+JS replaces <i> with <svg>, so we must replace
                    // the entire button content and let Font Awesome re-process.
                    if (upBtn) {
                        var upClass = userRating === true ? 'fa-solid fa-thumbs-up' : 'fa-regular fa-thumbs-up';
                        var upIcon = document.createElement('i');
                        upIcon.className = upClass;
                        upIcon.style.fontSize = '0.9rem';
                        upBtn.textContent = '';
                        upBtn.appendChild(upIcon);
                    }

                    if (downBtn) {
                        var downClass = userRating === false ? 'fa-solid fa-thumbs-down' : 'fa-regular fa-thumbs-down';
                        var downIcon = document.createElement('i');
                        downIcon.className = downClass;
                        downIcon.style.fontSize = '0.9rem';
                        downBtn.textContent = '';
                        downBtn.appendChild(downIcon);
                    }

                    // Trigger Font Awesome SVG+JS to convert the new <i> elements.
                    if (window.FontAwesome && FontAwesome.dom && FontAwesome.dom.i2svg) {
                        FontAwesome.dom.i2svg({ node: container });
                    }
                },
                refreshAllFeedbackIcons() {
                    var containers = this.$el.querySelectorAll('.ai-chat-message-assistant-feedback');

                    for (var i = 0; i < containers.length; i++) {
                        var msgId = containers[i].getAttribute('data-message-id');
                        var msg = this.messages.find(m => m.id === msgId);

                        if (msg) {
                            this.updateFeedbackIcons(containers[i], msg.userRating);
                        }
                    }
                },
                rateMessage(message, isPositive, event) {
                    var sessionId = this.getSessionId();

                    if (!sessionId || !message.id || !this.connection) {
                        return;
                    }

                    // Toggle: clicking the same rating again clears it.
                    var newRating = message.userRating === isPositive ? null : isPositive;
                    message.userRating = newRating;

                    // Find the feedback container by message ID for reliable DOM targeting.
                    var feedbackContainer = this.$el.querySelector(
                        '.ai-chat-message-assistant-feedback[data-message-id="' + message.id + '"]'
                    );

                    this.updateFeedbackIcons(feedbackContainer, newRating);

                    // Trigger spark animation after Font Awesome has re-processed icons.
                    if (newRating !== null && feedbackContainer) {
                        setTimeout(() => {
                            var btnClass = isPositive ? '.rate-up-btn' : '.rate-down-btn';
                            var btn = feedbackContainer.querySelector(btnClass);

                            if (btn) {
                                btn.classList.remove('spark-effect');
                                void btn.offsetWidth;
                                btn.classList.add('spark-effect');
                                btn.addEventListener('animationend', function onEnd() {
                                    btn.removeEventListener('animationend', onEnd);
                                    btn.classList.remove('spark-effect');
                                });
                            }
                        }, 50);
                    }

                    this.connection.invoke("RateMessage", sessionId, message.id, isPositive).catch(function (err) {
                        console.error('Failed to rate message:', err);
                    });
                }
            },
            watch: {
                documents: {
                    handler() { this.renderDocumentBar(); },
                    deep: true
                },
                isUploading() { this.renderDocumentBar(); },
                isPlayingAudio() {
                    // Reserved for future use — volume-based interrupt detection
                    // no longer mutes tracks; browser echo cancellation handles echo.
                },
                isConversationMode(active) {
                    // Hide/show mic button.
                    if (this.micButton) {
                        this.micButton.style.display = active ? 'none' : (this.speechToTextEnabled ? '' : 'none');
                    }

                    // Hide/show send button.
                    if (this.buttonElement) {
                        this.buttonElement.style.display = active ? 'none' : '';
                    }

                    // Disable/enable textarea.
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
                    if (config.widget) {
                        this.initializeWidget();
                    }
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
