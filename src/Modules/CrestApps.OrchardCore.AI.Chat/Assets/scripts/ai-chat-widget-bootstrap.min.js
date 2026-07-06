(function () {
    'use strict';

    function parseBooleanAttribute(value) {
        if (value === undefined || value === null || value === '') {
            return undefined;
        }

        return value === 'true';
    }

    function parseNumberAttribute(value) {
        if (value === undefined || value === null || value === '') {
            return undefined;
        }

        var parsedValue = Number(value);

        return Number.isNaN(parsedValue) ? undefined : parsedValue;
    }

    function parseConfigValue(configElement, attributeName, errorMessage) {
        var rawValue = configElement.getAttribute(attributeName);

        if (!rawValue) {
            return undefined;
        }

        try {
            return JSON.parse(rawValue);
        } catch (error) {
            console.error(errorMessage, error);
            return null;
        }
    }

    function getChatConfigFromDataAttributes(configElement) {
        var dataset = configElement.dataset;

        if (!dataset.signalRHubUrl) {
            return undefined;
        }

        var messages = parseConfigValue(
            configElement,
            'data-messages',
            'Failed to parse AI chat widget messages.'
        );

        if (messages === null) {
            return null;
        }

        var existingDocuments = parseConfigValue(
            configElement,
            'data-existing-documents',
            'Failed to parse AI chat widget existing documents.'
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

    function getChatConfigFromJsonAttribute(configElement) {
        return parseConfigValue(
            configElement,
            'data-config',
            'Failed to parse AI chat widget config.'
        );
    }

    function getWidgetConfigFromDataAttributes(configElement) {
        var dataset = configElement.dataset;

        if (!dataset.messageTemplateSelector) {
            return undefined;
        }

        return {
            messageTemplateSelector: dataset.messageTemplateSelector,
            indicatorTemplateSelector: dataset.indicatorTemplateSelector,
            shellSelector: dataset.shellSelector,
            widgetContainerSelector: dataset.widgetContainerSelector,
            toggleButtonSelector: dataset.toggleButtonSelector,
            closeButtonSelector: dataset.closeButtonSelector,
            inputSelector: dataset.inputSelector,
            widgetStateName: dataset.widgetStateName,
            widgetOpenStateKey: dataset.widgetOpenStateKey,
            openStateValue: dataset.openStateValue,
            closedStateValue: dataset.closedStateValue,
            openIconHtml: dataset.openIconHtml,
            closedIconHtml: dataset.closedIconHtml,
            widget: {
                chatWidgetContainer: dataset.widgetChatWidgetContainer,
                chatWidgetStateName: dataset.widgetChatWidgetStateName,
                chatHistorySection: dataset.widgetChatHistorySection,
                closeHistoryButton: dataset.widgetCloseHistoryButton,
                showHistoryButton: dataset.widgetShowHistoryButton,
                newChatButton: dataset.widgetNewChatButton,
                toggleButtonSelector: dataset.widgetToggleButtonSelector,
                resetSizeButtonSelector: dataset.widgetResetSizeButtonSelector,
                dragHandleSelector: dataset.widgetDragHandleSelector,
                enableDragging: parseBooleanAttribute(dataset.widgetEnableDragging),
                enableResizing: parseBooleanAttribute(dataset.widgetEnableResizing),
                persistLayout: parseBooleanAttribute(dataset.widgetPersistLayout),
                minWidth: parseNumberAttribute(dataset.widgetMinWidth),
                minHeight: parseNumberAttribute(dataset.widgetMinHeight)
            }
        };
    }

    function getWidgetConfigFromJsonAttribute(configElement) {
        return parseConfigValue(
            configElement,
            'data-widget-config',
            'Failed to parse AI chat widget config.'
        );
    }

    function initializeFromElement(configElement) {
        if (!configElement || configElement.dataset.aiChatWidgetInitialized === 'true') {
            return null;
        }

        var chatConfig = getChatConfigFromDataAttributes(configElement);

        if (chatConfig === null) {
            return null;
        }

        chatConfig = chatConfig || getChatConfigFromJsonAttribute(configElement);

        var widgetConfig = getWidgetConfigFromDataAttributes(configElement);

        if (widgetConfig === null) {
            return null;
        }

        widgetConfig = widgetConfig || getWidgetConfigFromJsonAttribute(configElement);

        if (!chatConfig || !widgetConfig) {
            return null;
        }

        var app = window.openAIChatWidgetManager.initialize(Object.assign({}, widgetConfig, {
            chatConfig: Object.assign({}, chatConfig, {
                widget: widgetConfig.widget
            })
        }));

        if (!app) {
            return null;
        }

        configElement.dataset.aiChatWidgetInitialized = 'true';

        return app;
    }

    function initializeAll() {
        document.querySelectorAll('[data-ai-chat-widget-config]').forEach(function (configElement) {
            initializeFromElement(configElement);
        });
    }

    if (document.readyState !== 'loading') {
        initializeAll();
    } else {
        document.addEventListener('DOMContentLoaded', initializeAll, { once: true });
    }
})();
