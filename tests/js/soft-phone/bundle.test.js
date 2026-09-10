import { readdirSync, readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

const moduleDirectory = 'src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone';
const assets = JSON.parse(readFileSync('src/Modules/CrestApps.OrchardCore.Telephony/Assets.json', 'utf8'));
const softPhoneGroup = assets.find(group => group.output === 'wwwroot/scripts/soft-phone.js');

// The bundle is a concatenation, not a module graph: the helper files have to be listed, and listed first, or
// they are simply not in the browser and every call through them is `undefined` — a soft phone that loads,
// renders, and then fails on the first number it formats. Nothing else would catch that until an agent did.
describe('the soft phone asset group', () => {
    it('is still declared', () => {
        expect(softPhoneGroup).toBeDefined();
    });

    it('includes every helper module', () => {
        const listed = softPhoneGroup.inputs;

        readdirSync(moduleDirectory)
            .filter(file => file.endsWith('.js'))
            .forEach(file => expect(listed).toContain(`${moduleDirectory.replace('src/Modules/CrestApps.OrchardCore.Telephony/', '')}/${file}`));
    });

    it('concatenates the helpers ahead of the script that uses them', () => {
        const inputs = softPhoneGroup.inputs;
        const mainIndex = inputs.indexOf('Assets/js/soft-phone.js');

        expect(mainIndex).toBe(inputs.length - 1);
    });
});

describe('the telephony client asset group', () => {
    const clientGroup = assets.find(group => group.output === 'wwwroot/scripts/telephony-client.js');

    it('concatenates the shared call timer ahead of the client that re-exports it', () => {
        // Every surface that shows a call duration reads it off this client. If the timer is not in the bundle
        // ahead of it, the export is undefined and every call on screen shows a blank timer.
        const inputs = clientGroup.inputs;

        expect(inputs.indexOf('Assets/js/shared/call-timer.js')).toBeLessThan(inputs.indexOf('Assets/js/telephony-client.js'));
    });
});
