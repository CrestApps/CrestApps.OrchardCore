(function () {
    'use strict';

    function parseBooleanAttribute(value) {
        if (value === undefined || value === null || value === '') {
            return undefined;
        }

        return value === 'true';
    }

    function parseJsonAttribute(configElement, attributeName, errorMessage) {
        var value = configElement.getAttribute(attributeName);

        if (!value) {
            return undefined;
        }

        try {
            return JSON.parse(value);
        } catch (error) {
            console.error(errorMessage, error);
            return null;
        }
    }

    function getConfigFromDataAttributes(configElement) {
        var dataset = configElement.dataset;

        if (!dataset.signalRHubUrl) {
            return undefined;
        }

        var messages = parseJsonAttribute(
            configElement,
            'data-messages',
            'Failed to parse AI chat messages.'
        );

        if (messages === null) {
            return null;
        }

        var existingDocuments = parseJsonAttribute(
            configElement,
            'data-existing-documents',
            'Failed to parse AI chat existing documents.'
        );

        if (existingDocuments === null) {
            return null;
        }

        return {
            signalRHubUrl: dataset.signalRHubUrl,
            appElementSelector: dataset.appElementSelector,
            chatContainerElementSelector: dataset.chatContainerElementSelector,
            inputElementSelector: dataset.inputElementSelector,
            sendButtonElementSelector: dataset.sendButtonElementSelector,
            placeholderElementSelector: dataset.placeholderElementSelector,
            autoCreateSession: parseBooleanAttribute(dataset.autoCreateSession),
            initialPrompt: dataset.initialPrompt,
            messages: messages || [],
            metricsEnabled: parseBooleanAttribute(dataset.metricsEnabled),
            chatMode: dataset.chatMode,
            micButtonElementSelector: dataset.micButtonElementSelector,
            ttsVoiceName: dataset.ttsVoiceName,
            conversationButtonElementSelector: dataset.conversationButtonElementSelector,
            textToSpeechEnabled: parseBooleanAttribute(dataset.textToSpeechEnabled),
            visionEnabled: parseBooleanAttribute(dataset.visionEnabled),
            isOrchestratorAvailable: parseBooleanAttribute(dataset.isOrchestratorAvailable),
            orchestratorUnavailableMessage: dataset.orchestratorUnavailableMessage,
            sessionDocumentsEnabled: parseBooleanAttribute(dataset.sessionDocumentsEnabled),
            documentBarSelector: dataset.documentBarSelector,
            uploadDocumentUrl: dataset.uploadDocumentUrl,
            removeDocumentUrl: dataset.removeDocumentUrl,
            allowedExtensions: dataset.allowedExtensions,
            supportedExtensionsText: dataset.supportedExtensionsText,
            existingDocuments: existingDocuments || []
        };
    }

    function getConfigFromJsonAttribute(configElement) {
        var rawConfig = configElement.getAttribute('data-config');

        if (!rawConfig) {
            return undefined;
        }

        try {
            return JSON.parse(rawConfig);
        } catch (error) {
            console.error('Failed to parse AI chat config.', error);
            return null;
        }
    }

    function initializeFromElement(configElement) {
        if (!configElement || configElement.dataset.aiChatInitialized === 'true') {
            return null;
        }

        var config = getConfigFromDataAttributes(configElement);

        if (config === null) {
            return null;
        }

        config = config || getConfigFromJsonAttribute(configElement);

        if (!config) {
            return null;
        }

        var app = window.openAIChatManager.initialize(config);

        if (!app) {
            return null;
        }

        if (configElement.dataset.resetSessionOnSend === 'true' &&
            typeof app.sendMessage === 'function' &&
            typeof app.resetSession === 'function') {
            var originalSendMessage = app.sendMessage;

            app.sendMessage = function () {
                app.resetSession();

                if (Array.isArray(app.messages)) {
                    app.messages.splice(0, app.messages.length);
                }

                originalSendMessage.call(app);
            };
        }

        configElement.dataset.aiChatInitialized = 'true';

        return app;
    }

    function initializeAll() {
        document.querySelectorAll('[data-ai-chat-config]').forEach(function (configElement) {
            initializeFromElement(configElement);
        });
    }

    if (document.readyState !== 'loading') {
        initializeAll();
    } else {
        document.addEventListener('DOMContentLoaded', initializeAll, { once: true });
    }
})();
