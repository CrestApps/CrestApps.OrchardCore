// @ts-check
/*
 * The count on the Messaging > Inbox admin menu item: the unread conversations waiting on the user. The admin menu is on every page,
 * the workspace included, so the workspace and the notifications every other admin page carries both keep it current
 * through this one helper. The number comes from the server, which applies the inbox's own visibility rules, so the
 * page never has to work out what the user may see.
 *
 * An ES module: the scripts that use it import it, and the asset build bundles it into each of them; the unit tests
 * import it directly.
 */

// The pause before the count is read after a notification. The hub sends the event before the change behind it
// has committed, so reading at once could still see the old number; a burst of events also folds into one read.
var settleDelayMs = 1500;

// A safety net for a notification that never arrived (a dropped connection, a conversation read on another
// device), kept slow because every admin page runs it.
var periodicRefreshMs = 60000;

// Turns a menu group's icon red while any item under it shows a count, and back once none does, so a collapsed
// menu (icons only) still shows there is something waiting. Every count on the admin menu marks itself with
// data-admin-menu-attention, whichever module draws it, so this can run after any of them changes.
export function flagMenuGroups(root) {
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

// The address the count is read from. The server answers page loads and the periodic read from the count it kept for
// the user a few seconds ago; a read a notification asked for passes refresh, so what was announced shows at once.
export function attentionUrl(url, fresh) {
    if (!fresh) {
        return url;
    }

    return url + (url.indexOf('?') === -1 ? '?' : '&') + 'refresh=true';
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
//   refresh()  - reads the count now; refresh(true) skips the count the server kept.
//   schedule() - reads the fresh count a moment from now; what a notification asks for.
export function createAttentionBadge(url) {
    var timer = null;
    var inFlight = false;
    var queued = false;
    var queuedFresh = false;

    function refresh(fresh) {
        if (!url || !document.querySelector('[data-messaging-attention-badge]')) {
            return;
        }

        if (inFlight) {
            queued = true;
            queuedFresh = queuedFresh || !!fresh;

            return;
        }

        inFlight = true;

        fetch(attentionUrl(url, fresh), { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin', cache: 'no-store' })
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
                    var again = queuedFresh;

                    queued = false;
                    queuedFresh = false;
                    refresh(again);
                }
            });
    }

    function schedule() {
        if (timer !== null) {
            clearTimeout(timer);
        }

        timer = setTimeout(function () {
            timer = null;
            refresh(true);
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
