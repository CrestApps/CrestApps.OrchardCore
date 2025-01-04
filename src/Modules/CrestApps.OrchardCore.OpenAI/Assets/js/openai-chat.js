const openAIChatManager = function () {

    var defaultConfig = {
        messageTemplate: `
            <div class="list-group">
                <div v-for="(message, index) in messages" :key="index" class="list-group-item">
                    <div class="d-flex">
                        <div class="p-2">
                            <i :class="message.role === 'user' ? 'fa-solid fa-user fa-2xl text-primary' : 'fa fa-robot fa-2xl text-success'"></i>
                        </div>
                        <div class="p-2 flex-grow-1">
                            <h4 v-if="message.title">{{ message.title }}</h4>
                            <div v-html="message.contentHTML || message.content"></div>
                        </div>
                    </div>
                    <div class="d-flex justify-content-center message-buttons-container">
                        <button class="ms-2 btn btn-sm btn-outline-secondary" @click="copyResponse(message.content)" title="Click here to copy response to clipboard.">
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

        Vue.createApp({
            data() {
                return {
                    inputElement: null,
                    buttonElement: null,
                    chatContainer: null,
                    placeholder: null,
                    isSessionStarted: false,
                    isPlaceholderVisible: true,
                    messages: [],
                    prompt: ''
                };
            },
            methods: {
                addMessage(message) {
                    this.messages.push(message);

                    if (message.role != 'indicator') {
                        this.$emit('addingOpenAIPromotMessage', message);
                    }

                    if (this.isPlaceholderVisible) {
                        if (this.placeholder) {
                            this.placeholder.classList.add('d-none');
                        }
                        this.isPlaceholderVisible = false;
                    }

                    this.$nextTick(() => {
                        this.$emit('addedOpenAIPromotMessage', message);
                        this.scrollToBottom();
                    });
                },
                handleUserInput(event) {
                    this.prompt = event.target.value;
                },
                getProfileId() {
                    return this.inputElement.getAttribute('data-profile-id');
                },
                initializeElements() {
                    this.inputElement = document.querySelector(config.inputElementSelector);
                    this.buttonElement = document.querySelector(config.sendButtonElementSelector);
                    this.chatContainer = document.querySelector(config.chatContainerElementSelector);
                    this.placeholder = document.querySelector(config.placeholderElementSelector);

                    this.inputElement.addEventListener('keyup', event => {
                        if (event.key === "Enter") {
                            this.buttonElement.dispatchEvent(new Event('click'));
                        }
                    });

                    this.inputElement.addEventListener('input', (e) => {
                        if (e.target.value.trim()) {
                            this.buttonElement.removeAttribute('disabled');
                        } else {
                            this.buttonElement.setAttribute('disabled', true);
                        }

                        this.handleUserInput(e);
                    });

                    this.buttonElement.addEventListener('click', () => {
                        this.sendMessage();
                    });
                },
                getSessionId() {
                    return this.inputElement.getAttribute('data-session-id');
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
                    }).then(response => response.json())
                        .then(result => {
                            this.setSession(result.sessionId);
                            this.addMessage(result.message);
                            this.hideTypingIndicator();
                            this.scrollToBottom();
                        })
                        .catch(error => {
                            console.error('Failed to send the message.', error);
                            this.hideTypingIndicator();
                        });
                },
                setSession(sessionId) {
                    if (this.isSessionStarted) {
                        return
                    }

                    this.inputElement.setAttribute('data-session-id', sessionId);
                    this.isSessionStarted = true;
                },
                showTypingIndicator() {
                    this.addMessage({
                        role: 'indicator',
                        contentHTML: config.indicatorTemplate
                    });
                },
                hideTypingIndicator() {
                    this.messages = this.messages.filter(msg => msg.role != 'indicator');
                },
                scrollToBottom() {
                    this.chatContainer.scrollTop = this.chatContainer.scrollHeight - this.chatContainer.clientHeight;
                }
            },
            mounted() {
                // First initialize elements.
                this.initializeElements();

                const promptGenerators = document.getElementsByClassName('profile-generated-prompt');

                for (var i = 0; i < promptGenerators.length; i++) {
                    promptGenerators[i].addEventListener('click', (e) => {
                        e.preventDefault();
                        this.generatePrompt(e.target);
                    });
                }

                for (let i = 0; i < config.messages.length; i++) {
                    this.addMessage(config.messages[i]);
                }
            },
            template: config.messageTemplate
        }).mount(config.appElementSelector)
    }

    return {
        initialize: initialize
    }
}();
