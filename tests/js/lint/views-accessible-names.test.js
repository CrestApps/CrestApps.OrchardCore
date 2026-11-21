import { readdirSync, readFileSync } from 'node:fs';
import { join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// A screen reader names a control from its label or aria-label, never from its placeholder, reads a table cell with its
// header only when the header says which way it applies, and announces a dialog by the title it points at.
const modulesDirectory = join('src', 'Modules');

function readViews() {
    return readdirSync(modulesDirectory, { recursive: true })
        .map(file => String(file))
        .filter(file => file.endsWith('.cshtml') && !file.split(sep).some(part => part === 'bin' || part === 'obj'))
        .map(file => ({ name: file.split(sep).join('/'), text: readFileSync(join(modulesDirectory, file), 'utf8') }));
}

// The opening tags of a view. A '>' inside a quoted attribute value (a lambda in a Razor expression) does not end one.
function* tags(text, name) {
    for (const start of text.matchAll(new RegExp(`<${name}\\b`, 'g'))) {
        let quoted = false;

        for (let index = start.index + start[0].length; index < text.length; index++) {
            const character = text[index];

            if (character === '"') {
                quoted = !quoted;
            } else if (character === '>' && !quoted && text[index - 1] !== '=') {
                yield { tag: text.slice(start.index, index + 1), line: text.slice(0, start.index).split('\n').length };

                break;
            }
        }
    }
}

// Read once while the file is collected: reading every view takes a few seconds, more while a build runs beside it.
const all = readViews();

function offending(name, isBad) {
    return all.flatMap(view => [...tags(view.text, name)]
        .filter(({ tag }) => isBad(tag))
        .map(({ tag, line }) => `${view.name}:${line}: ${tag.replace(/\s+/g, ' ').slice(0, 100)}`));
}

// The tables the November 2026 review listed (UX-5). Every header cell in them says whether it heads a column or a row.
const tablesWithScopedHeaders = [
    'CrestApps.OrchardCore.Orders/Views/OrderInvoice.cshtml',
    'CrestApps.OrchardCore.Marketplace/Views/VendorDashboard.cshtml',
    'CrestApps.OrchardCore.Bookings/Views/BookingsAdmin/Dates.cshtml',
    'CrestApps.OrchardCore.ContactCenter/Views/ContactCenterExternalTransferSettings.Edit.cshtml',
    'CrestApps.OrchardCore.ContentTransfer/Views/Admin/Import.cshtml',
    'CrestApps.OrchardCore.Workforce/Views/TimeOff/Worker.cshtml',
    'CrestApps.OrchardCore.Workforce/Views/TimeOff/Create.cshtml',
    'CrestApps.OrchardCore.Fundraising/Views/AccountDonations/Index.cshtml',
    'CrestApps.OrchardCore.ContactCenter/Views/AgentEntitlements/_EntitlementFields.cshtml',
    'CrestApps.OrchardCore.Receipts/Views/ReceiptDocument.cshtml',
    'CrestApps.OrchardCore.Reports/Views/Shared/_ReportSection.cshtml',
    'CrestApps.OrchardCore.Reports/Views/Shared/_ReportWidgetSections.cshtml',
    'CrestApps.OrchardCore.Bookings/Views/BookingsAdmin/Calendar.cshtml',
    'CrestApps.OrchardCore.Inventory/Views/PurchaseOrdersAdmin/Edit.cshtml',
    'CrestApps.OrchardCore.Orders/Views/DraftOrdersAdmin/Edit.cshtml',
    'CrestApps.OrchardCore.Subscriptions/Views/InstallmentPlans/Create.cshtml',
];

describe('views', () => {
    it('name every search box for screen readers, not only with a placeholder', () => {
        expect(offending('input', tag => /\sid="search-box"/.test(tag) && !/\saria-label=/.test(tag))).toEqual([]);
    });

    it('point every modal at its title', () => {
        expect(offending('div', tag => /\sclass="(?:[^"]*\s)?modal(?:\s[^"]*)?"/.test(tag) && !/\saria-labelledby=/.test(tag))).toEqual([]);
    });

    // The cart, the soft phone, the agent bar and checkout errors announce themselves; a message arriving in the
    // messaging workspace did not.
    it('announce a new message in the messaging thread', () => {
        const thread = readFileSync(join(modulesDirectory, 'CrestApps.OrchardCore.Omnichannel.Messaging/Views/Admin/_Thread.cshtml'), 'utf8');
        const [container] = [...tags(thread, 'div')].filter(({ tag }) => /\sdata-thread\b/.test(tag));

        expect(container.tag).toMatch(/\saria-live="polite"/);
    });

    it.each(tablesWithScopedHeaders)('give each header cell in %s a scope', view => {
        const text = readFileSync(join(modulesDirectory, view), 'utf8');

        expect([...tags(text, 'th')].filter(({ tag }) => !/\sscope=/.test(tag)).map(({ tag, line }) => `${line}: ${tag}`)).toEqual([]);
    });
});
