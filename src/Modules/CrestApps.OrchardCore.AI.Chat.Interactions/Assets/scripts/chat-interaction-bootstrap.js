(function () {
    'use strict';

    function ensurePendingChartRenderer() {
        if (typeof window.renderPendingCharts === 'function') {
            return;
        }

        window.renderPendingCharts = function () {
            if (typeof Chart === 'undefined') {
                return;
            }

            var configs = window.__chartConfigs;

            if (!configs) {
                return;
            }

            requestAnimationFrame(function () {
                Object.keys(configs).forEach(function (chartId) {
                    var canvas = document.getElementById(chartId);

                    if (!canvas || canvas.offsetParent === null) {
                        return;
                    }

                    try {
                        if (canvas._chartInstance) {
                            canvas._chartInstance.destroy();
                        }

                        var config = typeof configs[chartId] === 'string'
                            ? JSON.parse(configs[chartId])
                            : configs[chartId];

                        config.options = config.options || {};
                        config.options.responsive = true;
                        config.options.maintainAspectRatio = true;
                        config.options.aspectRatio = config.options.aspectRatio || 4 / 3;
                        canvas._chartInstance = new Chart(canvas, config);
                        delete configs[chartId];
                    } catch (error) {
                        console.error('Error creating chart:', error);
                    }
                });
            });
        };
    }

    function observePendingChartRendering(container) {
        if (!container || container._pendingChartsObserver) {
            return;
        }

        var observer = new MutationObserver(function () {
            window.renderPendingCharts();
        });

        observer.observe(container, {
            childList: true,
            subtree: true
        });

        container._pendingChartsObserver = observer;
    }

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
            'Failed to parse chat interaction messages.'
        );

        if (messages === null) {
            return null;
        }

        return {
            signalRHubUrl: dataset.signalRHubUrl,
            appElementSelector: dataset.appElementSelector,
            chatContainerElementSelector: dataset.chatContainerElementSelector,
            inputElementSelector: dataset.inputElementSelector,
            sendButtonElementSelector: dataset.sendButtonElementSelector,
            placeholderElementSelector: dataset.placeholderElementSelector,
            messages: messages || [],
            untitledText: dataset.untitledText,
            clearHistoryTitle: dataset.clearHistoryTitle,
            clearHistoryMessage: dataset.clearHistoryMessage,
            clearHistoryOkText: dataset.clearHistoryOkText,
            clearHistoryCancelText: dataset.clearHistoryCancelText,
            chatMode: dataset.chatMode,
            micButtonElementSelector: dataset.micButtonElementSelector,
            conversationButtonElementSelector: dataset.conversationButtonElementSelector,
            saveIndicatorElementSelector: dataset.saveIndicatorElementSelector,
            textToSpeechEnabled: parseBooleanAttribute(dataset.textToSpeechEnabled),
            visionEnabled: parseBooleanAttribute(dataset.visionEnabled)
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
            console.error('Failed to parse chat interaction config.', error);
            return null;
        }
    }

    function initializeFromElement(configElement) {
        if (!configElement || configElement.dataset.chatInteractionInitialized === 'true') {
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

        var app = window.chatInteractionManager.initialize(config);

        if (!app) {
            return null;
        }

        ensurePendingChartRenderer();

        if (config.chatContainerElementSelector) {
            observePendingChartRendering(document.querySelector(config.chatContainerElementSelector));
        }

        configElement.dataset.chatInteractionInitialized = 'true';

        return app;
    }

    function initializeAll() {
        document.querySelectorAll('[data-chat-interaction-config]').forEach(function (configElement) {
            initializeFromElement(configElement);
        });
    }

    if (document.readyState !== 'loading') {
        initializeAll();
    } else {
        document.addEventListener('DOMContentLoaded', initializeAll, { once: true });
    }
})();
