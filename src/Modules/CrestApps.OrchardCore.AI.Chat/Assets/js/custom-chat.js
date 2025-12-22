window.customChatManager = function () {

    const renderer = new marked.Renderer();

    renderer.link = function (data) {

        return `<a href="${data.href}" target="_blank" rel="noopener noreferrer">${data.text}</a>`;
    };

    const defaultConfig = {
        messageTemplate: `
        <div class="list-group">
            <div v-if="messages.length === 0" class="list-group-item text-muted text-center py-5">
                <i class="fa fa-robot fa-2xl mb-3 d-block"></i>
                <div>How can we help you today?</div>
            </div>

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
            </div>
        </div>
    `,
        indicatorTemplate:
            `<div class="spinner-grow spinner-grow-sm" role="status"></div>`
    };


    const initialize = (instanceConfig) => {

        const config = Object.assign({}, defaultConfig, instanceConfig);

        const app = Vue.createApp({
            data() {
                return {
                    inputElement: null,
                    buttonElement: null,
                    chatContainer: null,
                    messages: [],
                    prompt: '',
                    stream: null,
                    connection: null
                };
            },
            methods: {
                async startConnection() {
                    this.connection = new signalR.HubConnectionBuilder()
                        .withUrl(config.signalRHubUrl)
                        .withAutomaticReconnect()
                        .build();

                    this.connection.on("LoadSession", (data) => {

                        this.setSessionId(data.sessionId);

                        this.messages = [];

                        (data.messages ?? []).forEach(m => this.addMessage(m));

                        requestAnimationFrame(() => {

                            if (this.inputElement) {

                                this.inputElement.placeholder = 'How can we help you today?';

                                this.inputElement.focus();
                            }
                        });
                    });


                    this.connection.on("ReceiveError", (err) => {
                        console.error("[CustomChat] Hub error:", err);
                    });

                    await this.connection.start();
                },

                loadSession(customChatInstanceId) {

                    this.connection.invoke("LoadCustomChatSession", customChatInstanceId);
                },

                sendMessage() {

                    const text = this.prompt.trim();

                    if (!text) {

                        return;
                    }

                    this.addMessage({ role: 'user', content: text });

                    this.streamMessage(text);

                    this.prompt = '';

                    this.inputElement.value = '';
                },

                streamMessage(prompt) {
                    if (this.stream) {
                        this.stream.dispose();
                        this.stream = null;
                    }

                    const sessionId = this.getSessionId();

                    const customChatInstanceId = this.inputElement.getAttribute('data-custom-chat-instance-id');

                    this.showTypingIndicator();

                    this.stream = this.connection.stream("SendCustomChatMessage", customChatInstanceId, prompt, sessionId).subscribe(
                        {
                            next: (chunk) => this.applyStreamChunk(chunk),
                            complete: () => this.hideTypingIndicator(),
                            error: () => this.hideTypingIndicator()
                        });
                },

                applyStreamChunk(chunk) {

                    this.hideTypingIndicator();

                    let message = this.messages[this.messages.length - 1];

                    if (!message || message.role !== 'assistant') {

                        message = {
                            role: 'assistant',
                            content: '',
                            htmlContent: ''
                        };

                        this.messages.push(message);
                    }

                    if (chunk.sessionId && !this.getSessionId()) {

                        this.setSessionId(chunk.sessionId);
                    }

                    message.content += chunk.content || '';

                    message.htmlContent = marked.parse(message.content, { renderer });

                    this.scrollToBottom();
                },

                addMessage(message) {

                    if (message.content) {

                        message.htmlContent = marked.parse(message.content, { renderer });
                    }

                    this.messages.push(message);

                    this.scrollToBottom();
                },

                showTypingIndicator() {
                    this.messages.push({
                        role: 'indicator',
                        htmlContent: defaultConfig.indicatorTemplate
                    });
                },

                hideTypingIndicator() {
                    this.messages = this.messages.filter(m => m.role !== 'indicator');
                },

                scrollToBottom() {
                    setTimeout(() => {
                        this.chatContainer.scrollTop = this.chatContainer.scrollHeight;
                    }, 30);
                },

                getSessionId() {
                    return this.inputElement.getAttribute('data-session-id');
                },

                setSessionId(id) {
                    this.inputElement.setAttribute('data-session-id', id || '');
                },

                initDom() {
                    this.inputElement = document.querySelector(config.inputElementSelector);

                    this.buttonElement = document.querySelector(config.sendButtonElementSelector);

                    this.chatContainer = document.querySelector(config.chatContainerElementSelector);

                    this.inputElement.setAttribute('placeholder', 'How can we help you today?');

                    this.inputElement.focus();

                    this.inputElement.addEventListener('input', e => {

                        this.prompt = e.target.value;

                        this.buttonElement.disabled = !this.prompt.trim();
                    });

                    this.inputElement.addEventListener('keydown', e => {

                        if (e.key === 'Enter' && !e.shiftKey) {

                            e.preventDefault();

                            if (!this.buttonElement.disabled) {

                                this.sendMessage();
                            }
                        }
                    });

                    this.buttonElement.addEventListener('click', () => {
                        this.sendMessage();
                    });
                }
            },
            async mounted() {
                await this.startConnection();

                this.initDom();

                const customChatInstanceId = this.inputElement.getAttribute('data-custom-chat-instance-id');

                if (customChatInstanceId) {

                    this.loadSession(customChatInstanceId);
                }
            },
            template: config.messageTemplate
        });

        return app.mount(config.appElementSelector);
    };

    return { initialize };
}();