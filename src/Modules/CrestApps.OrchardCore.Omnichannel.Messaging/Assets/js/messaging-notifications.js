/*
 * Messaging notifications on every admin page but the workspace: a toast when a customer writes or a conversation is
 * handed to the user, and the count on the Messaging menu item. Before this only the workspace listened, so an agent
 * reading any other admin page heard nothing until they went back to the inbox.
 *
 * The page listens on the messaging hub passively. It never checks in, and the hub does not record a passive
 * connection as presence, so an agent who is only browsing other admin pages does not count as being at the
 * workspace, and routed distribution does not push conversations at somebody with no inbox on screen.
 */
(function (root) {
    'use strict';

    var messaging = root.CrestAppsMessaging;
    var host = document.querySelector('[data-messaging-notifications]');

    if (!messaging || !host) {
        return;
    }

    // No conversation is on screen here, so a transfer is only news when it is to this agent or to a queue they serve.
    var view = {
        agentId: host.getAttribute('data-agent-id') || null,
        conversationId: null,
    };

    var hubUrl = host.getAttribute('data-hub-url');
    var conversationUrlTemplate = host.getAttribute('data-conversation-url');
    var newMessageText = host.getAttribute('data-new-message-text');
    var newMessageFromText = host.getAttribute('data-new-message-from-text');
    var openText = host.getAttribute('data-open-text');
    var closeText = host.getAttribute('data-close-text');
    var someoneText = host.getAttribute('data-someone-text');
    var transferredToYouText = host.getAttribute('data-transferred-to-you-text');
    var transferredToQueueText = host.getAttribute('data-transferred-to-queue-text');
    var transferredToYourQueueText = host.getAttribute('data-transferred-to-your-queue-text');

    // Long enough to read a line of preview. What a toast announced stays counted on the menu after it goes.
    var toastDelayMs = 8000;

    // A recipient can hear the same event through two of their groups; it is announced once.
    var recent = messaging.createRecentNotifications(5000);
    var badge = messaging.createAttentionBadge(host.getAttribute('data-attention-url'));

    function conversationHref(conversationId) {
        if (!conversationId || !conversationUrlTemplate) {
            return null;
        }

        return messaging.sameOriginUrl(
            conversationUrlTemplate.replace('__ID__', encodeURIComponent(conversationId)),
            root.location.href);
    }

    // ---- Toasts ------------------------------------------------------------------------------------------------

    function showToast(title, body, href) {
        var container = document.querySelector('[data-messaging-toast-container]');

        if (!container) {
            return;
        }

        var toast = document.createElement('div');
        toast.className = 'toast';
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.setAttribute('aria-atomic', 'true');

        var header = document.createElement('div');
        header.className = 'toast-header';

        var icon = document.createElement('i');
        icon.className = 'fa-solid fa-comments text-primary me-2';
        icon.setAttribute('aria-hidden', 'true');
        header.appendChild(icon);

        var strong = document.createElement('strong');
        strong.className = 'me-auto text-truncate';
        strong.textContent = title;
        header.appendChild(strong);

        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'btn-close';
        close.setAttribute('data-bs-dismiss', 'toast');
        close.setAttribute('aria-label', closeText || '');
        header.appendChild(close);

        var bodyElement = document.createElement('div');
        bodyElement.className = 'toast-body';

        if (body) {
            var preview = document.createElement('div');
            preview.className = 'text-break';
            // textContent (not innerHTML) so message content can never inject markup.
            preview.textContent = body;
            bodyElement.appendChild(preview);
        }

        if (href) {
            var link = document.createElement('a');
            link.href = href;
            link.className = 'd-block fw-semibold' + (body ? ' mt-2' : '');
            link.textContent = openText;
            bodyElement.appendChild(link);

            // The whole toast opens the conversation, not only its link; the close button still only closes it.
            toast.style.cursor = 'pointer';
            toast.addEventListener('click', function (event) {
                if (event.target.closest('.btn-close') || event.target.closest('a')) {
                    return;
                }

                root.location.assign(href);
            });
        }

        toast.appendChild(header);
        toast.appendChild(bodyElement);
        container.appendChild(toast);

        if (root.bootstrap && root.bootstrap.Toast) {
            // Bootstrap holds the timer while the pointer or focus is on the toast, so it never vanishes mid-read.
            new root.bootstrap.Toast(toast, { autohide: true, delay: toastDelayMs }).show();
            toast.addEventListener('hidden.bs.toast', function () { toast.remove(); });
        } else {
            toast.classList.add('show');
            close.addEventListener('click', function () { toast.remove(); });
            setTimeout(function () { toast.remove(); }, toastDelayMs);
        }
    }

    // ---- Real time ---------------------------------------------------------------------------------------------

    function onInbound(notification) {
        badge.schedule();

        if (!notification || !recent.isNew(messaging.notificationKey('inbound', notification))) {
            return;
        }

        showToast(
            notification.contactAddress ? messaging.formatText(newMessageFromText, [notification.contactAddress]) : newMessageText,
            notification.preview || '',
            conversationHref(notification.conversationId));
    }

    function onAssigned(notification) {
        badge.schedule();

        var kind = messaging.classifyAssignment(notification, view);

        // A claim, a routed assignment or the agent's own transfer only changes the count.
        if ((kind !== 'to-me' && kind !== 'to-queue') || !recent.isNew(messaging.notificationKey(kind, notification))) {
            return;
        }

        var by = notification.transferredByName || someoneText;

        var title = kind === 'to-me'
            ? messaging.formatText(transferredToYouText, [by])
            : notification.transferredToName
                ? messaging.formatText(transferredToQueueText, [by, notification.transferredToName])
                : messaging.formatText(transferredToYourQueueText, [by]);

        showToast(title, '', conversationHref(notification.conversationId));
    }

    badge.start();

    if (!root.signalR || !hubUrl) {
        return;
    }

    // Tried again a few times when the first connect fails (a restart, a proxy hiccup); once connected, the client's
    // own automatic reconnect takes over. The menu count keeps refreshing on its own either way.
    var startAttempts = 0;
    var maxStartAttempts = 5;
    var startRetryMs = 15000;

    try {
        var connection = new root.signalR.HubConnectionBuilder()
            .withUrl(hubUrl + (hubUrl.indexOf('?') >= 0 ? '&' : '?') + 'passive=1')
            .withAutomaticReconnect()
            // Every admin page runs this, so only a real failure is worth a line in the console.
            .configureLogging(root.signalR.LogLevel ? root.signalR.LogLevel.Error : 4)
            .build();

        connection.on('NewInboundMessage', onInbound);
        connection.on('ConversationAssigned', onAssigned);

        // Delivery receipts and missed first responses are the workspace's to show. They are still received here, and
        // a named no-op keeps the client from warning about an event it has no handler for.
        connection.on('MessageDeliveryUpdated', function () { });
        connection.on('FirstResponseBreached', function () { });

        connection.onreconnected(function () {
            badge.refresh();
        });

        var start = function () {
            startAttempts++;

            connection.start().catch(function () {
                if (startAttempts < maxStartAttempts) {
                    setTimeout(start, startRetryMs);
                }
            });
        };

        start();
    } catch (e) {
        // SignalR unavailable; the menu count still refreshes on its own.
    }
}(typeof globalThis !== 'undefined' ? globalThis : window));
