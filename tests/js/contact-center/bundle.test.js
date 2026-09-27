import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

const assets = JSON.parse(readFileSync('src/Modules/CrestApps.OrchardCore.ContactCenter/Assets.json', 'utf8'));
const helper = 'Assets/js/shared/agent-presence.js';

// The bundles are concatenations, not a module graph: a helper that is not listed ahead of the script that calls it
// is simply not in the browser, and the agent's presence label goes blank on the first change.
describe.each([
    ['wwwroot/scripts/contact-center-soft-phone.js', 'Assets/js/contact-center-soft-phone.js'],
    ['wwwroot/scripts/agent-workspace.js', 'Assets/js/agent-workspace.js'],
    ['wwwroot/scripts/contact-center-agent-bar.js', 'Assets/js/contact-center-agent-bar.js'],
])('the %s bundle', (output, script) => {
    const group = assets.find(candidate => candidate.output === output);

    it('carries the presence helper ahead of the script that uses it', () => {
        expect(group).toBeDefined();
        expect(group.inputs).toContain(helper);
        expect(group.inputs.indexOf(helper)).toBeLessThan(group.inputs.indexOf(script));
    });
});

// The live dashboard filters its agent board through the shared filter helper, and quietly shows every agent when the
// helper is missing. Listed after the dashboard, the helper may not be there yet when the dashboard starts (it starts at
// once on a page that has already loaded); left out, it never is. Either way the queue, campaign and status filters would
// do nothing, and no error would say why.
describe('the live dashboard bundle', () => {
    const group = assets.find(candidate => candidate.output === 'wwwroot/scripts/supervisor-dashboard.js');
    const filters = 'Assets/js/shared/agent-board-filters.js';
    const dashboard = 'Assets/js/supervisor-dashboard.js';

    it('carries the agent board filters ahead of the dashboard', () => {
        expect(group).toBeDefined();
        expect(group.inputs).toContain(filters);
        expect(group.inputs).toContain(dashboard);
        expect(group.inputs.indexOf(filters)).toBeLessThan(group.inputs.indexOf(dashboard));
    });
});
