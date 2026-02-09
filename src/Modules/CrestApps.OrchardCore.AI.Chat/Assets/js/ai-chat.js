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
                    audioStream: null,
                    recordingMessageId: null,
                    audioSubject: null,
                    audioSubjectCompleted: false,
                    audioInvokePromise: null,
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

                    this.connection.on("LoadSession", (data) => {
                        this.initializeSession(data.sessionId, true);
                        this.messages = [];

                        (data.messages ?? []).forEach(msg => {

                            this.addMessage(msg);
                        });

                    });

                    this.connection.on("ReceiveTranscript", (data) => {
                        // Handle real-time transcript updates from the server
                        console.log('Received transcript update:', data);
                        if (data && data.text && this.recordingMessageId !== null) {
                            const message = this.messages[this.recordingMessageId];
                            if (message) {
                                // Append or replace transcript
                                if (message.content === '...' || message.content === '') {
                                    message.content = data.text;
                                } else {
                                    message.content += ' ' + data.text;
                                }
                                message.htmlContent = marked.parse(message.content, { renderer });
                                this.messages[this.recordingMessageId] = message;
                                this.scrollToBottom();
                            }
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
                        // Request audio with constraints for better quality
                        const stream = await navigator.mediaDevices.getUserMedia({
                            audio: {
                                echoCancellation: true,
                                noiseSuppression: true,
                                autoGainControl: true,
                                channelCount: 1,
                                sampleSize: 16,
                                sampleRate: 16000
                            },
                            video: false
                        });

                        this.audioStream = stream;
                        this.recordingMessageId = null;
                        this.audioSubjectCompleted = false;

                        // Check if browser supports audio/ogg;codecs=opus natively
                        const isOggOpusSupported = window.MediaRecorder && window.MediaRecorder.isTypeSupported('audio/ogg;codecs=opus');
                        
                        // OpusMediaRecorder worker options for browsers that need polyfill
                        const workerOptions = {
                            OggOpusEncoderWasmPath: 'https://cdn.jsdelivr.net/npm/opus-media-recorder@latest/OggOpusEncoder.wasm',
                            WebMOpusEncoderWasmPath: 'https://cdn.jsdelivr.net/npm/opus-media-recorder@latest/WebMOpusEncoder.wasm'
                        };

                        // Use OpusMediaRecorder polyfill if native doesn't support ogg/opus
                        const RecorderClass = (isOggOpusSupported || !window.OpusMediaRecorder) ? window.MediaRecorder : window.OpusMediaRecorder;
                        
                        const recorderOptions = {
                            mimeType: 'audio/ogg;codecs=opus',
                            audioBitsPerSecond: 128000
                        };

                        // Create MediaRecorder with appropriate options
                        if (RecorderClass === window.OpusMediaRecorder) {
                            console.log('Using OpusMediaRecorder polyfill for audio/ogg;codecs=opus');
                            this.mediaRecorder = new RecorderClass(stream, recorderOptions, workerOptions);
                        } else if (isOggOpusSupported) {
                            console.log('Using native MediaRecorder with audio/ogg;codecs=opus');
                            this.mediaRecorder = new RecorderClass(stream, recorderOptions);
                        } else {
                            // Fallback to webm if ogg not supported and no polyfill
                            console.log('Falling back to audio/webm');
                            this.mediaRecorder = new RecorderClass(stream, { mimeType: 'audio/webm' });
                        }

                        // Create a SignalR Subject for client-to-server streaming
                        this.audioSubject = new signalR.Subject();
                        this.audioInvokePromise = this.connection.send(
                            "SendAudioChunk",
                            this.getProfileId(),
                            this.getSessionId(),
                            this.audioSubject
                        ).catch(err => {
                            console.error('Error sending audio stream:', err);
                            return { transcript: '', sessionId: this.getSessionId() };
                        });

                        this.mediaRecorder.addEventListener("dataavailable", async (e) => {
                            if (this.audioSubjectCompleted || !this.audioSubject) {
                                return;
                            }
                            
                            if (e.data && e.data.size > 0) {
                                try {
                                    // Convert blob to base64 to send via SignalR
                                    const data = await e.data.arrayBuffer();
                                    const uint8Array = new Uint8Array(data);
                                    const binaryString = uint8Array.reduce((str, byte) => str + String.fromCharCode(byte), '');
                                    const base64 = btoa(binaryString);
                                    
                                    console.log('Sending audio chunk, bytes:', data.byteLength, 'base64 length:', base64.length);
                                    
                                    if (this.audioSubject && !this.audioSubjectCompleted) {
                                        this.audioSubject.next(base64);
                                    }
                                } catch (e) {
                                    console.error('Error encoding audio chunk:', e);
                                }
                            }
                        });

                        this.mediaRecorder.addEventListener("stop", async () => {
                            console.log('MediaRecorder stopped');
                            
                            // Complete the SignalR stream after a short delay to ensure last chunk is sent
                            setTimeout(() => {
                                console.log('Completing audio subject');
                                try {
                                    this.audioSubjectCompleted = true;
                                    this.audioSubject?.complete();
                                } catch (e) {
                                    console.error('Error completing audio subject:', e);
                                }
                            }, 500);

                            try {
                                const result = await this.audioInvokePromise;
                                console.log('Audio invoke result:', result);
                                const finalTranscript = result?.transcript || '';
                                const returnedSessionId = result?.sessionId;
                                if (returnedSessionId) {
                                    this.setSessionId(returnedSessionId);
                                }
                                if (this.recordingMessageId !== null) {
                                    const message = this.messages[this.recordingMessageId];
                                    if (message) {
                                        message.content = finalTranscript || message.content || '';
                                        message.htmlContent = marked.parse(message.content, { renderer });
                                        this.messages[this.recordingMessageId] = message;
                                        this.scrollToBottom();
                                    }
                                }
                            } catch (e) {
                                console.error('Error finalizing audio stream:', e);
                            } finally {
                                this.audioSubject = null;
                                this.audioInvokePromise = null;
                                this.audioSubjectCompleted = false;
                            }

                            // Stop all tracks to release the microphone
                            if (this.audioStream) {
                                this.audioStream.getTracks().forEach(track => track.stop());
                                this.audioStream = null;
                            }

                            // Finalize recording UI
                            await this.finalizeRecording();
                        });

                        this.mediaRecorder.addEventListener("start", () => {
                            console.log('MediaRecorder started');
                        });

                        // Start recording with 1-second timeslice for chunked processing
                        this.mediaRecorder.start(1000);
                        this.isRecording = true;

                        // Show initial transcription message
                        this.recordingMessageId = this.messages.length;
                        this.addMessage({
                            role: 'user',
                            content: '...', // placeholder until transcript arrives
                            isTranscribing: true
                        });

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
                    console.log('stopRecording called, isRecording:', this.isRecording);
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
                async finalizeRecording() {
                    if (this.recordingMessageId !== null) {
                        let message = this.messages[this.recordingMessageId];
                        if (message) {
                            // Remove the transcribing flag
                            delete message.isTranscribing;

                            // If we got transcription, keep it and enable send button
                            if (message.content && message.content !== '...') {
                                this.inputElement.value = message.content;
                                this.prompt = message.content;
                                this.buttonElement.removeAttribute('disabled');
                            } else {
                                // No transcription received, remove the empty message
                                this.messages.splice(this.recordingMessageId, 1);
                            }
                        }
                        this.recordingMessageId = null;
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

                window.addEventListener('beforeunload', this.handleBeforeUnload);
            },
            beforeUnmount() {
                if (this.isRecording) {
                    this.stopRecording();
                }

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
