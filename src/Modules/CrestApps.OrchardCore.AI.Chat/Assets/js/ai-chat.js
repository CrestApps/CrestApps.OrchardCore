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
        messageTemplate: `
        <div class="ai-chat-messages">
            <div v-for="(message, index) in messages" :key="index" class="ai-chat-message-item">
                <div>
                    <div v-if="message.role === 'user'" class="ai-chat-msg-role ai-chat-msg-role-user">{{ userLabel }}</div>
                    <div v-else-if="message.role !== 'indicator'" class="ai-chat-msg-role ai-chat-msg-role-assistant">
                        <i :class="'fa fa-robot' + (message.isStreaming ? ' ai-streaming-icon' : ' ai-bot-icon')"></i>
                        {{ assistantLabel }}
                    </div>
                    <div class="lh-base">
                        <h4 v-if="message.title">{{ message.title }}</h4>
                        <div v-html="message.htmlContent || message.content"></div>
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
        </div>
    `,
        indicatorTemplate: `
        <div class="ai-chat-msg-role ai-chat-msg-role-assistant">
            <i class="fa fa-robot ai-streaming-icon" style="display: inline-block;"></i>
            Assistant
        </div>
    `
    };

    const renderer = new marked.Renderer();

    // Modify the link rendering to open in a new tab
    renderer.link = function (data) {
        return `<a href="${data.href}" target="_blank" rel="noopener noreferrer">${data.text}</a>`;
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
            highlighted = code.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        }

        var langLabel = lang ? ` data-lang="${lang}"` : '';
        return `<pre${langLabel}><button type="button" class="ai-code-copy-btn" title="Copy code"><i class="fa-solid fa-copy"></i></button><code class="hljs${lang ? ' language-' + lang : ''}">${highlighted}</code></pre>`;
    };

    // Custom image renderer for generated images with thumbnail styling and download button.
    // Handles both URL and data-URI sources (data URIs are converted to blobs for download).
    renderer.image = function (data) {
        const src = data.href;
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
        return html;
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
                    isSteaming: false,
                    isNavigatingAway: false,
                    autoScroll: true,
                    stream: null,
                    messages: [],
                    prompt: '',
                    documents: config.existingDocuments || [],
                    isUploading: false,
                    isDragOver: false,
                    documentBar: null,
                    metricsEnabled: !!config.metricsEnabled,
                    userLabel: config.userLabel,
                    assistantLabel: config.assistantLabel,
                    thumbsUpTitle: config.thumbsUpTitle,
                    thumbsDownTitle: config.thumbsDownTitle,
                    copyTitle: config.copyTitle,
                };
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
                            for (var k = 0; k < result.failed.length; k++) {
                                console.warn('File failed to upload:', result.failed[k].fileName, result.failed[k].error);
                            }
                        }
                    } catch (err) {
                        console.error('Upload error:', err);
                    } finally {
                        this.isUploading = false;
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

                    this.documentBar.innerHTML = html;

                    // Bind remove handlers
                    var self = this;
                    var closeButtons = this.documentBar.querySelectorAll('.btn-close');
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
                    });

                    this.connection.on("ReceiveError", (error) => {
                        console.error("SignalR Error: ", error);
                    });

                    this.connection.on("MessageRated", (messageId, userRating) => {
                        var msg = this.messages.find(m => m.id === messageId);
                        if (msg) {
                            msg.userRating = userRating;
                        }
                    });

                    this.connection.onreconnecting(() => {
                        console.warn("SignalR: reconnecting...");
                    });

                    this.connection.onreconnected(() => {
                        console.info("SignalR: reconnected.");
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

                            for (const [key, value] of citedRefs) {
                                processedContent = processedContent.replaceAll(key, `<sup><strong>${value.index}</strong></sup>`);
                            }

                            // if we have multiple references, add a comma to ensure we don't concatenate numbers.
                            processedContent = processedContent.replaceAll('</strong></sup><sup>', '</strong></sup><sup>,</sup><sup>');

                            if (citedRefs.length) {
                                processedContent += '<br><br>';

                                for (const [key, value] of citedRefs) {
                                    const label = value.text || key;
                                    processedContent += value.link
                                        ? `**${value.index}**. [${label}](${value.link})<br>`
                                        : `**${value.index}**. ${label}<br>`;
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
                fireEvent(event) {
                    document.dispatchEvent(event);
                },
                isIndicator(message) {
                    return message.role === 'indicator';
                },
                sendMessage() {
                    let trimmedPrompt = this.prompt.trim();

                    if (!trimmedPrompt) {
                        return;
                    }

                    this.addMessage({
                        role: 'user',
                        content: trimmedPrompt
                    });

                    this.streamMessage(this.getProfileId(), trimmedPrompt, null);
                    this.inputElement.value = '';
                    this.prompt = '';
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
                        const citedRefs = Object.entries(references).filter(([key]) => content.includes(key));

                        if (!citedRefs.length) {
                            return;
                        }

                        // Replace [doc:N] markers with superscripts.
                        let processed = content.trim();
                        for (const [key, value] of citedRefs) {
                            processed = processed.replaceAll(key, `<sup><strong>${value.index}</strong></sup>`);
                        }
                        processed = processed.replaceAll('</strong></sup><sup>', '</strong></sup><sup>,</sup><sup>');

                        processed += '<br><br>';

                        for (const [key, value] of citedRefs) {
                            const label = value.text || key;
                            processed += value.link
                                ? `**${value.index}**. [${label}](${value.link})<br>`
                                : `**${value.index}**. ${label}<br>`;
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
                        this.buttonElement.innerHTML = stopIcon;
                    }

                    if (this.inputElement) {
                        this.inputElement.setAttribute('disabled', 'disabled');
                    }
                },
                streamingFinished() {
                    var startIcon = this.buttonElement.getAttribute('data-start-icon');

                    if (startIcon) {
                        this.buttonElement.innerHTML = startIcon;
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
                    this.setSessionId('');
                    this.isSessionStarted = false;
                    this.sessionRating = null;
                    if (this.widgetIsInitialized) {
                        localStorage.removeItem(this.chatWidgetStateSession);
                    }
                    this.messages = [];
                    this.documents = [];
                    this.showPlaceholder();
                },
                initializeApp() {
                    this.inputElement = document.querySelector(config.inputElementSelector);
                    this.buttonElement = document.querySelector(config.sendButtonElementSelector);
                    this.chatContainer = document.querySelector(config.chatContainerElementSelector);
                    this.placeholder = document.querySelector(config.placeholderElementSelector);

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

                            var pre = btn.closest('pre');
                            if (!pre) {
                                return;
                            }

                            var codeEl = pre.querySelector('code');
                            if (codeEl) {
                                navigator.clipboard.writeText(codeEl.textContent);
                            }
                        });
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
                        upBtn.innerHTML = '<i class="' + upClass + '" style="font-size: 0.9rem;"></i>';
                    }

                    if (downBtn) {
                        var downClass = userRating === false ? 'fa-solid fa-thumbs-down' : 'fa-regular fa-thumbs-down';
                        downBtn.innerHTML = '<i class="' + downClass + '" style="font-size: 0.9rem;"></i>';
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
                isUploading() { this.renderDocumentBar(); }
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
