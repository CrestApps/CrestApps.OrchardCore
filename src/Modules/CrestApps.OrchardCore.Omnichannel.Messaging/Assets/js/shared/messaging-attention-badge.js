/*
 * The count on the Messaging > Inbox admin menu item: the unread conversations waiting on the user. The admin menu is on every page,
 * the workspace included, so the workspace and the notifications every other admin page carries both keep it current
 * through this one helper. The number comes from the server, which applies the inbox's own visibility rules, so the
 * page never has to work out what the user may see.
 *
 * Concatenated ahead of the scripts that use it by the module asset pipeline, like messaging-state.js.
 */
(function (root) {
    'use strict';

    var messaging = root.CrestAppsMessaging = root.CrestAppsMessaging || {};

    // The pause before the count is read after a notification. The hub sends the event before the change behind it
    // has committed, so reading at once could still see the old number; a burst of events also folds into one read.
    var settleDelayMs = 1500;

    // A safety net for a notification that never arrived (a dropped connection, a conversation read on another
    // device), kept slow because every admin page runs it.
    var periodicRefreshMs = 60000;

    // Turns a menu group's icon red while any item under it shows a count, and back once none does, so a collapsed
    // menu (icons only) still shows there is something waiting. Every count on the admin menu marks itself with
    // data-admin-menu-attention, whichever module draws it, so this can run after any of them changes.
    function flagMenuGroups(root) {
        var scope = root || document;

        scope.querySelectorAll('[data-admin-menu-attention]').forEach(function (badge) {
            var item = badge.closest('li');
            var group = item && item.parentElement ? item.parentElement.closest('li') : null;

            while (group) {
                // The group's own label (a figure > figcaption in the admin theme), not one of its items' labels.
                var header = Array.prototype.find.call(group.querySelectorAll('.item-label'), function (label) {
                    return label.closest('li') === group;
                });
                var icon = header ? header.querySelector(':scope > .icon') : null;

                if (icon) {
                    icon.classList.toggle('text-danger', !!group.querySelector('[data-admin-menu-attention]:not(.d-none)'));
                }

                group = group.parentElement ? group.parentElement.closest('li') : null;
            }
        });
    }

    function show(count) {
        var value = Math.floor(Number(count) || 0);

        document.querySelectorAll('[data-messaging-attention-badge]').forEach(function (badge) {
            badge.textContent = value > 99 ? '99+' : (value > 0 ? String(value) : '');
            badge.classList.toggle('d-none', value <= 0);
        });

        flagMenuGroups(document);
    }

    // Creates the badge updater for a page.
    //   start()    - reads the count now, then again on a slow timer and whenever the page comes back into view.
    //   refresh()  - reads the count now.
    //   schedule() - reads the count a moment from now; what a notification asks for.
    function createAttentionBadge(url) {
        var timer = null;
        var inFlight = false;
        var queued = false;

        function refresh() {
            if (!url || !document.querySelector('[data-messaging-attention-badge]')) {
                return;
            }

            if (inFlight) {
                queued = true;

                return;
            }

            inFlight = true;

            fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin', cache: 'no-store' })
                .then(function (response) { return response.ok ? response.json() : null; })
                .then(function (data) {
                    if (data && typeof data.count === 'number') {
                        show(data.count);
                    }
                })
                .catch(function () { /* the next notification or the periodic read tries again */ })
                .finally(function () {
                    inFlight = false;

                    if (queued) {
                        queued = false;
                        refresh();
                    }
                });
        }

        function schedule() {
            if (timer !== null) {
                clearTimeout(timer);
            }

            timer = setTimeout(function () {
                timer = null;
                refresh();
            }, settleDelayMs);
        }

        function start() {
            refresh();

            setInterval(function () {
                if (!document.hidden) {
                    refresh();
                }
            }, periodicRefreshMs);

            document.addEventListener('visibilitychange', function () {
                if (!document.hidden) {
                    refresh();
                }
            });
        }

        return { start: start, refresh: refresh, schedule: schedule };
    }

    messaging.createAttentionBadge = createAttentionBadge;
    messaging.flagMenuGroups = flagMenuGroups;
}(typeof globalThis !== 'undefined' ? globalThis : window));
