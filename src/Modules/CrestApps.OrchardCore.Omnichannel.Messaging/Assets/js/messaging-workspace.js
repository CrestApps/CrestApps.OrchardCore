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
    var readUrl = workspace.getAttribute('data-read-url');
    var conversationUrlTemplate = workspace.getAttribute('data-conversation-url');
    var availabilityUrl = workspace.getAttribute('data-availability-url');
    var newMessageText = workspace.getAttribute('data-new-message-text');
    var viewText = workspace.getAttribute('data-view-text');
    var transferTexts = {
        someone: workspace.getAttribute('data-someone-text'),
        toYou: workspace.getAttribute('data-transferred-to-you-text'),
        toQueue: workspace.getAttribute('data-transferred-to-queue-text'),
        toYourQueue: workspace.getAttribute('data-transferred-to-your-queue-text'),
        away: workspace.getAttribute('data-transferred-away-text'),
    };

    // The same event can reach the page through two of the agent's groups (a transfer goes to the recipient and to the
    // queue they serve); it is announced once. The list and the thread still catch up on every copy.
    var recent = messaging.createRecentNotifications(5000);

    // The count on the Messaging menu item, which this page carries like every admin page.
    var attentionBadge = messaging.createAttentionBadge(workspace.getAttribute('data-attention-url'));

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

        // The starred customers above the list narrow with the same search, and their row hides when none match.
        var favorites = list.querySelector('[data-inbox-favorites]');

        if (favorites) {
            var favoritesVisible = 0;

            favorites.querySelectorAll('[data-favorite-key]').forEach(function (favorite) {
                var match = messaging.rowMatchesFilter(favorite.getAttribute('data-filter-value') + ' ' + favorite.textContent, term);

                favorite.classList.toggle('d-none', !match);

                if (match) { favoritesVisible++; }
            });

            favorites.classList.toggle('d-none', favoritesVisible === 0);
        }

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
            markRead();
        }
    }

    // The messages that arrived while the agent was away were put on screen but left unread, so the menu could count
    // them; now they are in front of the agent, the conversation is read.
    function markRead() {
        if (!readUrl) { return; }

        fetch(readUrl, {
            method: 'POST',
            headers: { 'RequestVerificationToken': antiforgeryToken(), 'X-Requested-With': 'XMLHttpRequest' },
            credentials: 'same-origin',
        })
            .then(function () {
                refreshInbox();
                attentionBadge.schedule();
            })
            .catch(function () { /* opening the conversation again reads it */ });
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

        // Only a thread in front of the agent is read by the poll. One in a background tab, or scrolled up, keeps its
        // new messages unread until the agent is back at the bottom of it.
        var seen = messaging.isThreadInView(document.hidden, isPinnedToBottom());
        var url = messagesUrl + (messagesUrl.indexOf('?') >= 0 ? '&' : '?') + 'afterTicks=' + after + '&seen=' + seen;

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

                var added = messaging.unseenInboundCount(fresh, seen);

                if (added > 0) {
                    setActiveTabBadge(unseen + added);
                }

                // The poll found messages the push did not announce, so the list is stale too, and reading them here
                // may have marked the conversation read, which the menu count reflects.
                refreshInbox();
                attentionBadge.schedule();
            })
            .catch(function () { /* transient network error; the next poll retries */ })
            .finally(function () { pulling = false; });
    }

    if (thread) {
        scrollToBottom();

        // A picture grows its bubble only once it has loaded, after the thread was scrolled. A thread the agent was
        // reading at the bottom stays at the bottom; one they scrolled up in stays where they left it.
        var followBottom = true;

        thread.addEventListener('scroll', function () {
            followBottom = isPinnedToBottom();
        }, { passive: true });

        thread.addEventListener('load', function (event) {
            if (followBottom && event.target && event.target.hasAttribute && event.target.hasAttribute('data-messaging-attachment')) {
                scrollToBottom();
            }
        }, true);

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

    // ---- Pictures ----------------------------------------------------------------------------------------------

    // The pictures waiting to go with the next message, as the agent attached them. They are shrunk to fit only when
    // the message is sent, because the share of the size budget each gets depends on how many there are by then.
    var composerForm = composerBody ? composerBody.form : null;
    var fileInput = composerForm ? composerForm.querySelector('[data-composer-files]') : null;
    var pending = [];

    function composerText(name) {
        return composerForm ? (composerForm.getAttribute(name) || '') : '';
    }

    var mediaError = composerForm ? composerForm.querySelector('[data-composer-media-error]') : null;

    function showMediaError(text) {
        if (!mediaError) { return; }

        mediaError.textContent = text || '';
        mediaError.classList.toggle('d-none', !text);
    }

    function syncRequired() {
        // A picture on its own is a message; only an empty composer with no picture is refused.
        if (composerBody) {
            composerBody.required = pending.length === 0;
        }
    }

    function renderPreviews() {
        var previews = composerForm ? composerForm.querySelector('[data-composer-previews]') : null;

        if (!previews) { return; }

        previews.innerHTML = '';

        pending.forEach(function (item, index) {
            var tile = document.createElement('div');
            tile.className = 'messaging-composer-preview rounded border overflow-hidden';

            var image = document.createElement('img');
            image.src = item.url;
            image.alt = item.file.name || '';
            tile.appendChild(image);

            var remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'btn-close';
            remove.setAttribute('aria-label', composerText('data-remove-text'));
            remove.addEventListener('click', function () {
                URL.revokeObjectURL(item.url);
                pending.splice(index, 1);
                showMediaError('');
                renderPreviews();
            });
            tile.appendChild(remove);

            previews.appendChild(tile);
        });

        previews.classList.toggle('d-none', pending.length === 0);
        syncRequired();
    }

    function addFiles(files) {
        if (!fileInput) { return; }

        var maxCount = parseInt(composerText('data-max-media-count'), 10) || 0;
        var plan = messaging.planAttachments(pending.length, files, maxCount);

        plan.accepted.forEach(function (file) {
            pending.push({ file: file, url: URL.createObjectURL(file) });
        });

        if (plan.notImages.length > 0) {
            showMediaError(composerText('data-not-image-text'));
        } else if (plan.overCount.length > 0) {
            showMediaError(composerText('data-too-many-text'));
        } else {
            showMediaError('');
        }

        renderPreviews();

        if (composerBody) { composerBody.focus(); }
    }

    function loadImage(file) {
        return new Promise(function (resolve, reject) {
            var url = URL.createObjectURL(file);
            var image = new Image();

            image.onload = function () { URL.revokeObjectURL(url); resolve(image); };
            image.onerror = function () { URL.revokeObjectURL(url); reject(new Error('unreadable')); };
            image.src = url;
        });
    }

    function toJpeg(image, step) {
        var size = messaging.fitDimensions(image.naturalWidth, image.naturalHeight, step.maxEdge);
        var canvas = document.createElement('canvas');

        canvas.width = size.width;
        canvas.height = size.height;

        var context = canvas.getContext('2d');

        // A JPEG has no transparency, so a transparent PNG is laid on white rather than black.
        context.fillStyle = '#fff';
        context.fillRect(0, 0, size.width, size.height);
        context.drawImage(image, 0, 0, size.width, size.height);

        return new Promise(function (resolve) {
            canvas.toBlob(resolve, 'image/jpeg', step.quality);
        });
    }

    // Redraws a still picture smaller, step by step, until it fits its share. Resolves to null when even the smallest
    // step is too large.
    function shrink(file, budget) {
        if (!messaging.needsShrinking(file, budget)) {
            return Promise.resolve(budget <= 0 || file.size <= budget ? file : null);
        }

        return loadImage(file).then(function (image) {
            var steps = messaging.shrinkSteps();

            function attempt(index) {
                if (index >= steps.length) { return null; }

                return toJpeg(image, steps[index]).then(function (blob) {
                    if (blob && blob.size <= budget) {
                        var name = (file.name || 'picture').replace(/\.[^.]+$/, '') + '.jpg';

                        return new File([blob], name, { type: 'image/jpeg' });
                    }

                    return attempt(index + 1);
                });
            }

            return attempt(0);
        });
    }

    var sending = false;

    if (composerForm && fileInput) {
        var attachButton = composerForm.querySelector('[data-composer-attach]');

        if (attachButton) {
            attachButton.addEventListener('click', function () { fileInput.click(); });
        }

        fileInput.addEventListener('change', function () {
            // The input only ever carries what is about to be sent; the picker's choice joins the pending list.
            var chosen = Array.prototype.slice.call(fileInput.files || []);
            fileInput.value = '';
            addFiles(chosen);
        });

        // A picture pasted into the message box is attached like a dropped one.
        composerBody.addEventListener('paste', function (event) {
            var files = event.clipboardData ? Array.prototype.slice.call(event.clipboardData.files || []) : [];

            if (files.length > 0) {
                event.preventDefault();
                addFiles(files);
            }
        });

        // Dropping anywhere on the conversation attaches the pictures, with the composer showing where they go.
        var dropTarget = composerForm.closest('.messaging-thread') || composerForm;
        var overlay = composerForm.querySelector('[data-composer-drop-overlay]');
        var dragDepth = 0;

        var carriesFiles = function (event) {
            return event.dataTransfer && Array.prototype.indexOf.call(event.dataTransfer.types || [], 'Files') >= 0;
        };

        var setOverlay = function (visible) {
            if (overlay) { overlay.classList.toggle('show', visible); }
        };

        dropTarget.addEventListener('dragenter', function (event) {
            if (!carriesFiles(event)) { return; }

            event.preventDefault();
            dragDepth++;
            setOverlay(true);
        });

        dropTarget.addEventListener('dragover', function (event) {
            if (!carriesFiles(event)) { return; }

            event.preventDefault();
            event.dataTransfer.dropEffect = 'copy';
        });

        dropTarget.addEventListener('dragleave', function (event) {
            if (!carriesFiles(event)) { return; }

            dragDepth = Math.max(0, dragDepth - 1);

            if (dragDepth === 0) { setOverlay(false); }
        });

        dropTarget.addEventListener('drop', function (event) {
            if (!carriesFiles(event)) { return; }

            event.preventDefault();
            dragDepth = 0;
            setOverlay(false);
            addFiles(Array.prototype.slice.call(event.dataTransfer.files || []));
        });

        // Sending: fit the pictures to the channel's size budget, put them on the form, and post it.
        composerForm.addEventListener('submit', function (event) {
            if (pending.length === 0) { return; }

            event.preventDefault();

            if (sending) { return; }

            sending = true;

            var submitButton = composerForm.querySelector('button[type="submit"]');

            if (submitButton) { submitButton.disabled = true; }

            var maxBytes = parseInt(composerText('data-max-media-bytes'), 10) || 0;
            var budget = messaging.perFileBudget(maxBytes, pending.length);

            Promise.all(pending.map(function (item) {
                return shrink(item.file, budget).catch(function () { return null; }).then(function (fitted) {
                    return { item: item, fitted: fitted };
                });
            })).then(function (results) {
                var tooLarge = results.filter(function (result) { return !result.fitted; });

                if (tooLarge.length > 0 || (maxBytes > 0 && messaging.totalAttachmentSize(results.map(function (r) { return r.fitted; })) > maxBytes)) {
                    var name = tooLarge.length > 0 ? (tooLarge[0].item.file.name || '') : '';

                    showMediaError(composerText('data-too-large-text').replace('{0}', name));
                    sending = false;

                    if (submitButton) { submitButton.disabled = false; }

                    return;
                }

                var transfer = new DataTransfer();

                results.forEach(function (result) { transfer.items.add(result.fitted); });
                fileInput.files = transfer.files;

                // The native submit skips this handler, so the pictures go exactly as fitted.
                composerForm.submit();
            });
        });
    }

    if (composerBody) {
        composerBody.addEventListener('input', updateCounter);

        // Enter sends, Shift+Enter starts a new line, the way every chat surface behaves.
        composerBody.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
                event.preventDefault();

                if (composerBody.value.trim() || pending.length > 0) {
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
                if (recent.isNew(messaging.notificationKey('inbound', notification))) {
                    showToast(
                        notification && notification.contactAddress ? newMessageText + ' — ' + notification.contactAddress : newMessageText,
                        notification ? notification.preview : '',
                        notification && notification.conversationId
                            ? conversationUrlTemplate.replace('__ID__', encodeURIComponent(notification.conversationId))
                            : null);
                }
                break;
        }

        refreshInbox();
        attentionBadge.schedule();
    }

    function conversationHref(conversationId) {
        return conversationId
            ? conversationUrlTemplate.replace('__ID__', encodeURIComponent(conversationId))
            : null;
    }

    // A conversation changed hands: the list always catches up, and a transfer also says who moved it where.
    function onAssigned(notification) {
        var kind = messaging.classifyAssignment(notification, view);
        var announce = kind !== 'refresh' && recent.isNew(messaging.notificationKey(kind, notification));

        switch (announce ? kind : 'refresh') {
            case 'to-me':
            case 'to-queue':
                showToast(messaging.assignmentToastTitle(kind, notification, transferTexts), '', conversationHref(notification.conversationId));
                break;

            case 'away':
                showToast(messaging.assignmentToastTitle(kind, notification, transferTexts), '', null);
                break;
        }

        refreshInbox();
        attentionBadge.schedule();
    }

    attentionBadge.start();

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
                attentionBadge.refresh();
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
