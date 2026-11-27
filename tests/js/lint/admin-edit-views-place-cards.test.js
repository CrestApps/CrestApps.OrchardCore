import { readdirSync, readFileSync } from 'node:fs';
import { basename, join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// Orchard Core (PR #20256) groups the fields of every admin form in logical cards, and every card comes from
// placement: `.Location(l => l.Zone("Content", "1").Card(S["Server"], "10"))`. A view holds only its rows, so it never
// writes `card`, `card-header` or `<fieldset>`/`<legend>` markup for a group of fields; the admin theme renders the
// card (a fieldset titled by its legend) itself. A view that does shows a second frame inside the placed card.
const modulesDirectory = join('src', 'Modules');

// Editors this rule is not about: content item editors (parts and fields are not grouped in cards), workflow activity,
// deployment and pipeline step fields (*.Fields.Edit, Items/), list filters and the partials shared by several screens.
function isAdminEditView(path) {
    const parts = path.split(sep);
    const name = basename(path);

    if (parts.includes('Shared') || parts.includes('Items') || name.startsWith('_')) {
        return false;
    }

    if (/Part[.-]|Part\.Edit|Field[.-]|Field\.Edit|Filters?\.Edit|\.Fields\.Edit/.test(name)) {
        return false;
    }

    return name.endsWith('.Edit.cshtml');
}

// Views reviewed one by one: what they draw is not a group of fields of the form but a repeated item (a row with its
// own header that scripts add and remove), a selectable card, or one record inside a list of records.
const reviewedExceptions = new Map([
    ['CrestApps.OrchardCore.AgenticCommerce/Views/AgentFeedSettingsApis.Edit.cshtml', 'one fieldset per repeated feed target'],
    ['CrestApps.OrchardCore.AI/Views/AIToolInstanceParameters.Edit.cshtml', 'one card per repeated parameter row'],
    ['CrestApps.OrchardCore.AI.Chat/Views/AIProfileAnalytics.Edit.cshtml', 'one card per repeated conversion goal'],
    ['CrestApps.OrchardCore.AI.Chat/Views/AIProfileDataExtraction.Edit.cshtml', 'one card per repeated extraction entry'],
    ['CrestApps.OrchardCore.AI.Chat/Views/AIProfilePostSession.Edit.cshtml', 'one card per repeated post-session task'],
    ['CrestApps.OrchardCore.AI.Chat.Copilot/Views/ChatInteractionCopilotConfig.Edit.cshtml', 'call-to-action box, not a field group'],
    ['CrestApps.OrchardCore.AI.Mcp/Views/McpPromptArguments.Edit.cshtml', 'one card per repeated argument row'],
    ['CrestApps.OrchardCore.AI.Mcp/Views/McpPromptMessages.Edit.cshtml', 'one card per repeated message row'],
    ['CrestApps.OrchardCore.AI.Prompting/Views/PromptTemplateChatInteractionSelection.Edit.cshtml', 'one card per selected prompt template'],
    ['CrestApps.OrchardCore.AI.Prompting/Views/PromptTemplateSelection.Edit.cshtml', 'one card per selected prompt template'],
    ['CrestApps.OrchardCore.Checkout/Views/CheckoutFlow.Edit.cshtml', 'one card per repeated checkout step'],
    ['CrestApps.OrchardCore.ContactCenter/Views/ActivityQueueHoursAndOverflow.Edit.cshtml', 'one card per repeated overflow rule'],
    ['CrestApps.OrchardCore.ContactCenter/Views/ActivityQueueSkills.Edit.cshtml', 'one card per repeated skill row'],
    ['CrestApps.OrchardCore.ContactCenter/Views/AgentEntitlementSkills.Edit.cshtml', 'one card per repeated skill row'],
    ['CrestApps.OrchardCore.Customers/Views/CustomerContactStep.Edit.cshtml', 'storefront checkout step; disabled-state fieldset'],
    ['CrestApps.OrchardCore.Inventory/Views/InventorySettingsLocations.Edit.cshtml', 'one fieldset per repeated stock location'],
    ['CrestApps.OrchardCore.Learning.Coursework/Views/SubmissionGrade.Edit.cshtml', 'radio group fieldset per rubric criterion'],
    ['CrestApps.OrchardCore.Omnichannel.Managements/Views/BulkManageActivityFilterFields.Edit.cshtml', 'filter form'],
    ['CrestApps.OrchardCore.Omnichannel.Managements/Views/CadenceSteps.Edit.cshtml', 'one card per repeated cadence step'],
    ['CrestApps.OrchardCore.Shipping/Views/ShippingCheckoutStep.Edit.cshtml', 'storefront checkout step'],
    ['CrestApps.OrchardCore.Shipping/Views/ShippingPickupSettings.Edit.cshtml', 'one fieldset per repeated pickup location'],
    ['CrestApps.OrchardCore.TenantHierarchy/Views/ChildTenantRecipe.Edit.cshtml', 'selectable recipe cards'],
    ['CrestApps.OrchardCore.Tickets/Views/TicketBoardColumns.Edit.cshtml', 'one card per repeated board column'],
    ['CrestApps.OrchardCore.Wizard/Views/WizardFlow.Edit.cshtml', 'content flow editor'],
]);

const groupingMarkup = /<fieldset\b|class="[^"]*\bcard\b(?![-\w])[^"]*"|\bcard-header\b/;

function* views(directory) {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
        const path = join(directory, entry.name);

        if (entry.isDirectory()) {
            if (!['bin', 'obj', 'node_modules'].includes(entry.name)) {
                yield* views(path);
            }
        } else if (entry.name.endsWith('.cshtml')) {
            yield path;
        }
    }
}

describe('admin edit views place their cards', () => {
    const offenders = [];

    for (const path of views(modulesDirectory)) {
        if (!isAdminEditView(path)) {
            continue;
        }

        const key = path.split(sep).slice(2).join('/');

        if (reviewedExceptions.has(key)) {
            continue;
        }

        if (groupingMarkup.test(readFileSync(path, 'utf8'))) {
            offenders.push(key);
        }
    }

    it('does not write card or fieldset markup in an admin edit view', () => {
        // Declare the card in the placement of the shape instead: .Location(l => l.Zone("Content", "1").Card(S["Name"], "10")).
        expect(offenders).toEqual([]);
    });

    it('lists only reviewed exceptions that still exist and still need one', () => {
        const stale = [];

        for (const key of reviewedExceptions.keys()) {
            const path = join(modulesDirectory, ...key.split('/'));
            let text = null;

            try {
                text = readFileSync(path, 'utf8');
            } catch {
                // The view was removed.
            }

            if (text === null || !groupingMarkup.test(text)) {
                stale.push(key);
            }
        }

        expect(stale).toEqual([]);
    });
});
