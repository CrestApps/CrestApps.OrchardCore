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

        messageTemplate: `
            <div class="ai-chat-messages">
                <div v-for="(message, index) in messages" :key="index" class="ai-chat-message-item">
                    <div>
                        <div v-if="message.role === 'user'" class="ai-chat-msg-role ai-chat-msg-role-user">You</div>
                        <div v-else-if="message.role !== 'indicator'" class="ai-chat-msg-role ai-chat-msg-role-assistant">
                            <i :class="'fa fa-robot' + (message.isStreaming ? ' ai-streaming-icon' : ' ai-bot-icon')"></i>
                            Assistant
                        </div>
                        <div class="lh-base">
                            <h4 v-if="message.title">{{ message.title }}</h4>
                            <div v-html="message.htmlContent || message.content"></div>
                            <span class="message-buttons-container" v-if="!isIndicator(message)">
                                <button class="btn btn-sm btn-link text-secondary p-0 button-message-toolbox" @click="copyResponse(message.content)" title="Click here to copy response to clipboard.">
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
        `,
        // Localizable strings
        untitledText: 'Untitled',
        clearHistoryTitle: 'Clear History',
        clearHistoryMessage: 'Are you sure you want to clear the chat history? This action cannot be undone. Your documents, parameters, and tools will be preserved.',
        clearHistoryOkText: 'Yes',
        clearHistoryCancelText: 'Cancel'
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
                    saveSettingsTimeout: null
                };
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
                            for (const [key, value] of Object.entries(message.references)) {
                                processedContent = processedContent.replaceAll(key, `<sup><strong>${value.index}</strong></sup>`);
                            }
                            processedContent = processedContent.replaceAll('</strong></sup><sup>', '</strong></sup><sup>,</sup><sup>');
                            processedContent += '<br><br>';

                            for (const value of Object.values(message.references)) {
                                processedContent += `**${value.index}**. [${value.text}](${value.link})<br>`;
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
                isIndicator(message) {
                    return message.role === 'indicator';
                },
                async sendMessage() {
                    let trimmedPrompt = this.prompt.trim();

                    if (!trimmedPrompt) {
                        return;
                    }

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

                        message.content = (message.content?.trim() + '<br><br>' || '');

                        for (const value of Object.values(references)) {
                            message.content += `**${value.index}**. [${value.text}](${value.link})<br>`;
                        }

                        message.htmlContent = parseMarkdownContent(message.content, message);

                        this.messages[messageIndex] = message;

                        this.scrollToBottom();
                    }
                },
                streamingStarted() {
                    var stopIcon = this.buttonElement.getAttribute('data-stop-icon');

                    if (stopIcon) {
                        this.buttonElement.innerHTML = stopIcon;
                    }
                },
                streamingFinished() {
                    var startIcon = this.buttonElement.getAttribute('data-start-icon');

                    if (startIcon) {
                        this.buttonElement.innerHTML = startIcon;
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
                },
                loadInteraction(itemId) {
                    this.connection.invoke("LoadInteraction", itemId).catch(err => console.error(err));
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
                        'input[name^="ChatInteraction."]:not([name*=".Tools["]):not([name*=".Connections["]), ' +
                        'select[name^="ChatInteraction."]:not([name*=".Tools["]):not([name*=".Connections["]), ' +
                        'textarea[name^="ChatInteraction."]:not([name*=".Tools["]):not([name*=".Connections["])'
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

                    // Add tool and MCP connection collections (special handling).
                    settings.toolNames = this.getSelectedToolNames();
                    settings.mcpConnectionIds = this.getSelectedMcpConnectionIds();

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
