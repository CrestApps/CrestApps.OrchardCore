import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// Admin buttons that move money (a refund, a payout, a charge, a gift card or store credit balance) or lock work to
// payroll. A double click sent them twice, and most asked nothing first. Each form now carries no-multisubmit (the admin
// theme ignores a second submit) and asks before it goes: data-url-af="RemoveUrl" with data-title/data-message, or
// data-confirm (the crestapps-browser helpers) where the button has to keep the form's own validation.
const modulesDirectory = join('src', 'Modules');

const forms = [
    ['CrestApps.OrchardCore.Orders/Views/OrdersAdmin/_OrderActions.cshtml', 'Refund'],
    ['CrestApps.OrchardCore.Orders/Views/OrdersAdmin/_OrderActions.cshtml', 'Cancel'],
    ['CrestApps.OrchardCore.Transactions/Views/Admin/Detail.cshtml', 'RecordPayment'],
    ['CrestApps.OrchardCore.Transactions/Views/Admin/Detail.cshtml', 'Adjust'],
    ['CrestApps.OrchardCore.Transactions/Views/Admin/Detail.cshtml', 'MarkPaid'],
    ['CrestApps.OrchardCore.Marketplace/Views/VendorsAdmin/Detail.cshtml', 'PayOut'],
    ['CrestApps.OrchardCore.Marketplace/Views/VendorsAdmin/Detail.cshtml', 'Release'],
    ['CrestApps.OrchardCore.Bookings/Views/BookingsAdmin/Detail.cshtml', 'ChargeBalance'],
    ['CrestApps.OrchardCore.Orders/Views/GiftCardsAdmin/Create.cshtml', 'Create'],
    ['CrestApps.OrchardCore.Orders/Views/GiftCardsAdmin/Edit.cshtml', 'Adjust'],
    ['CrestApps.OrchardCore.Customers/Views/CustomerStoreCredit.DetailAdmin.cshtml', 'Change'],
    ['CrestApps.OrchardCore.Customers/Views/CustomerLoyalty.DetailAdmin.cshtml', 'Change'],
    ['CrestApps.OrchardCore.Transactions/Views/PaymentsAdmin/Refund.cshtml', 'Refund'],
    ['CrestApps.OrchardCore.Subscriptions/Views/InstallmentPlans/Detail.cshtml', 'Charge'],
    ['CrestApps.OrchardCore.Workforce/Views/Timesheets/Index.cshtml', 'Lock'],
];

// The form with this asp-action, from its opening tag to its end.
function formFor(text, action) {
    const start = text.search(new RegExp(`<form\\b[^>]*\\sasp-action="${action}"`));

    if (start < 0) {
        return null;
    }

    const end = text.indexOf('</form>', start);

    return { open: text.slice(start, text.indexOf('>', start) + 1), body: text.slice(start, end) };
}

describe.each(forms)('%s, the %s form', (view, action) => {
    const form = formFor(readFileSync(join(modulesDirectory, view), 'utf8'), action);

    it('is found', () => {
        expect(form).not.toBeNull();
    });

    it('is sent once however often its button is clicked', () => {
        expect(form.open).toMatch(/\sclass="[^"]*\bno-multisubmit\b/);
    });

    it('asks before it is sent', () => {
        const asks = /\sdata-url-af="[^"]*\bRemoveUrl\b[^>]*\sdata-message=|\sdata-message=[^>]*\sdata-url-af="[^"]*\bRemoveUrl\b|\sdata-confirm="/;

        expect(form.body).toMatch(asks);
    });
});
