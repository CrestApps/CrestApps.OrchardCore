const openAIChatManager = function () {

    var defaultConfig = {
        messageTemplate: `
            <div class="list-group">
                <div v-for="(message, index) in messages" :key="index" class="list-group-item">
                    <div class="d-flex">
                        <div class="p-2">
                            <i :class="message.role === 'user' ? 'fa-solid fa-user fa-2xl text-primary' : 'fa fa-robot fa-2xl text-success'"></i>
                        </div>
                        <div class="p-2 flex-grow-1" v-html="message.promptHTML || message.prompt"></div>
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
                    isSessionStarted: false,
                    messages: [],
                    prompt: ''
                };
            },
            methods: {
                addMessage(message) {
                    this.messages.push(message);
                    this.$nextTick(() => {
                        this.scrollToBottom();
                    });
                },
                handleUserInput(event) {
                    this.prompt = event.target.value;
                },
                sendMessage() {
                    let trimmedPrompt = this.prompt.trim();

                    if (!trimmedPrompt) {
                        return;
                    }

                    const userMessage = {
                        role: 'user',
                        prompt: trimmedPrompt
                    };

                    const inputElement = document.querySelector(config.inputElementSelector);
                    const buttonElement = document.querySelector(config.sendButtonElementSelector);

                    this.addMessage(userMessage);
                    this.showTypingIndicator();
                    inputElement.value = '';
                    this.prompt = '';
                    buttonElement.setAttribute('disabled', true);

                    this.completeChat(inputElement.getAttribute('data-profile-id'), userMessage.prompt, inputElement.getAttribute('data-session-id'));
                },
                generatePrompt(element) {
                    if (!element) {
                        console.error('The element paramter is required.');

                        return;
                    }

                    let profileId = element.getAttribute('data-profile-id');
                    let sessionId = element.getAttribute('data-session-id');

                    if (!profileId || !sessionId) {

                        console.error('The given element is missing data-profile-id and/or data-session-id');
                        return;
                    }

                    this.completeChat(profileId, null, sessionId);
                },
                completeChat(profileId, prompt, sessionId) {
                    fetch(config.chatUrl, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            profileId: profileId,
                            sessionId: sessionId,
                            prompt: prompt
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

                    const inputElement = document.querySelector(config.inputElementSelector);

                    inputElement.setAttribute('data-session-id', sessionId);

                    var elements = document.getElementsByClassName('profile-generated-prompt');

                    for (var i = 0; i < elements.length; i++) {
                        elements[i].setAttribute('data-session-id', sessionId);
                    }

                    this.isSessionStarted = true;
                },
                showTypingIndicator() {
                    this.addMessage({
                        role: 'indicator',
                        promptHTML: config.indicatorTemplate
                    });
                },
                hideTypingIndicator() {
                    this.messages = this.messages.filter(msg => msg.role != 'indicator');
                },
                scrollToBottom() {
                    const chatContainer = document.querySelector(config.chatContainerElementSelector);
                    chatContainer.scrollTop = chatContainer.scrollHeight - chatContainer.clientHeight;
                }
            },
            mounted() {
                const sendButton = document.querySelector(config.sendButtonElementSelector);
                const userPrompt = document.querySelector(config.inputElementSelector);
                const placeholder = document.querySelector(config.placeholderElementSelector);

                userPrompt.addEventListener('keyup', event => {
                    if (event.key === "Enter") {
                        sendButton.dispatchEvent(new Event('click'));
                    }
                });

                userPrompt.addEventListener('input', (e) => {
                    if (e.target.value.trim()) {
                        sendButton.removeAttribute('disabled');
                    } else {
                        sendButton.setAttribute('disabled', true);
                    }

                    this.handleUserInput(e);
                });

                sendButton.addEventListener('click', () => {

                    if (placeholder) {
                        placeholder.classList.add('d-none');
                    }

                    this.sendMessage();
                });

                const promptGenerators = document.getElementsByClassName('profile-generated-prompt');

                for (var i = 0; i < promptGenerators.length; i++) {
                    promptGenerators[i].addEventListener('click', (e) => {
                        e.preventDefault();
                        this.generatePrompt(e);
                    });
                }

                if (config.messages.length) {
                    for (let i = 0; i < config.messages.length; i++) {
                        this.addMessage(config.messages[i]);
                    }

                    this.scrollToBottom();
                }
            },
            template: config.messageTemplate
        }).mount(config.appElementSelector)
    }

    return {
        initialize: initialize
    }
}();
