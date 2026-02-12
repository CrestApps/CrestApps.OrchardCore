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
    messageTemplate: `
        <div class="list-group">
            <div v-for="(message, index) in messages" :key="index" class="list-group-item">
                <div class="d-flex align-items-center">
                    <div class="p-2">
                        <i :class="message.role === 'user' ? 'fa-solid fa-user fa-2xl text-primary' : 'fa fa-robot fa-2xl text-success'"></i>
                    </div>
                    <div class="p-2 lh-base">
                        <h4 v-if="message.title">{{ message.title }}</h4>
                        <div v-html="message.htmlContent || message.content"></div>
                    </div>
                </div>
                <div class="d-flex justify-content-center message-buttons-container" v-if="!isIndicator(message)">
                    <button class="ms-2 btn btn-sm btn-outline-secondary button-message-toolbox" @click="copyResponse(message.content)" title="Click here to copy response to clipboard.">
                        <i class="fa-solid fa-copy fa-lg"></i>
                    </button>
                </div>
            </div>
        </div>
    `,
    indicatorTemplate: `<div class="spinner-grow spinner-grow-sm" role="status"><span class="visually-hidden">Loading...</span></div>`
};

const renderer = new marked.Renderer();

// Modify the link rendering to open in a new tab
renderer.link = function (data) {
    return `<a href="${data.href}" target="_blank" rel="noopener noreferrer">${data.text}</a>`;
};

// Custom image renderer for generated images with thumbnail styling and download button
renderer.image = function (data) {
    const src = data.href;
    const alt = data.text || defaultConfig.generatedImageAltText;
    const maxWidth = defaultConfig.generatedImageMaxWidth;
    return `<div class="generated-image-container">
        <img src="${src}" alt="${alt}" class="img-thumbnail" style="max-width: ${maxWidth}px; height: auto;" />
        <div class="mt-2">
            <a href="${src}" target="_blank" download title="${defaultConfig.downloadImageTitle}" class="btn btn-sm btn-outline-secondary">
                <i class="fa-solid fa-download"></i>
            </a>
        </div>
    </div>`;
};

// Chart counter for unique IDs
let chartCounter = 0;

function createChartHtml(chartId) {
    const chartMaxWidth = defaultConfig.generatedChartMaxWidth;

    return `<div class="chart-container" style="position: relative; width: 100%; max-width: ${chartMaxWidth}px; margin: 0 auto; height: 480px;">
        <canvas id="${chartId}" class="img-thumbnail" width="${chartMaxWidth}" height="480" style="width: 100%; height: 480px;"></canvas>
    </div>
    <div class="mt-2">
        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="downloadChart('${chartId}')" title="${defaultConfig.downloadChartTitle}">
            <i class="fa-solid fa-download"></i> ${defaultConfig.downloadChartButtonText}
        </button>
    </div>`;
}

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

// Replace chart markers in content with chart placeholders and collect configs.
function processChartMarkers(content, message) {
    if (!content) {
        return content;
    }

    let result = content;
    message._pendingCharts ??= [];

    // Only replace markers when we can fully extract them.
    while (true) {
        const extracted = tryExtractChartMarker(result);
        if (!extracted) {
            break;
        }

        const chartId = `chat_chart_${++chartCounter}`;
        message._pendingCharts.push({ chartId: chartId, config: extracted.json });

        const html = createChartHtml(chartId);
        result = result.substring(0, extracted.startIndex) + html + result.substring(extracted.endIndex);
    }

    return result;
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
                    stream: null,
                    messages: [],
                    prompt: ''
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

                    this.connection.on("LoadSession", (data) => {
                        this.initializeSession(data.sessionId, true);
                        this.messages = [];

                        (data.messages ?? []).forEach(msg => {
                            // Ensure persisted chart markers are rendered too
                            if (msg && msg.content) {
                                msg.content = processChartMarkers(msg.content.trim(), msg);
                                if (msg.content.includes('class="chart-container"')) {
                                    msg.htmlContent = msg.content;
                                }
                            }

                            this.addMessage(msg);

                            this.$nextTick(() => {
                                renderChartsInMessage(msg);
                            });
                        });

                    });

                    this.connection.on("ReceiveError", (error) => {
                        console.error("SignalR Error: ", error);
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

                    if (message.content) {
                        let processedContent = message.content.trim();

                        // Process chart markers first (before markdown parsing)
                        processedContent = processChartMarkers(processedContent, message);

                        if (message.references && typeof message.references === "object" && Object.keys(message.references).length) {

                            for (const [key, value] of Object.entries(message.references)) {
                                processedContent = processedContent.replaceAll(key, `<sup><strong>${value.index}</strong></sup>`);
                            }

                            // if we have multiple references, add a comma to ensure we don't concatenate numbers.
                            processedContent = processedContent.replaceAll('</strong></sup><sup>', '</strong></sup><sup>,</sup><sup>');
                            processedContent += '<br><br>';

                            for (const [key, value] of Object.entries(message.references)) {
                                processedContent += `**${value.index}**. [${value.text}](${value.link})<br>`;
                            }
                        }

                        message.content = processedContent;

                        // If we inserted chart HTML, don't markdown-parse
                        if (processedContent.includes('class="chart-container"')) {
                            message.htmlContent = processedContent;
                        } else {
                            message.htmlContent = marked.parse(processedContent, { renderer });
                        }
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
                                        this.setSessionId(chunk.sessionId);
                                    }

                                    this.hideTypingIndicator();
                                    // Re-assign the index after hiding the typing indicator.
                                    messageIndex = this.messages.length;
                                    let newMessage = {
                                        role: "assistant",
                                        title: chunk.title,
                                        content: "",
                                        htmlContent: "",
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

                                // Process chart markers before markdown parsing
                                let htmlContent = processChartMarkers(content, message);

                                if (htmlContent.includes('class="chart-container"')) {
                                    message.htmlContent = htmlContent;
                                } else {
                                    message.htmlContent = marked.parse(htmlContent, { renderer });
                                }

                                this.messages[messageIndex] = message;

                                this.$nextTick(() => {
                                    renderChartsInMessage(message);
                                    this.scrollToBottom();
                                });
                            },
                            complete: () => {
                                this.processReferences(references, messageIndex);
                                this.streamingFinished();

                                if (!this.messages[messageIndex].content) {
                                    // Blank message received.
                                    this.hideTypingIndicator();
                                    this.addMessage(this.getServiceDownMessage());
                                    console.log('blank message');
                                }

                                this.stream?.dispose();
                                this.stream = null;
                            },
                            error: (err) => {
                                this.processReferences(references, messageIndex);
                                this.streamingFinished();

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

                        message.content = (message.content?.trim() + '<br><br>' || '');

                        for (const [key, value] of Object.entries(references)) {
                            message.content += `**${value.index}**. [${value.text}](${value.link})<br>`;
                        }

                        message.htmlContent = marked.parse(message.content, { renderer });

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
                    if (this.widgetIsInitialized) {
                        localStorage.removeItem(this.chatWidgetStateSession);
                    }
                    this.messages = [];
                    this.showPlaceholder();
                },
                initializeApp() {
                    this.inputElement = document.querySelector(config.inputElementSelector);
                    this.buttonElement = document.querySelector(config.sendButtonElementSelector);
                    this.chatContainer = document.querySelector(config.chatContainerElementSelector);
                    this.placeholder = document.querySelector(config.placeholderElementSelector);

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

                    this.buttonElement.addEventListener('click', () => {

                        if (this.stream != null) {
                            this.stream.dispose();
                            this.stream = null;

                            this.streamingFinished();

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

                    if (!config.widget.showChatButton) {
                        console.error('The widget showChatButton is required.');
                        return;
                    }

                    if (!config.widget.chatWidgetContainer) {
                        console.error('The widget chatWidgetContainer is required.');
                        return;
                    }

                    if (!config.widget.chatWidgetStateName) {
                        console.error('The widget chatWidgetStateName is required.');
                        return;
                    }

                    const showChatButton = document.querySelector(config.widget.showChatButton);

                    if (!showChatButton) {
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

                    const isOpen = localStorage.getItem(this.chatWidgetStateName) === 'open';

                    if (isOpen) {
                        this.reloadCurrentSession();
                        chatWidgetContainer.classList.remove('d-none');
                    }

                    showChatButton.addEventListener('click', () => {
                        var isHidden = chatWidgetContainer.classList.contains('d-none');
                        if (isHidden) {
                            chatWidgetContainer.classList.remove('d-none');
                            localStorage.setItem(this.chatWidgetStateName, 'open');
                            this.reloadCurrentSession();
                        } else {
                            chatWidgetContainer.classList.add('d-none');
                            localStorage.setItem(this.chatWidgetStateName, 'closed');
                        }
                    });

                    if (config.widget.closeChatButton) {
                        const closeChatButton = document.querySelector(config.widget.closeChatButton);

                        if (closeChatButton) {
                            closeChatButton.addEventListener('click', () => {
                                chatWidgetContainer.classList.add('d-none');
                                localStorage.setItem(this.chatWidgetStateName, 'closed');
                            });
                        }
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
