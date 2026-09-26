/*
 * The messaging workspace page: the customer list, the open customer's conversation and its channel tabs.
 *
 * One SignalR connection keeps all three current. A message for the conversation on screen is appended to it; a message
 * for the same customer on another channel badges that channel's tab; any message reorders the customer list. A light
 * poll backs the connection up, and the connection doubles as the agent's presence heartbeat.
 */
(function (root) {
    'use strict';

    var messaging = root.CrestAppsMessaging;
    var workspace = document.querySelector('[data-messaging-workspace]');

    if (!messaging || !workspace) {
        return;
    }

    var view = {
        conversationId: workspace.getAttribute('data-conversation-id') || null,
        customerKey: workspace.getAttribute('data-customer-key') || null,
        agentId: workspace.getAttribute('data-agent-id') || null,
    };

    var hubUrl = workspace.getAttribute('data-hub-url');
    var inboxUrl = workspace.getAttribute('data-inbox-url');
    var messagesUrl = workspace.getAttribute('data-messages-url');
    var conversationUrlTemplate = workspace.getAttribute('data-conversation-url');
    var availabilityUrl = workspace.getAttribute('data-availability-url');
    var newMessageText = workspace.getAttribute('data-new-message-text');
    var viewText = workspace.getAttribute('data-view-text');
    var someoneText = workspace.getAttribute('data-someone-text');
    var transferredToYouText = workspace.getAttribute('data-transferred-to-you-text');
    var transferredToTeamText = workspace.getAttribute('data-transferred-to-team-text');
    var transferredAwayText = workspace.getAttribute('data-transferred-away-text');

    var list = workspace.querySelector('[data-inbox-list]');
    var search = workspace.querySelector('[data-inbox-search]');
    var thread = workspace.querySelector('[data-thread]');

    function antiforgeryToken() {
        var input = document.querySelector('[data-antiforgery] input[name="__RequestVerificationToken"]');

        return input ? input.value : '';
    }

    // ---- The customer list ------------------------------------------------------------------------------------

    function applySearch() {
        if (!list) { return; }

        var term = search ? search.value : '';
        var rows = list.querySelectorAll('[data-customer-key]');
        var visible = 0;

        rows.forEach(function (row) {
            var match = messaging.rowMatchesFilter(row.getAttribute('data-filter-value') + ' ' + row.textContent, term);

            row.classList.toggle('d-none', !match);

            if (match) { visible++; }
        });

        var empty = list.querySelector('[data-inbox-no-results]');

        if (empty) {
            empty.classList.toggle('d-none', rows.length === 0 || visible > 0);
        }
    }

    if (search) {
        search.addEventListener('input', applySearch);
    }

    var refreshingInbox = null;
    var inboxRefreshQueued = false;

    // Re-reads the list from the server so its order, unread badges and assignment reflect the new message. Refreshes
    // asked for while one is in flight fold into a single trailing one.
    function refreshInbox() {
        if (!list || !inboxUrl) { return; }

        if (refreshingInbox) {
            inboxRefreshQueued = true;

            return;
        }

        refreshingInbox = fetch(inboxUrl, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' })
            .then(function (response) { return response.ok ? response.text() : null; })
            .then(function (html) {
                if (html === null) { return; }

                var scrollTop = list.scrollTop;
                list.innerHTML = html;
                list.scrollTop = scrollTop;
                applySearch();
            })
            .catch(function () { /* the next event or poll retries */ })
            .finally(function () {
                refreshingInbox = null;

                if (inboxRefreshQueued) {
                    inboxRefreshQueued = false;
                    refreshInbox();
                }
            });
    }

    // A slow safety net for a push that never arrived (a dropped connection, a message for a conversation not open): the
    // list catches up on its own, without polling hard.
    if (list) {
        setInterval(function () {
            if (!document.hidden) {
                refreshInbox();
            }
        }, 30000);
    }

    // ---- The channel tabs --------------------------------------------------------------------------------------

    function badgeTab(channel, count) {
        var tab = workspace.querySelector('[data-channel-tab="' + CSS.escape(channel) + '"]');

        if (!tab || tab.classList.contains('active')) { return; }

        var badge = tab.querySelector('[data-channel-tab-badge]');

        if (!badge) { return; }

        badge.textContent = String(count);
        badge.classList.toggle('d-none', count <= 0);
    }

    // The open channel's own tab counts the customer messages that arrived while the agent was scrolled up or away
    // from the page, and clears once they are back at the bottom of the thread.
    var unseen = 0;

    function setActiveTabBadge(count) {
        unseen = count;

        var badge = workspace.querySelector('[data-channel-tab].active [data-channel-tab-badge]');

        if (!badge) { return; }

        badge.textContent = String(count);
        badge.classList.toggle('d-none', count <= 0);
    }

    function clearUnseenWhenVisible() {
        if (unseen > 0 && !document.hidden && isPinnedToBottom()) {
            setActiveTabBadge(0);
        }
    }

    // ---- The open conversation ---------------------------------------------------------------------------------

    function scrollToBottom() {
        if (thread) { thread.scrollTop = thread.scrollHeight; }
    }

    // "Pinned" means the agent is already at (or within a hair of) the bottom. A new message only follows to the bottom
    // when they are, so it never yanks the view away from someone reading earlier messages.
    function isPinnedToBottom() {
        return thread && (thread.scrollHeight - thread.scrollTop - thread.clientHeight) <= 60;
    }

    function bubblesOnScreen() {
        return Array.prototype.map.call(thread.querySelectorAll('.messaging-message'), function (node) {
            return { id: node.getAttribute('data-message-id'), ticks: node.getAttribute('data-created-ticks') };
        });
    }

    var pulling = false;

    function pullThread() {
        if (!thread || !messagesUrl || pulling) { return; }

        pulling = true;

        var after = messaging.maxTicks(bubblesOnScreen().map(function (bubble) { return bubble.ticks; }));
        var url = messagesUrl + (messagesUrl.indexOf('?') >= 0 ? '&' : '?') + 'afterTicks=' + after;

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' })
            .then(function (response) { return response.ok ? response.text() : ''; })
            .then(function (html) {
                if (!html || !html.trim()) { return; }

                var holder = document.createElement('div');
                holder.innerHTML = html;

                var incomingNodes = Array.prototype.slice.call(holder.querySelectorAll('.messaging-message'));
                var fresh = messaging.selectNewBubbles(bubblesOnScreen(), incomingNodes.map(function (node) {
                    return {
                        id: node.getAttribute('data-message-id'),
                        ticks: node.getAttribute('data-created-ticks'),
                        inbound: node.getAttribute('data-inbound') === 'true',
                        node: node,
                    };
                }));

                if (fresh.length === 0) { return; }

                // Decide whether to follow the new message before appending it, based on where the agent is now.
                var pinned = isPinnedToBottom();
                var empty = thread.querySelector('[data-empty]');

                if (empty) { empty.remove(); }

                fresh.forEach(function (bubble) { thread.appendChild(bubble.node); });

                if (pinned) { scrollToBottom(); }

                var added = messaging.unseenInboundCount(fresh, pinned && !document.hidden);

                if (added > 0) {
                    setActiveTabBadge(unseen + added);
                }

                // The poll found messages the push did not announce, so the list is stale too.
                refreshInbox();
            })
            .catch(function () { /* transient network error; the next poll retries */ })
            .finally(function () { pulling = false; });
    }

    if (thread) {
        scrollToBottom();

        thread.addEventListener('scroll', clearUnseenWhenVisible, { passive: true });
        document.addEventListener('visibilitychange', clearUnseenWhenVisible);

        // A light fallback poll catches AI replies, messages sent by other agents, and any missed push.
        setInterval(pullThread, 7000);
    }

    // ---- The composer ------------------------------------------------------------------------------------------

    var composerBody = workspace.querySelector('[data-composer-body]');
    var picker = workspace.querySelector('[data-template-picker]');
    var counter = workspace.querySelector('[data-composer-counter]');

    function updateCounter() {
        if (!counter || !composerBody) { return; }

        counter.textContent = composerBody.value.length + ' / ' + counter.getAttribute('data-max-length');
    }

    if (picker && composerBody) {
        picker.addEventListener('change', function () {
            if (!picker.value) { return; }

            composerBody.value = composerBody.value ? (composerBody.value + '\n' + picker.value) : picker.value;
            composerBody.focus();
            picker.selectedIndex = 0;
            updateCounter();
        });
    }

    if (composerBody) {
        composerBody.addEventListener('input', updateCounter);

        // Enter sends, Shift+Enter starts a new line, the way every chat surface behaves.
        composerBody.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
                event.preventDefault();

                if (composerBody.value.trim()) {
                    composerBody.form.requestSubmit();
                }
            }
        });
    }

    // ---- Availability ------------------------------------------------------------------------------------------

    var toggle = workspace.querySelector('[data-availability-toggle]');

    if (toggle && availabilityUrl) {
        toggle.addEventListener('change', function () {
            fetch(availabilityUrl + '?available=' + (toggle.checked ? 'true' : 'false'), {
                method: 'POST',
                headers: { 'RequestVerificationToken': antiforgeryToken() },
                credentials: 'same-origin',
            }).then(function (response) {
                if (!response.ok) { toggle.checked = !toggle.checked; }
            }).catch(function () { toggle.checked = !toggle.checked; });
        });
    }

    // ---- Transfer ----------------------------------------------------------------------------------------------

    // The dialog is rendered with the conversation but moved to the end of the document, so it stacks above the
    // workspace panes rather than inside them.
    var transferModal = workspace.querySelector('[data-transfer-modal]');

    if (transferModal) {
        document.body.appendChild(transferModal);

        var transferForm = transferModal.querySelector('[data-transfer-form]');
        var transferRequired = transferModal.querySelector('[data-transfer-required]');

        var transferType = function () {
            var checked = transferForm.querySelector('[data-transfer-type]:checked');

            return checked ? checked.value : 'Agent';
        };

        // Only the picker for the kind of target chosen is shown; the other keeps its selection but is not read.
        var showTransferTarget = function () {
            var type = transferType();

            transferForm.querySelectorAll('[data-transfer-target]').forEach(function (section) {
                section.classList.toggle('d-none', section.getAttribute('data-transfer-target') !== type);
            });

            if (transferRequired) { transferRequired.classList.add('d-none'); }
        };

        transferForm.querySelectorAll('[data-transfer-type]').forEach(function (radio) {
            radio.addEventListener('change', showTransferTarget);
        });

        showTransferTarget();

        transferForm.addEventListener('submit', function (event) {
            var inputName = messaging.transferTargetInputName(transferType());
            var chosen = transferForm.querySelector('input[type="hidden"][name="' + inputName + '"]');

            if (!chosen || !chosen.value) {
                event.preventDefault();

                if (transferRequired) { transferRequired.classList.remove('d-none'); }
            }
        });
    }

    // ---- Toasts ------------------------------------------------------------------------------------------------

    function showToast(title, body, href) {
        var container = document.querySelector('[data-toast-container]');

        if (!container) { return; }

        var toast = document.createElement('div');
        toast.className = 'toast';
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.setAttribute('aria-atomic', 'true');

        var header = document.createElement('div');
        header.className = 'toast-header';

        var strong = document.createElement('strong');
        strong.className = 'me-auto';
        strong.textContent = title;
        header.appendChild(strong);

        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'btn-close';
        close.setAttribute('data-bs-dismiss', 'toast');
        header.appendChild(close);

        var bodyElement = document.createElement('div');
        bodyElement.className = 'toast-body';
        // textContent (not innerHTML) so message content can never inject markup.
        bodyElement.textContent = body || '';

        var safeHref = messaging.sameOriginUrl(href, root.location.href);

        if (safeHref) {
            var link = document.createElement('a');
            link.href = safeHref;
            link.className = 'd-block mt-2 fw-semibold';
            link.textContent = viewText;
            bodyElement.appendChild(link);
        }

        toast.appendChild(header);
        toast.appendChild(bodyElement);
        container.appendChild(toast);

        if (root.bootstrap && root.bootstrap.Toast) {
            // A new-message toast stays until the agent closes it, so a notification is never missed while they are
            // looking away.
            new root.bootstrap.Toast(toast, { autohide: false }).show();
            toast.addEventListener('hidden.bs.toast', function () { toast.remove(); });
        } else {
            toast.classList.add('show');
        }
    }

    // ---- Real time ---------------------------------------------------------------------------------------------

    function onInbound(notification) {
        switch (messaging.classifyInbound(notification, view)) {
            case 'thread':
                pullThread();
                break;

            case 'tab':
                badgeTab(notification.channel, messaging.tabBadgeCount(notification));
                break;

            default:
                showToast(
                    notification && notification.contactAddress ? newMessageText + ' — ' + notification.contactAddress : newMessageText,
                    notification ? notification.preview : '',
                    notification && notification.conversationId
                        ? conversationUrlTemplate.replace('__ID__', encodeURIComponent(notification.conversationId))
                        : null);
                break;
        }

        refreshInbox();
    }

    function conversationHref(conversationId) {
        return conversationId
            ? conversationUrlTemplate.replace('__ID__', encodeURIComponent(conversationId))
            : null;
    }

    // A conversation changed hands: the list always catches up, and a transfer also says who moved it where.
    function onAssigned(notification) {
        var by = (notification && notification.transferredByName) || someoneText;
        var to = (notification && notification.transferredToName) || someoneText;

        switch (messaging.classifyAssignment(notification, view)) {
            case 'to-me':
                showToast(messaging.formatText(transferredToYouText, [by]), '', conversationHref(notification.conversationId));
                break;

            case 'to-team':
                showToast(messaging.formatText(transferredToTeamText, [by, to]), '', conversationHref(notification.conversationId));
                break;

            case 'away':
                showToast(messaging.formatText(transferredAwayText, [to]), '', null);
                break;
        }

        refreshInbox();
    }

    if (root.signalR && hubUrl) {
        try {
            var connection = new root.signalR.HubConnectionBuilder()
                .withUrl(hubUrl)
                .withAutomaticReconnect()
                .build();

            connection.on('NewInboundMessage', onInbound);
            connection.on('ConversationAssigned', onAssigned);
            connection.on('MessageDeliveryUpdated', function (notification) {
                if (notification && notification.conversationId === view.conversationId) {
                    pullThread();
                }
            });

            // The connection doubles as presence. While the page is open it checks in on a timer well inside the
            // heartbeat timeout, so a dropped tab stops counting as an agent who can see new work.
            var heartbeatTimer = null;

            var heartbeat = function () {
                connection.invoke('Heartbeat').catch(function () { /* the next tick retries */ });
            };

            var startHeartbeat = function () {
                heartbeat();

                if (heartbeatTimer === null) {
                    heartbeatTimer = setInterval(heartbeat, 30000);
                }
            };

            // After a dropped connection is restored, reconcile anything missed while offline.
            connection.onreconnected(function () {
                startHeartbeat();
                pullThread();
                refreshInbox();
            });

            connection.onclose(function () {
                if (heartbeatTimer !== null) {
                    clearInterval(heartbeatTimer);
                    heartbeatTimer = null;
                }
            });

            connection.start()
                .then(startHeartbeat)
                .catch(function () { /* the fallback poll keeps the thread current */ });
        } catch (e) {
            // SignalR unavailable; the fallback poll keeps the thread current.
        }
    }
}(typeof globalThis !== 'undefined' ? globalThis : window));
