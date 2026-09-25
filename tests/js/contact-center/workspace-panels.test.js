import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/shared/workspace-panels.js';

const { activeInteractionSignature, createChangeGate, emptyStateHtml, NO_ACTIVE_INTERACTION } = globalThis.CrestAppsContactCenter;

const assets = JSON.parse(readFileSync('src/Modules/CrestApps.OrchardCore.ContactCenter/Assets.json', 'utf8'));
const workspaceSource = readFileSync('src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/agent-workspace.js', 'utf8');

// Bug: the workspace's "Active interaction" card opened blank. It redrew only when the interaction's signature
// changed, and "no interaction" (null) was also the "never drawn" value, so the first state was skipped and the
// empty state only appeared after a call had come and gone.
describe('createChangeGate', () => {
    it('draws the first value even when it is the empty one', () => {
        const changed = createChangeGate();

        expect(changed(activeInteractionSignature(null))).toBe(true);
    });

    it('draws the first value even when it is null or undefined', () => {
        expect(createChangeGate()(null)).toBe(true);
        expect(createChangeGate()(undefined)).toBe(true);
    });

    it('skips a repeat of the value already drawn', () => {
        const changed = createChangeGate();

        changed('a');

        expect(changed('a')).toBe(false);
    });

    it('draws again when the interaction starts and when it ends', () => {
        const changed = createChangeGate();
        const call = { interactionId: 'i-1', status: 'Connected' };

        expect(changed(activeInteractionSignature(null))).toBe(true);
        expect(changed(activeInteractionSignature(call))).toBe(true);
        expect(changed(activeInteractionSignature(call))).toBe(false);
        expect(changed(activeInteractionSignature(null))).toBe(true);
    });
});

describe('activeInteractionSignature', () => {
    it('names the empty card', () => {
        expect(activeInteractionSignature(null)).toBe(NO_ACTIVE_INTERACTION);
        expect(activeInteractionSignature(undefined)).toBe(NO_ACTIVE_INTERACTION);
    });

    it('changes with the status and the recording state', () => {
        const base = { interactionId: 'i-1', status: 'Connected', recordingState: 'Active' };

        expect(activeInteractionSignature(base)).not.toBe(activeInteractionSignature({ ...base, status: 'OnHold' }));
        expect(activeInteractionSignature(base)).not.toBe(activeInteractionSignature({ ...base, recordingState: 'Paused', isRecordingPaused: true }));
        expect(activeInteractionSignature(base)).not.toBe(NO_ACTIVE_INTERACTION);
    });
});

describe('emptyStateHtml', () => {
    it('shows an icon, a headline and a hint', () => {
        const html = emptyStateHtml({ icon: 'fa-solid fa-headset', title: 'No active interactions right now', hint: 'Incoming calls appear here.' });

        expect(html).toContain('class="cc-empty"');
        expect(html).toContain('<i class="fa-solid fa-headset"></i>');
        expect(html).toContain('aria-hidden="true"');
        expect(html).toContain('<div class="cc-empty__title">No active interactions right now</div>');
        expect(html).toContain('<div class="cc-empty__hint">Incoming calls appear here.</div>');
    });

    it('leaves the hint out when there is none', () => {
        expect(emptyStateHtml({ title: 'Nothing' })).not.toContain('cc-empty__hint');
    });

    it('escapes the text it is given', () => {
        const html = emptyStateHtml({ title: '<b>x</b>', hint: '"y" & z' });

        expect(html).toContain('&lt;b&gt;x&lt;/b&gt;');
        expect(html).toContain('&quot;y&quot; &amp; z');
    });

    it('can render as a list item for a list card', () => {
        expect(emptyStateHtml({ title: 'No recent interactions.' }, 'li')).toMatch(/^<li class="cc-empty"[\s\S]*<\/li>$/);
    });
});

describe('the agent workspace', () => {
    const group = assets.find(candidate => candidate.output === 'wwwroot/scripts/agent-workspace.js');

    it('bundles the panel helper ahead of the workspace script', () => {
        expect(group.inputs).toContain('Assets/js/shared/workspace-panels.js');
        expect(group.inputs.indexOf('Assets/js/shared/workspace-panels.js')).toBeLessThan(group.inputs.indexOf('Assets/js/agent-workspace.js'));
    });

    it('gates the active card with a change gate rather than a null-seeded signature', () => {
        expect(workspaceSource).toContain('createChangeGate()');
        expect(workspaceSource).not.toMatch(/var activeSignature = null/);
    });
});
