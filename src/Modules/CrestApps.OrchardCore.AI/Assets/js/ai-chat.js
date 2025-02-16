const openAIChatManager = function () {

    var defaultConfig = {
        messageTemplate: `
            <div class="list-group">
                <div v-for="(message, index) in messages" :key="index" class="list-group-item">
                    <div class="d-flex">
                        <div class="p-2">
                            <i :class="message.role === 'user' ? 'fa-solid fa-user fa-2xl text-primary' : 'fa fa-robot fa-2xl text-success'"></i>
                        </div>
                        <div class="p-2 lh-base">
                            <h4 v-if="message.title">{{ message.title }}</h4>
                            <div v-html="message.htmlContent || message.content"></div>
                        </div>
                    </div>
                    <div class="d-flex justify-content-center message-buttons-container" v-if="message.role !== 'indicator'">
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
                    isSessionStarted: false,
                    isPlaceholderVisible: true,
                    chatWidgetStateName: null,
                    chatWidgetStateSession: null,
                    chatHistorySection: null,
                    widgetIsInitialized: false,
                    messages: [],
                    prompt: '',
                    messageBuffers: {} // Holds partial messages
                };
            },
            methods: {
                async startConnection() {
                    this.connection = new signalR.HubConnectionBuilder()
                        .withUrl(config.signalRHubUrl)
                        .withAutomaticReconnect()
                        .build();

                    this.connection.on("ReceiveMessageChunk", (messageId, chunk, isFinal) => {
                        this.handleChunkedMessage(messageId, chunk, isFinal);
                    });

                    try {
                        await this.connection.start();
                        console.log("SignalR Connected");
                    } catch (err) {
                        console.error("SignalR Connection Error: ", err);
                    }
                },
                handleChunkedMessage(messageId, chunk, isFinal) {
                    if (!this.messageBuffers[messageId]) {
                        this.messageBuffers[messageId] = { content: "", messageIndex: null };
                    }

                    let buffer = this.messageBuffers[messageId];
                    buffer.content += chunk;

                    // If first chunk, create message entry
                    if (buffer.messageIndex === null) {
                        let newMessage = { role: 'bot', content: "", htmlContent: "" };
                        this.messages.push(newMessage);
                        buffer.messageIndex = this.messages.length - 1;
                    }

                    // Update the existing message
                    let message = this.messages[buffer.messageIndex];
                    message.content = buffer.content;
                    message.htmlContent = marked.parse(buffer.content);

                    if (isFinal) {
                        delete this.messageBuffers[messageId]; // Clean up buffer
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
                sendMessage() {
                    let trimmedPrompt = this.prompt.trim();

                    if (!trimmedPrompt) {
                        return;
                    }

                    this.addMessage({
                        role: 'user',
                        content: trimmedPrompt
                    });

                    let messageId = Date.now().toString(); // Unique ID for chunked handling
                    this.connection.invoke("SendMessage", messageId, trimmedPrompt).catch(err => console.error(err));

                    this.showTypingIndicator();
                    this.inputElement.value = '';
                    this.prompt = '';
                    this.buttonElement.setAttribute('disabled', true);
                },
                showTypingIndicator() {
                    this.addMessage({
                        role: 'indicator',
                        htmlContent: config.indicatorTemplate
                    });
                },
                hideTypingIndicator() {
                    this.messages = this.messages.filter(msg => msg.role !== 'indicator');
                },
                scrollToBottom() {
                    setTimeout(() => {
                        this.chatContainer.scrollTop = this.chatContainer.scrollHeight - this.chatContainer.clientHeight;
                    }, 50);
                }
            },
            mounted() {
                this.initializeApp();
                this.startConnection();
                if (config.widget) {
                    this.initializeWidget();
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
