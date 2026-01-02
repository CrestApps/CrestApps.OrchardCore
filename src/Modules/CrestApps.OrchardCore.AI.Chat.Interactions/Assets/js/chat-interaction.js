window.chatInteractionManager = function () {

    const renderer = new marked.Renderer();

    // Modify the link rendering to open in a new tab
    renderer.link = function (data) {
        return `<a href="${data.href}" target="_blank" rel="noopener noreferrer">${data.text}</a>`;
    };

    var defaultConfig = {
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

    const initialize = (instanceConfig) => {

        const config = Object.assign({}, defaultConfig, instanceConfig);

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

                    this.connection.on("LoadInteraction", (data) => {
                        this.initializeInteraction(data.itemId, true);
                        this.messages = [];

                        // Update the title field if it exists
                        const titleInput = document.querySelector('input[name="Title"]');
                        if (titleInput && data.title) {
                            titleInput.value = data.title;
                        }

                        (data.messages ?? []).forEach(msg => {
                            this.addMessage(msg);
                        });
                    });

                    this.connection.on("SettingsSaved", (itemId, title) => {
                        // Update the history list item if it exists
                        const historyItem = document.querySelector(`[data-interaction-id="${itemId}"]`);
                        if (historyItem) {
                            historyItem.textContent = title || 'Untitled';
                        }
                    });

                    this.connection.on("ReceiveError", (error) => {
                        console.error("SignalR Error: ", error);
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
                        message.htmlContent = marked.parse(processedContent, { renderer });
                    }

                    this.addMessageInternal(message);
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
                                message.htmlContent = marked.parse(content, { renderer });

                                this.messages[messageIndex] = message;

                                this.scrollToBottom();
                            },
                            complete: () => {
                                this.processReferences(references, messageIndex);
                                this.streamingFinished();

                                if (!this.messages[messageIndex].content) {
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

                    // Add event listeners for settings fields to save on change
                    const settingsInputs = document.querySelectorAll('input[name="ChatInteraction.Title"], input[name="ChatInteraction.ConnectionName"], select[name="ChatInteraction.DeploymentId"], textarea[name="ChatInteraction.SystemMessage"], input[name="ChatInteraction.Temperature"], input[name="ChatInteraction.TopP"], input[name="ChatInteraction.FrequencyPenalty"], input[name="ChatInteraction.PresencePenalty"], input[name="ChatInteraction.MaxTokens"], input[name="ChatInteraction.PastMessagesCount"]');
                    settingsInputs.forEach(input => {
                        input.addEventListener('blur', () => this.saveSettings());
                        // Also save on change for select elements
                        if (input.tagName === 'SELECT') {
                            input.addEventListener('change', () => this.saveSettings());
                        }
                    });
                },
                loadInteraction(itemId) {
                    this.connection.invoke("LoadInteraction", itemId).catch(err => console.error(err));
                },
                saveSettings() {
                    const itemId = this.getItemId();
                    if (!itemId) {
                        return;
                    }

                    const titleInput = document.querySelector('input[name="ChatInteraction.Title"]');
                    const connectionNameInput = document.querySelector('input[name="ChatInteraction.ConnectionName"]');
                    const deploymentIdInput = document.querySelector('select[name="ChatInteraction.DeploymentId"]');
                    const systemMessageInput = document.querySelector('textarea[name="ChatInteraction.SystemMessage"]');
                    const temperatureInput = document.querySelector('input[name="ChatInteraction.Temperature"]');
                    const topPInput = document.querySelector('input[name="ChatInteraction.TopP"]');
                    const frequencyPenaltyInput = document.querySelector('input[name="ChatInteraction.FrequencyPenalty"]');
                    const presencePenaltyInput = document.querySelector('input[name="ChatInteraction.PresencePenalty"]');
                    const maxTokensInput = document.querySelector('input[name="ChatInteraction.MaxTokens"]');
                    const pastMessagesCountInput = document.querySelector('input[name="ChatInteraction.PastMessagesCount"]');

                    const settings = {
                        title: titleInput?.value || 'Untitled',
                        connectionName: connectionNameInput?.value || null,
                        deploymentId: deploymentIdInput?.value || null,
                        systemMessage: systemMessageInput?.value || null,
                        temperature: temperatureInput?.value ? parseFloat(temperatureInput.value) : null,
                        topP: topPInput?.value ? parseFloat(topPInput.value) : null,
                        frequencyPenalty: frequencyPenaltyInput?.value ? parseFloat(frequencyPenaltyInput.value) : null,
                        presencePenalty: presencePenaltyInput?.value ? parseFloat(presencePenaltyInput.value) : null,
                        maxTokens: maxTokensInput?.value ? parseInt(maxTokensInput.value) : null,
                        pastMessagesCount: pastMessagesCountInput?.value ? parseInt(pastMessagesCountInput.value) : null
                    };

                    this.connection.invoke(
                        "SaveSettings",
                        itemId,
                        settings.title,
                        settings.connectionName,
                        settings.deploymentId,
                        settings.systemMessage,
                        settings.temperature,
                        settings.topP,
                        settings.frequencyPenalty,
                        settings.presencePenalty,
                        settings.maxTokens,
                        settings.pastMessagesCount
                    ).catch(err => console.error('Error saving settings:', err));
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
