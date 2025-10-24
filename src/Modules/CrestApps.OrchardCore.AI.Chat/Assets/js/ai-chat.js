window.openAIChatManager = function () {

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
                    microphoneButton: null,
                    chatContainer: null,
                    placeholder: null,
                    isSessionStarted: false,
                    isPlaceholderVisible: true,
                    chatWidgetStateName: null,
                    chatWidgetStateSession: null,
                    chatHistorySection: null,
                    widgetIsInitialized: false,
                    isSteaming: false,
                    isRecording: false,
                    mediaRecorder: null,
                    audioChunks: [],
                    stream: null,
                    messages: [],
                    prompt: ''
                };
            },
            methods: {
                async startConnection() {
                    this.connection = new signalR.HubConnectionBuilder()
                        .withUrl(config.signalRHubUrl)
                        .withAutomaticReconnect()
                        .build();

                    this.connection.on("LoadSession", (data) => {
                        this.initializeSession(data.sessionId, true);
                        this.messages = [];

                        (data.messages ?? []).forEach(msg => {

                            this.addMessage(msg);
                        });

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
                    this.fireEvent(new CustomEvent("addingOpenAIPromotMessage", { detail: { message: message } }));
                    this.messages.push(message);

                    this.$nextTick(() => {
                        this.fireEvent(new CustomEvent("addedOpenAIPromotMessage", { detail: { message: message } }));
                    });
                },
                addMessage(message) {

                    if (message.content) {
                        let processedContent = message.content.trim();
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
                        message.htmlContent = marked.parse(processedContent, { renderer });
                    }

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

                    // Get the index after showing typing indicator.
                    var messageIndex = this.messages.length;
                    var currentSessionId = this.getSessionId();

                    this.stream = this.connection.stream("SendMessage", this.getProfileId(), trimmedPrompt, currentSessionId, null)
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

                                    // Append processed content to the message.
                                    // if we have multiple references, add a comma to ensure we don't concatenate numbers.
                                    content += processedContent.replaceAll('</strong></sup><sup>', '</strong></sup><sup>,</sup><sup>');
                                }

                                // Update the existing message
                                message.content = content;
                                message.htmlContent = marked.parse(content, { renderer });

                                this.messages[messageIndex] = message;

                                this.scrollToBottom();
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
                                this.addMessage(this.getServiceDownMessage());

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

                    let profileId = element.getAttribute('data-profile-id');
                    let sessionId = this.getSessionId();

                    if (!profileId || !sessionId) {

                        console.error('The given element is missing data-profile-id or the session has not yet started.');
                        return;
                    }

                    this.showTypingIndicator();
                    this.streamMessage(null);
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

                    if (config.microphoneButtonElementSelector) {
                        this.microphoneButton = document.querySelector(config.microphoneButtonElementSelector);
                        
                        if (this.microphoneButton) {
                            this.microphoneButton.addEventListener('click', () => {
                                if (this.isRecording) {
                                    this.stopRecording();
                                } else {
                                    this.startRecording();
                                }
                            });
                        }
                    }

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
                },
                async startRecording() {
                    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                        console.error('Media devices not supported');
                        alert('Your browser does not support audio recording. Please use a modern browser like Chrome, Edge, or Firefox.');
                        return;
                    }

                    try {
                        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                        this.mediaRecorder = new MediaRecorder(stream, {
                            mimeType: 'audio/webm'
                        });
                        this.audioChunks = [];

                        this.mediaRecorder.ondataavailable = (event) => {
                            if (event.data.size > 0) {
                                this.audioChunks.push(event.data);
                            }
                        };

                        this.mediaRecorder.onstop = async () => {
                            const audioBlob = new Blob(this.audioChunks, { type: 'audio/webm' });
                            await this.sendAudioMessage(audioBlob);
                            
                            // Stop all tracks to release the microphone
                            stream.getTracks().forEach(track => track.stop());
                        };

                        this.mediaRecorder.start();
                        this.isRecording = true;
                        
                        if (this.microphoneButton) {
                            this.microphoneButton.classList.add('btn-danger');
                            this.microphoneButton.classList.remove('btn-outline-secondary');
                            this.microphoneButton.innerHTML = '<i class="fa-solid fa-stop"></i>';
                        }
                    } catch (error) {
                        console.error('Error accessing microphone:', error);
                        alert('Unable to access microphone. Please check your browser permissions and try again.');
                    }
                },
                stopRecording() {
                    if (this.mediaRecorder && this.isRecording) {
                        this.mediaRecorder.stop();
                        this.isRecording = false;
                        
                        if (this.microphoneButton) {
                            this.microphoneButton.classList.remove('btn-danger');
                            this.microphoneButton.classList.add('btn-outline-secondary');
                            this.microphoneButton.innerHTML = '<i class="fa-solid fa-microphone"></i>';
                        }
                    }
                },
                async sendAudioMessage(audioBlob) {
                    try {
                        this.showTypingIndicator();

                        // Convert blob to base64 for SignalR transmission
                        const reader = new FileReader();
                        reader.readAsDataURL(audioBlob);
                        
                        reader.onloadend = async () => {
                            const base64Audio = reader.result.split(',')[1];
                            
                            try {
                                const transcribedText = await this.connection.invoke(
                                    "SendAudioMessage",
                                    this.getProfileId(),
                                    base64Audio,
                                    this.getSessionId(),
                                    null
                                );

                                this.hideTypingIndicator();

                                if (transcribedText) {
                                    // Set the transcribed text in the input field
                                    this.inputElement.value = transcribedText;
                                    this.prompt = transcribedText;
                                    
                                    // Enable the send button
                                    this.buttonElement.removeAttribute('disabled');
                                    
                                    // Optionally auto-send the message
                                    // Uncomment the next line if you want to automatically send after transcription
                                    // this.sendMessage();
                                } else {
                                    console.error('No transcription returned');
                                }
                            } catch (err) {
                                this.hideTypingIndicator();
                                console.error("Error sending audio message:", err);
                                alert('Error processing voice message. Please try again.');
                            }
                        };
                    } catch (error) {
                        this.hideTypingIndicator();
                        console.error('Error sending audio message:', error);
                        alert('Error processing voice message. Please try again.');
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
            },
            beforeUnmount() {
                if (this.isRecording) {
                    this.stopRecording();
                }
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
