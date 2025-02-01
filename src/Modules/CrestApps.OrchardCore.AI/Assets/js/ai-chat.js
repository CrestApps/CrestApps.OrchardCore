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
    }

    const initialize = (instanceConfig) => {

        const config = Object.assign({}, defaultConfig, instanceConfig);

        if (!config.chatUrl) {
            console.error('The chatUrl is required.');
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
                    prompt: ''
                };
            },
            methods: {
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
                handleUserInput(event) {
                    this.prompt = event.target.value;
                },
                getProfileId() {
                    return this.inputElement.getAttribute('data-profile-id');
                },
                resetSession() {
                    this.inputElement.setAttribute('data-session-id', '');
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
                        if (event.key === "Enter" && !event.shiftKey) {
                            this.buttonElement.dispatchEvent(new Event('click'));
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
                reloadCurrentSession() {

                    var sessionId = this.getSessionId();
                    if (sessionId) {
                        this.loadSession(sessionId);
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
                    this.showTypingIndicator();
                    this.inputElement.value = '';
                    this.prompt = '';
                    this.buttonElement.setAttribute('disabled', true);

                    this.completeChat(this.getProfileId(), trimmedPrompt, this.getSessionId());
                },
                generatePrompt(element) {
                    if (!element) {
                        console.error('The element paramter is required.');

                        return;
                    }

                    let profileId = element.getAttribute('data-profile-id');
                    let sessionId = this.getSessionId();

                    if (!profileId || !sessionId) {

                        console.error('The given element is missing data-profile-id or the session has not yet started.');
                        return;
                    }

                    this.showTypingIndicator();
                    this.completeChat(profileId, null, sessionId);
                },
                completeChat(profileId, prompt, sessionId) {

                    var sessionProfileId = this.getProfileId();

                    fetch(config.chatUrl, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            profileId: profileId,
                            sessionId: sessionId,
                            prompt: prompt,
                            sessionProfileId: sessionProfileId == profileId ? null : sessionProfileId
                        })
                    }).then(response => {

                        if (!response.ok) {
                            throw new Error(`Request failed with status ${response.status}`);
                        }

                        return response.json();
                    }).then(result => {
                        this.initializeSession(result.sessionId, false);
                        this.addMessage(result.message);
                        this.hideTypingIndicator();
                    }).catch(error => {
                        console.error('Failed to send the message.', error);
                        this.hideTypingIndicator();
                    });
                },
                createSessionUrl(baseUrl, param, value) {

                    const fullUrl = baseUrl.toLowerCase().startsWith('http') ? baseUrl : window.location.origin + baseUrl;
                    const url = new URL(fullUrl);

                    url.searchParams.set(param, value);

                    return url.toString();
                },
                loadSession(sessionId) {
                    if (!config.widget.sessionUrl) {
                        console.error('The sessionUrl is required.');

                        return;
                    }

                    const url = this.createSessionUrl(config.widget.sessionUrl, 'sessionId', sessionId);

                    fetch(url, {
                        method: 'GET'
                    }).then(response => {

                        if (!response.ok) {
                            throw new Error(`Request failed with status ${response.status}`);
                        }

                        return response.json();
                    }).then(result => {
                        this.initializeSession(result.sessionId, true);
                        this.messages = result.messages ?? [];
                        if (this.messages.length) {
                            this.hidePlaceholder();
                        } else {
                            this.showPlaceholder();
                        }

                        this.$nextTick(() => {
                            this.scrollToBottom();
                        });
                    }).catch(error => {
                        console.error('Failed to load session.', error);
                        this.hideTypingIndicator();
                    });
                },
                fireEvent(event) {
                    document.dispatchEvent(event);
                },
                initializeSession(sessionId, force) {
                    if (this.isSessionStarted && !force) {
                        return
                    }
                    this.fireEvent(new CustomEvent("initializingSessionOpenAIChat", { detail: { sessionId: sessionId } }))
                    this.inputElement.setAttribute('data-session-id', sessionId);
                    this.isSessionStarted = true;

                    if (this.widgetIsInitialized) {
                        localStorage.setItem(this.chatWidgetStateSession, sessionId);
                    }
                },
                showTypingIndicator() {
                    this.addMessage({
                        role: 'indicator',
                        htmlContent: config.indicatorTemplate
                    });
                },
                hideTypingIndicator() {
                    this.messages = this.messages.filter(msg => msg.role != 'indicator');
                },
                scrollToBottom() {
                    setTimeout(() => {
                        this.chatContainer.scrollTop = this.chatContainer.scrollHeight - this.chatContainer.clientHeight;
                    }, 50)
                }
            },
            mounted() {
                this.initializeApp();

                if (config.widget) {
                    this.initializeWidget();
                }
            },
            template: config.messageTemplate
        }).mount(config.appElementSelector);

        return app;
    }

    return {
        initialize: initialize
    }
}();
