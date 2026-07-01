/*
 * Provider-agnostic soft phone client.
 *
 * Connects to the Telephony SignalR hub and drives the floating soft phone UI. The widget can be
 * dragged, remembers its position and open state, reflects the provider/connection status, shows a
 * call history, and reaches the same provider implementation regardless of which telephony provider
 * is configured server-side.
 */
(function () {
    'use strict';

    // Must match the CrestApps.OrchardCore.Telephony.Models.TelephonyCapabilities flags enum.
    var CAPABILITIES = {
        Dial: 1,
        Hangup: 1 << 1,
        Hold: 1 << 2,
        Resume: 1 << 3,
        Mute: 1 << 4,
        Transfer: 1 << 5,
        Merge: 1 << 6,
        SendDigits: 1 << 7,
        ReceiveCalls: 1 << 8
    };

    var STATE_NAMES = ['Idle', 'Connecting', 'Ringing', 'Connected', 'OnHold', 'Disconnected', 'Failed'];

    function normalizeState(state) {
        if (typeof state === 'number') {
            return STATE_NAMES[state] || 'Idle';
        }

        if (typeof state === 'string' && state.length) {
            return state;
        }

        return 'Idle';
    }

    function isActive(stateName) {
        return stateName === 'Connecting' || stateName === 'Ringing' || stateName === 'Connected' || stateName === 'OnHold';
    }

    function parseConfig(rootElement) {
        var raw = rootElement.getAttribute('data-config');

        if (!raw) {
            return { hubUrl: '', capabilities: 0, strings: {} };
        }

        try {
            return JSON.parse(raw);
        } catch (e) {
            return { hubUrl: '', capabilities: 0, strings: {} };
        }
    }

    function escapeHtml(value) {
        var element = document.createElement('div');
        element.textContent = value == null ? '' : String(value);

        return element.innerHTML;
    }

    function clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }

    function isFiniteNumber(value) {
        return typeof value === 'number' && isFinite(value);
    }

    function createSoftPhone(rootElement, options) {
        options = options || {};

        var config = parseConfig(rootElement);
        var strings = config.strings || {};
        var capabilities = config.capabilities || 0;
        var storageKey = (config.storageKey || 'telephony-soft-phone') + '-layout';

        var signalRFactory = options.signalRFactory || (typeof signalR !== 'undefined' ? signalR : null);

        var dom = {
            toggle: rootElement.querySelector('[data-telephony-toggle]'),
            toggleIcon: rootElement.querySelector('[data-telephony-toggle-icon]'),
            panel: rootElement.querySelector('[data-telephony-panel]'),
            dragHandle: rootElement.querySelector('[data-telephony-drag-handle]'),
            close: rootElement.querySelector('[data-telephony-close]'),
            status: rootElement.querySelector('[data-telephony-status]'),
            number: rootElement.querySelector('[data-telephony-number]'),
            peer: rootElement.querySelector('[data-telephony-peer]'),
            error: rootElement.querySelector('[data-telephony-error]'),
            keys: Array.prototype.slice.call(rootElement.querySelectorAll('[data-telephony-key]')),
            dial: rootElement.querySelector('[data-telephony-dial]'),
            hold: rootElement.querySelector('[data-telephony-hold]'),
            resume: rootElement.querySelector('[data-telephony-resume]'),
            mute: rootElement.querySelector('[data-telephony-mute]'),
            unmute: rootElement.querySelector('[data-telephony-unmute]'),
            transfer: rootElement.querySelector('[data-telephony-transfer]'),
            merge: rootElement.querySelector('[data-telephony-merge]'),
            hangup: rootElement.querySelector('[data-telephony-hangup]'),
            connectPanel: rootElement.querySelector('[data-telephony-connect-panel]'),
            connect: rootElement.querySelector('[data-telephony-connect]'),
            unavailable: rootElement.querySelector('[data-telephony-unavailable]'),
            unavailableText: rootElement.querySelector('[data-telephony-unavailable-text]'),
            keypadView: rootElement.querySelector('[data-telephony-view="keypad"]'),
            history: rootElement.querySelector('[data-telephony-history]'),
            historyList: rootElement.querySelector('[data-telephony-history-list]'),
            footer: rootElement.querySelector('[data-telephony-footer]'),
            tabs: Array.prototype.slice.call(rootElement.querySelectorAll('[data-telephony-tab]'))
        };

        var connection = null;
        var currentCall = null;
        var requiresAuthentication = false;
        var isConnected = false;
        var isAvailable = false;
        var authenticationScheme = null;
        var activeTab = 'keypad';
        var suppressToggleClick = false;

        function has(capability) {
            return (capabilities & capability) === capability;
        }

        function show(element, visible) {
            if (element) {
                element.hidden = !visible;
            }
        }

        function setStatus(text) {
            if (dom.status) {
                dom.status.textContent = text;
            }
        }

        function showError(message) {
            if (!dom.error) {
                return;
            }

            if (message) {
                dom.error.textContent = message;
                dom.error.hidden = false;
            } else {
                dom.error.textContent = '';
                dom.error.hidden = true;
            }
        }

        function statusTextForState(stateName) {
            var key = stateName.charAt(0).toLowerCase() + stateName.slice(1);

            return strings[key] || stateName;
        }

        // ---- Layout persistence and dragging ----

        function loadLayout() {
            try {
                return JSON.parse(localStorage.getItem(storageKey)) || {};
            } catch (e) {
                return {};
            }
        }

        function saveLayout(patch) {
            try {
                var layout = loadLayout();
                Object.assign(layout, patch);
                localStorage.setItem(storageKey, JSON.stringify(layout));
            } catch (e) {
                // Ignore storage errors (for example private browsing).
            }
        }

        function applyRootPosition(left, top) {
            rootElement.style.left = left + 'px';
            rootElement.style.top = top + 'px';
            rootElement.style.right = 'auto';
            rootElement.style.bottom = 'auto';
        }

        function getAvailablePositionRange() {
            var toggleRect = rootElement.getBoundingClientRect();
            var toggleWidth = toggleRect.width || 56;
            var toggleHeight = toggleRect.height || 56;
            var margin = 8;

            // Keep the toggle on screen so the widget can be dragged to any edge, including the far
            // right and over other widgets such as the AI chat widget.
            var maxLeft = Math.max(margin, window.innerWidth - toggleWidth - margin);
            var maxTop = Math.max(margin, window.innerHeight - toggleHeight - margin);
            var minLeft = margin;
            var minTop = margin;

            if (dom.panel && !dom.panel.hidden) {
                var panelRect = dom.panel.getBoundingClientRect();
                var panelWidth = panelRect.width || toggleWidth;
                var panelHeight = panelRect.height || 0;

                // The panel is anchored to the right of the toggle and floats above it, so it extends
                // to the left and up. Keep the panel within the viewport so its header stays grabbable.
                minLeft = Math.min(maxLeft, Math.max(margin, panelWidth - toggleWidth + margin));
                minTop = Math.min(maxTop, panelHeight + (2.5 * margin));
            }

            return {
                minLeft: minLeft,
                minTop: minTop,
                maxLeft: maxLeft,
                maxTop: maxTop
            };
        }

        function clampPosition(left, top) {
            var range = getAvailablePositionRange();

            return {
                left: clamp(left, range.minLeft, range.maxLeft),
                top: clamp(top, range.minTop, range.maxTop)
            };
        }

        function createStoredPosition(left, top) {
            var range = getAvailablePositionRange();
            var leftSpan = Math.max(0, range.maxLeft - range.minLeft);
            var topSpan = Math.max(0, range.maxTop - range.minTop);

            return {
                left: left,
                top: top,
                leftRatio: leftSpan === 0 ? 0 : (left - range.minLeft) / leftSpan,
                topRatio: topSpan === 0 ? 0 : (top - range.minTop) / topSpan
            };
        }

        function resolveStoredPosition(storedPosition) {
            if (!storedPosition) {
                return null;
            }

            var range = getAvailablePositionRange();
            var left = Number(storedPosition.left);
            var top = Number(storedPosition.top);
            var leftRatio = Number(storedPosition.leftRatio);
            var topRatio = Number(storedPosition.topRatio);

            if (Number.isFinite(leftRatio)) {
                left = range.minLeft + Math.max(0, range.maxLeft - range.minLeft) * leftRatio;
            }

            if (Number.isFinite(topRatio)) {
                top = range.minTop + Math.max(0, range.maxTop - range.minTop) * topRatio;
            }

            if (!Number.isFinite(left) || !Number.isFinite(top)) {
                return null;
            }

            return clampPosition(left, top);
        }

        function persistPosition() {
            var rect = rootElement.getBoundingClientRect();

            saveLayout({
                position: createStoredPosition(rect.left, rect.top)
            });
        }

        function applyDefaultPosition() {
            // Place the soft phone beside the AI chat widget, when present, so they do not overlap.
            var chatToggle = document.querySelector('.ai-chat-widget-toggle');

            if (!chatToggle) {
                return;
            }

            var chatRect = chatToggle.getBoundingClientRect();
            var size = rootElement.getBoundingClientRect();
            var width = size.width || 56;
            var left = chatRect.left - width - 14;

            if (left < 8) {
                left = chatRect.right + 14;
            }

            var position = clampPosition(left, chatRect.top);
            applyRootPosition(position.left, position.top);
        }

        function restoreLayout() {
            var layout = loadLayout();

            if (layout.open && dom.panel) {
                dom.panel.hidden = false;
            }

            if (layout.position && isFiniteNumber(Number(layout.position.left))) {
                var position = resolveStoredPosition(layout.position);

                if (position) {
                    applyRootPosition(position.left, position.top);
                }
            } else {
                applyDefaultPosition();
            }
        }

        function restorePosition() {
            var layout = loadLayout();

            if (layout.position) {
                var storedPosition = resolveStoredPosition(layout.position);

                if (storedPosition) {
                    applyRootPosition(storedPosition.left, storedPosition.top);

                    return;
                }
            }

            if (rootElement.style.left) {
                var rect = rootElement.getBoundingClientRect();
                var position = clampPosition(rect.left, rect.top);
                applyRootPosition(position.left, position.top);
            }
        }

        function attachDrag(handle, dragOptions) {
            if (!handle) {
                return;
            }

            dragOptions = dragOptions || {};
            var pointerId = null;
            var startX = 0;
            var startY = 0;
            var startLeft = 0;
            var startTop = 0;
            var dragged = false;

            function onMove(event) {
                if (pointerId === null || event.pointerId !== pointerId) {
                    return;
                }

                var deltaX = event.clientX - startX;
                var deltaY = event.clientY - startY;

                if (!dragged && Math.hypot(deltaX, deltaY) < 4) {
                    return;
                }

                dragged = true;
                var position = clampPosition(startLeft + deltaX, startTop + deltaY);
                applyRootPosition(position.left, position.top);
            }

            function onUp() {
                if (pointerId === null) {
                    return;
                }

                document.removeEventListener('pointermove', onMove);
                document.removeEventListener('pointerup', onUp);
                document.removeEventListener('pointercancel', onUp);
                rootElement.classList.remove('telephony-soft-phone--dragging');
                pointerId = null;

                if (dragged) {
                    persistPosition();

                    if (dragOptions.suppressClick) {
                        suppressToggleClick = true;
                    }
                }
            }

            handle.addEventListener('pointerdown', function (event) {
                if (event.button !== 0) {
                    return;
                }

                if (dragOptions.ignoreButtons && event.target.closest('button, a, input, textarea, select')) {
                    return;
                }

                var rect = rootElement.getBoundingClientRect();
                applyRootPosition(rect.left, rect.top);
                pointerId = event.pointerId;
                dragged = false;
                startX = event.clientX;
                startY = event.clientY;
                startLeft = rect.left;
                startTop = rect.top;
                rootElement.classList.add('telephony-soft-phone--dragging');
                document.addEventListener('pointermove', onMove);
                document.addEventListener('pointerup', onUp);
                document.addEventListener('pointercancel', onUp);
            });
        }

        // ---- Rendering ----

        function updateTabs() {
            dom.tabs.forEach(function (tab) {
                var selected = tab.getAttribute('data-telephony-tab') === activeTab;
                tab.classList.toggle('is-active', selected);
                tab.setAttribute('aria-selected', selected ? 'true' : 'false');
            });
        }

        function setActiveTab(tab) {
            activeTab = tab;
            render();

            if (tab === 'history') {
                loadHistory();
            }
        }

        function render() {
            var stateName = currentCall ? normalizeState(currentCall.state) : 'Idle';
            var active = isActive(stateName);

            rootElement.classList.toggle('telephony-soft-phone--in-call', active);

            if (dom.toggleIcon) {
                dom.toggleIcon.className = active ? 'fa-solid fa-phone-slash' : 'fa-solid fa-phone';
            }

            var notAvailable = !isAvailable;
            var needsConnect = isAvailable && requiresAuthentication && !isConnected;

            // Unavailable: no provider configured. Hide the body and the tab footer.
            if (notAvailable && !active) {
                if (dom.unavailableText) {
                    dom.unavailableText.textContent = strings.notConfigured || 'No telephony provider is configured.';
                }

                show(dom.unavailable, true);
                show(dom.connectPanel, false);
                show(dom.keypadView, false);
                show(dom.history, false);
                show(dom.footer, false);
                setStatus(strings.notReady || 'Not Ready');

                return;
            }

            show(dom.unavailable, false);

            // Needs a per-user connection (for example OAuth). Hide the body and the tab footer.
            if (needsConnect && !active) {
                show(dom.connectPanel, true);
                show(dom.keypadView, false);
                show(dom.history, false);
                show(dom.footer, false);
                setStatus(strings.notConnected || 'Not connected');

                return;
            }

            show(dom.connectPanel, false);

            // Operating state: show the footer tabs and the selected view (keypad or recent calls).
            show(dom.footer, true);
            updateTabs();

            var showHistory = activeTab === 'history';
            show(dom.history, showHistory);
            show(dom.keypadView, !showHistory);

            setStatus(currentCall ? statusTextForState(stateName) : (strings.idle || 'Ready'));

            if (dom.peer) {
                dom.peer.textContent = active && currentCall ? (currentCall.to || currentCall.from || '') : '';
            }

            show(dom.dial, !active);
            show(dom.hangup, active && has(CAPABILITIES.Hangup));
            show(dom.hold, active && stateName === 'Connected' && has(CAPABILITIES.Hold));
            show(dom.resume, active && stateName === 'OnHold' && has(CAPABILITIES.Resume));

            var muted = currentCall && currentCall.isMuted;
            show(dom.mute, active && !muted && has(CAPABILITIES.Mute));
            show(dom.unmute, active && muted && has(CAPABILITIES.Mute));

            show(dom.transfer, active && has(CAPABILITIES.Transfer));
            show(dom.merge, active && has(CAPABILITIES.Merge));

            if (dom.number) {
                dom.number.disabled = active;
            }
        }

        // ---- Call operations ----

        function applyResult(result) {
            if (!result) {
                return false;
            }

            if (result.succeeded === false) {
                showError(result.error || (strings.failed || 'Call failed'));

                return false;
            }

            showError(null);

            if (result.call) {
                currentCall = result.call;

                if (normalizeState(currentCall.state) === 'Disconnected' || normalizeState(currentCall.state) === 'Failed') {
                    currentCall = null;
                }
            }

            render();

            return true;
        }

        function invoke(method, payload) {
            if (!connection) {
                return Promise.reject(new Error('Not connected.'));
            }

            return connection.invoke(method, payload).then(function (result) {
                applyResult(result);

                return result;
            }).catch(function (error) {
                showError(error && error.message ? error.message : String(error));

                throw error;
            });
        }

        function currentCallId() {
            return currentCall ? currentCall.callId : null;
        }

        function dial() {
            var number = dom.number ? dom.number.value.trim() : '';

            if (!number) {
                showError(strings.invalidNumber || 'Enter a phone number to call.');

                return;
            }

            invoke('Dial', { to: number });
        }

        function dialNumber(number) {
            if (!number) {
                return;
            }

            activeTab = 'keypad';
            togglePanel(true);

            if (dom.number) {
                dom.number.value = number;
            }

            invoke('Dial', { to: number });
        }

        function hangup() {
            var id = currentCallId();

            if (id) {
                invoke('Hangup', { callId: id });
            }
        }

        function hold() {
            var id = currentCallId();

            if (id) {
                invoke('Hold', { callId: id });
            }
        }

        function resume() {
            var id = currentCallId();

            if (id) {
                invoke('Resume', { callId: id });
            }
        }

        function mute() {
            var id = currentCallId();

            if (id) {
                invoke('Mute', { callId: id });
            }
        }

        function unmute() {
            var id = currentCallId();

            if (id) {
                invoke('Unmute', { callId: id });
            }
        }

        function transfer() {
            var id = currentCallId();

            if (!id) {
                return;
            }

            var destination = window.prompt(strings.transferPrompt || 'Transfer to number');

            if (destination) {
                invoke('Transfer', { callId: id, to: destination, mode: 0 });
            }
        }

        function merge() {
            var id = currentCallId();

            if (!id) {
                return;
            }

            var secondary = window.prompt(strings.mergePrompt || 'Second call id to merge');

            if (secondary) {
                invoke('Merge', { primaryCallId: id, secondaryCallId: secondary });
            }
        }

        function pressKey(value) {
            var stateName = currentCall ? normalizeState(currentCall.state) : 'Idle';

            if (stateName === 'Connected' && has(CAPABILITIES.SendDigits)) {
                invoke('SendDigits', { callId: currentCallId(), digits: value });
            } else if (!isActive(stateName) && dom.number) {
                dom.number.value += value;
            }
        }

        function togglePanel(open) {
            if (!dom.panel) {
                return;
            }

            var shouldOpen = typeof open === 'boolean' ? open : dom.panel.hidden;
            dom.panel.hidden = !shouldOpen;
            saveLayout({ open: shouldOpen });
            restorePosition();
            render();
        }

        // ---- Connection status and authentication ----

        function refreshConnectionStatus() {
            if (!connection) {
                return Promise.resolve();
            }

            return connection.invoke('GetConnectionStatus').then(function (status) {
                if (status) {
                    isAvailable = !!status.isAvailable;
                    requiresAuthentication = !!status.requiresAuthentication;
                    isConnected = !!status.isConnected;
                    authenticationScheme = status.authenticationScheme || 'oauth2';
                    render();
                }
            }).catch(function () {
                // Leave the default unavailable state when the hub call fails.
            });
        }

        function refreshCapabilities() {
            if (!connection) {
                return Promise.resolve();
            }

            return connection.invoke('GetCapabilities').then(function (value) {
                if (typeof value === 'number') {
                    capabilities = value;
                    render();
                }
            }).catch(function () {
                // Keep the capabilities provided in the configuration when the hub call fails.
            });
        }

        function startOAuth() {
            if (!config.connectUrl) {
                return;
            }

            var separator = config.connectUrl.indexOf('?') >= 0 ? '&' : '?';
            var url = config.connectUrl + separator + 'returnUrl=' + encodeURIComponent(window.location.pathname);
            var popup = window.open(url, 'telephony-oauth', 'width=520,height=640');

            if (!popup) {
                window.location.href = url;
            }
        }

        function handleConnect() {
            var handlers = window.telephonySoftPhone && window.telephonySoftPhone.authHandlers;
            var handler = handlers && (handlers[authenticationScheme] || handlers.oauth2);

            var context = {
                scheme: authenticationScheme,
                connectUrl: config.connectUrl,
                startOAuth: startOAuth,
                refreshStatus: refreshConnectionStatus
            };

            if (typeof handler === 'function') {
                handler(context);
            } else {
                startOAuth();
            }
        }

        function onOAuthMessage(event) {
            if (event.origin !== window.location.origin || !event.data || event.data.type !== 'telephony-oauth') {
                return;
            }

            if (event.data.success) {
                refreshConnectionStatus();
            }
        }

        // ---- History ----

        function loadHistory() {
            if (!connection) {
                renderHistory([]);

                return;
            }

            connection.invoke('GetInteractions', 50).then(function (items) {
                renderHistory(items || []);
            }).catch(function () {
                renderHistory([]);
            });
        }

        function isInbound(interaction) {
            return interaction.direction === 1 || interaction.direction === 'Inbound';
        }

        function isMissed(interaction) {
            return interaction.outcome === 2 || interaction.outcome === 'Missed' ||
                interaction.outcome === 3 || interaction.outcome === 'Rejected';
        }

        function isInProgress(interaction) {
            return interaction.outcome === 0 || interaction.outcome === 'InProgress';
        }

        function formatTime(value) {
            try {
                var date = new Date(value);

                return isNaN(date.getTime()) ? '' : date.toLocaleString();
            } catch (e) {
                return '';
            }
        }

        function renderHistory(items) {
            if (!dom.historyList) {
                return;
            }

            if (!items.length) {
                dom.historyList.innerHTML = '<div class="telephony-soft-phone__history-empty">' +
                    escapeHtml(strings.noInteractions || 'No recent calls.') + '</div>';

                return;
            }

            dom.historyList.innerHTML = items.map(function (interaction) {
                var inbound = isInbound(interaction);
                var missed = isMissed(interaction);
                var inProgress = isInProgress(interaction);
                var icon = inbound ? 'fa-arrow-down-left' : 'fa-arrow-up-right';
                var number = inbound ? (interaction.from || '') : (interaction.to || '');
                var label = missed ? (strings.missed || 'Missed') : (inbound ? (strings.incoming || 'Incoming') : (strings.outgoing || 'Outgoing'));
                var time = formatTime(interaction.startedUtc);
                var cls = 'telephony-soft-phone__history-item' +
                    (missed ? ' telephony-soft-phone__history-item--missed' : '') +
                    (inProgress ? ' telephony-soft-phone__history-item--active' : '');

                var meta = escapeHtml(label) +
                    (inProgress ? ' \u2022 ' + escapeHtml(strings.inProgress || 'In progress') : '') +
                    (time ? ' \u2022 ' + escapeHtml(time) : '');

                return '<button type="button" class="' + cls + '" data-telephony-history-number="' + escapeHtml(number) + '">' +
                    '<span class="telephony-soft-phone__history-dir"><i class="fa-solid ' + icon + '"></i></span>' +
                    '<span class="telephony-soft-phone__history-body">' +
                    '<span class="telephony-soft-phone__history-number">' + escapeHtml(number || label) + '</span>' +
                    '<span class="telephony-soft-phone__history-meta">' + meta + '</span>' +
                    '</span></button>';
            }).join('');

            Array.prototype.forEach.call(dom.historyList.querySelectorAll('[data-telephony-history-number]'), function (item) {
                item.addEventListener('click', function () {
                    var number = item.getAttribute('data-telephony-history-number');

                    if (number) {
                        activeTab = 'keypad';
                        dialNumber(number);
                    }
                });
            });
        }

        // ---- SignalR ----

        function registerClientCallbacks() {
            if (!connection) {
                return;
            }

            connection.on('CallStateChanged', function (call) {
                currentCall = call;

                if (normalizeState(call.state) === 'Disconnected' || normalizeState(call.state) === 'Failed') {
                    currentCall = null;
                }

                render();
            });

            connection.on('IncomingCall', function (call) {
                currentCall = call;
                activeTab = 'keypad';
                togglePanel(true);
                render();
            });

            connection.on('ReceiveError', function (message) {
                showError(message);
            });

            connection.on('CredentialsIssued', function () { });

            connection.onclose(function () {
                setStatus(strings.disconnectedHub || 'Disconnected');
            });
        }

        function connect() {
            if (!signalRFactory || !config.hubUrl) {
                render();

                return Promise.resolve();
            }

            connection = new signalRFactory.HubConnectionBuilder()
                .withUrl(config.hubUrl)
                .withAutomaticReconnect()
                .build();

            registerClientCallbacks();

            return connection.start().then(function () {
                showError(null);

                return Promise.all([refreshCapabilities(), refreshConnectionStatus()]);
            }).then(function () {
                render();
            }).catch(function (error) {
                showError(error && error.message ? error.message : String(error));
            });
        }

        function bindEvents() {
            if (dom.toggle) {
                dom.toggle.addEventListener('click', function () {
                    if (suppressToggleClick) {
                        suppressToggleClick = false;

                        return;
                    }

                    togglePanel();
                });
            }

            if (dom.close) {
                dom.close.addEventListener('click', function () { togglePanel(false); });
            }

            dom.tabs.forEach(function (tab) {
                tab.addEventListener('click', function () {
                    setActiveTab(tab.getAttribute('data-telephony-tab'));
                });
            });

            if (dom.dial) {
                dom.dial.addEventListener('click', dial);
            }

            if (dom.hangup) {
                dom.hangup.addEventListener('click', hangup);
            }

            if (dom.hold) {
                dom.hold.addEventListener('click', hold);
            }

            if (dom.resume) {
                dom.resume.addEventListener('click', resume);
            }

            if (dom.mute) {
                dom.mute.addEventListener('click', mute);
            }

            if (dom.unmute) {
                dom.unmute.addEventListener('click', unmute);
            }

            if (dom.transfer) {
                dom.transfer.addEventListener('click', transfer);
            }

            if (dom.merge) {
                dom.merge.addEventListener('click', merge);
            }

            if (dom.connect) {
                dom.connect.addEventListener('click', handleConnect);
            }

            dom.keys.forEach(function (key) {
                key.addEventListener('click', function () {
                    pressKey(key.getAttribute('data-telephony-key'));
                });
            });

            attachDrag(dom.dragHandle, { ignoreButtons: true, suppressClick: false });
            attachDrag(dom.toggle, { ignoreButtons: false, suppressClick: true });

            window.addEventListener('message', onOAuthMessage);
            window.addEventListener('resize', restorePosition);
        }

        bindEvents();
        restoreLayout();
        render();
        rootElement.style.visibility = '';

        var startPromise = connect();

        return {
            element: rootElement,
            config: config,
            dial: dial,
            dialNumber: dialNumber,
            hangup: hangup,
            hold: hold,
            resume: resume,
            mute: mute,
            unmute: unmute,
            transfer: transfer,
            merge: merge,
            pressKey: pressKey,
            togglePanel: togglePanel,
            open: function () { togglePanel(true); },
            getCurrentCall: function () { return currentCall; },
            getConnection: function () { return connection; },
            started: startPromise
        };
    }

    function initializeAll() {
        var elements = document.querySelectorAll('#telephony-soft-phone, .telephony-soft-phone');

        Array.prototype.forEach.call(elements, function (element) {
            if (!element.__telephonySoftPhone) {
                element.__telephonySoftPhone = createSoftPhone(element);
            }
        });
    }

    function getInstance() {
        var element = document.querySelector('#telephony-soft-phone, .telephony-soft-phone');

        return element ? element.__telephonySoftPhone : null;
    }

    window.telephonySoftPhone = {
        create: createSoftPhone,
        initializeAll: initializeAll,
        getInstance: getInstance,
        // Authentication handlers keyed by scheme. Providers using a different per-user authentication
        // scenario can register their own handler so the widget remains extensible.
        authHandlers: {
            oauth2: function (context) {
                context.startOAuth();
            }
        },
        dial: function (number) {
            var instance = getInstance();

            if (instance) {
                instance.dialNumber(number);
            }
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeAll);
    } else {
        initializeAll();
    }
})();
