import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/shared/messaging-state.js';
import '../../../src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/shared/messaging-attention-badge.js';

const messaging = globalThis.CrestAppsMessaging;

// Live, an open workspace in a background tab read every new message before anyone saw it, so the menu count stayed
// at zero. The poll now reads the thread only when it is in front of the agent.
describe('isThreadInView', () => {
    it('is in view when the page shows and the agent is at the bottom of the thread', () => {
        expect(messaging.isThreadInView(false, true)).toBe(true);
    });

    it('is not in view from a background tab', () => {
        expect(messaging.isThreadInView(true, true)).toBe(false);
    });

    it('is not in view while the agent is scrolled up reading history', () => {
        expect(messaging.isThreadInView(false, false)).toBe(false);
    });
});

// Just enough of the DOM for the admin menu: the element tree, closest(), classList and the selectors the menu
// scripts use.
class FakeElement {
    constructor(tag, classes = [], attributes = []) {
        this.tagName = tag.toUpperCase();
        this.classes = new Set(classes);
        this.attributes = new Set(attributes);
        this.children = [];
        this.parentElement = null;

        const classes$ = this.classes;
        this.classList = {
            contains: name => classes$.has(name),
            add: name => classes$.add(name),
            remove: name => classes$.delete(name),
            toggle: (name, force) => {
                const on = force === undefined ? !classes$.has(name) : !!force;

                if (on) {
                    classes$.add(name);
                } else {
                    classes$.delete(name);
                }

                return on;
            },
        };
    }

    append(...children) {
        children.forEach(child => {
            child.parentElement = this;
            this.children.push(child);
        });

        return this;
    }

    descendants() {
        return this.children.flatMap(child => [child, ...child.descendants()]);
    }

    matches(selector) {
        switch (selector) {
            case 'li': return this.tagName === 'LI';
            case '.item-label': return this.classes.has('item-label');
            case '.icon': return this.classes.has('icon');
            case '[data-admin-menu-attention]': return this.attributes.has('data-admin-menu-attention');
            case '[data-admin-menu-attention]:not(.d-none)': return this.attributes.has('data-admin-menu-attention') && !this.classes.has('d-none');
            case '[data-messaging-attention-badge]': return this.attributes.has('data-messaging-attention-badge');
            default: throw new Error(`The fake DOM does not know the selector ${selector}`);
        }
    }

    closest(selector) {
        for (let node = this; node; node = node.parentElement) {
            if (node.matches(selector)) {
                return node;
            }
        }

        return null;
    }

    querySelectorAll(selector) {
        if (selector.startsWith(':scope > ')) {
            const rest = selector.slice(':scope > '.length);

            return this.children.filter(child => child.matches(rest));
        }

        return this.descendants().filter(node => node.matches(selector));
    }

    querySelector(selector) {
        return this.querySelectorAll(selector)[0] ?? null;
    }
}

const el = (tag, classes, attributes) => new FakeElement(tag, classes, attributes);

// The admin theme's markup: each item is li > figure > figcaption > .item-label, and a group's items sit in a list
// inside its figure, not directly under its li.
function adminMenu() {
    const groupIcon = el('span', ['icon']).append(el('i'));
    const badge = el('span', ['badge', 'd-none'], ['data-admin-menu-attention']);
    const otherBadge = el('span', ['badge', 'd-none'], ['data-admin-menu-attention']);
    const otherGroupIcon = el('span', ['icon']).append(el('i'));

    const item = (label, count) => el('li').append(
        el('figure').append(el('figcaption').append(el('a', ['item-label']).append(el('span', ['icon', 'icon-none']), el('span', ['title']), ...(count ? [count] : [])))));

    const group = (icon, ...items) => el('li', ['has-items']).append(
        el('figure').append(
            el('figcaption').append(el('button', ['item-label']).append(icon, el('span', ['title']))),
            el('ul').append(...items)));

    const root = el('ul', ['menu-admin']).append(
        group(groupIcon, item('Live dashboard'), item('Shared voicemail', badge)),
        group(otherGroupIcon, item('Inbox', otherBadge), item('Broadcasts')));

    return { root, groupIcon, badge, otherGroupIcon, otherBadge };
}

// The shared voicemail template carries its own copy, inline in Razor; it is run here against the same menu.
function inlineVoicemailFlagger() {
    const view = readFileSync('src/Modules/CrestApps.OrchardCore.ContactCenter/Views/NavigationItemText-contactCenterSharedVoicemail.Id.cshtml', 'utf8');
    const start = view.indexOf('function flagMenuGroups()');

    expect(start).toBeGreaterThan(-1);

    // The function ends at the first line that closes it at its own indentation.
    const indent = view.slice(view.lastIndexOf('\n', start) + 1, start);
    const end = view.indexOf('\n' + indent + '}', start);
    const source = view.slice(start, end + indent.length + 2);

    return root => new Function('document', source + '\nflagMenuGroups();')(root);
}

const flaggers = {
    'the messaging script': root => messaging.flagMenuGroups(root),
    'the shared voicemail template': inlineVoicemailFlagger(),
};

describe.each(Object.entries(flaggers))('the menu group icon, as %s sets it', (_, flag) => {
    it('turns red while an item under the group shows a count', () => {
        const menu = adminMenu();
        menu.badge.classList.remove('d-none');

        flag(menu.root);

        expect(menu.groupIcon.classList.contains('text-danger')).toBe(true);
    });

    it('turns back once no item under it shows a count', () => {
        const menu = adminMenu();
        menu.groupIcon.classList.add('text-danger');

        flag(menu.root);

        expect(menu.groupIcon.classList.contains('text-danger')).toBe(false);
    });

    it('leaves another group alone', () => {
        const menu = adminMenu();
        menu.badge.classList.remove('d-none');

        flag(menu.root);

        expect(menu.otherGroupIcon.classList.contains('text-danger')).toBe(false);
    });

    // Each module's count re-runs the rule for every group, so one clearing never turns off another's red.
    it('keeps a group red for another module\'s count', () => {
        const menu = adminMenu();
        menu.otherBadge.classList.remove('d-none');
        menu.otherGroupIcon.classList.add('text-danger');

        flag(menu.root);

        expect(menu.otherGroupIcon.classList.contains('text-danger')).toBe(true);
    });

    it('never colours the item\'s own spacer icon', () => {
        const menu = adminMenu();
        menu.badge.classList.remove('d-none');

        flag(menu.root);

        const spacer = menu.badge.parentElement.querySelector(':scope > .icon');
        expect(spacer.classList.contains('text-danger')).toBe(false);
    });
});

// Live, the Shared voicemail item lost its indent (its template drew no icon spacer) and its badge ran off the edge.
describe('the admin menu items that show a count', () => {
    const templates = {
        'Shared voicemail': 'src/Modules/CrestApps.OrchardCore.ContactCenter/Views/NavigationItemText-contactCenterSharedVoicemail.Id.cshtml',
        'Messaging > Inbox': 'src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Views/NavigationItemText-messagingInbox.Id.cshtml',
    };

    it.each(Object.entries(templates))('%s starts with the icon spacer every item has', (_, path) => {
        expect(readFileSync(path, 'utf8').trimStart().startsWith('<span class="icon icon-none"')).toBe(true);
    });

    it.each(Object.entries(templates))('%s keeps its badge inside the row and marks it for the group icon', (_, path) => {
        const badge = readFileSync(path, 'utf8').match(/<span class="badge[^"]*"[^>]*>/);

        expect(badge).not.toBeNull();
        expect(badge[0]).toContain('ms-auto');
        expect(badge[0]).toContain('me-3');
        expect(badge[0]).toContain('flex-shrink-0');
        expect(badge[0]).toContain('data-admin-menu-attention');
    });

    it('shows the unread count on Inbox, not on the Messaging group', () => {
        const group = readFileSync('src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Views/NavigationItemText-messaging.Id.cshtml', 'utf8');
        const inbox = readFileSync(templates['Messaging > Inbox'], 'utf8');

        expect(group).not.toContain('data-messaging-attention-badge');
        expect(inbox).toContain('data-messaging-attention-badge');
    });
});
