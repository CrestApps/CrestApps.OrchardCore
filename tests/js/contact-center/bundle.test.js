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
