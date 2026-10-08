import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/signaling-region.js';

const softPhone = globalThis.CrestAppsSoftPhone;

describe('clampSignalingRegion', () => {
    it('accepts the regions the SDK knows', () => {
        expect(softPhone.clampSignalingRegion('eu')).toBe('eu');
        expect(softPhone.clampSignalingRegion('us-west')).toBe('us-west');
        expect(softPhone.clampSignalingRegion('south-asia')).toBe('south-asia');
    });

    it('normalizes case and surrounding space, so a hand-typed setting still works', () => {
        expect(softPhone.clampSignalingRegion('  EU  ')).toBe('eu');
        expect(softPhone.clampSignalingRegion('US_WEST'.toLowerCase().replace('_', '-'))).toBe('us-west');
    });

    it('treats anything unknown as automatic, so a stale choice cannot point at a dead host', () => {
        expect(softPhone.clampSignalingRegion('middle-east')).toBe('');
        expect(softPhone.clampSignalingRegion('lv1-prod')).toBe('');
        expect(softPhone.clampSignalingRegion(undefined)).toBe('');
        expect(softPhone.clampSignalingRegion(null)).toBe('');
        expect(softPhone.clampSignalingRegion(7)).toBe('');
    });
});

describe('resolveSignalingRegion', () => {
    it('uses the tenant setting when the agent has not chosen', () => {
        expect(softPhone.resolveSignalingRegion('eu', '')).toBe('eu');
        expect(softPhone.resolveSignalingRegion('eu', undefined)).toBe('eu');
    });

    it('lets the agent override the tenant, because the right edge follows the person', () => {
        expect(softPhone.resolveSignalingRegion('us-east', 'eu')).toBe('eu');
    });

    it('falls through to the provider geo-routing when neither is set', () => {
        expect(softPhone.resolveSignalingRegion('', '')).toBe('');
    });

    it('ignores an unknown value on either side rather than passing it through', () => {
        expect(softPhone.resolveSignalingRegion('atlantis', 'eu')).toBe('eu');
        expect(softPhone.resolveSignalingRegion('eu', 'atlantis')).toBe('eu');
        expect(softPhone.resolveSignalingRegion('atlantis', 'middle-east')).toBe('');
    });
});

describe('withSignalingRegion', () => {
    it('adds the region when one is chosen', () => {
        expect(softPhone.withSignalingRegion({ login: 'a' }, 'eu')).toEqual({ login: 'a', region: 'eu' });
    });

    it('adds nothing on automatic, leaving the SDK default routing untouched', () => {
        const options = softPhone.withSignalingRegion({ login: 'a' }, '');

        expect(options).toEqual({ login: 'a' });
        expect('region' in options).toBe(false);
    });

    it('adds nothing for an unknown region rather than passing it through', () => {
        expect('region' in softPhone.withSignalingRegion({ login: 'a' }, 'atlantis')).toBe(false);
    });
});

describe('describeSignalingRegion', () => {
    it('gives the label for the readout', () => {
        expect(softPhone.describeSignalingRegion('eu')).toBe('Europe');
        expect(softPhone.describeSignalingRegion('ca-central')).toBe('Canada Central');
    });

    it('says nothing on automatic', () => {
        expect(softPhone.describeSignalingRegion('')).toBe('');
    });
});

// The offered list has to track the SDK actually vendored here: a value the SDK does not know would be rewritten
// into a hostname that does not resolve, and the agent would simply fail to register.
describe('the offered regions', () => {
    it('match the vendored SDK exactly', () => {
        const declaration = readFileSync('node_modules/@telnyx/webrtc/lib/src/Region.d.ts', 'utf8');
        const sdkValues = [...declaration.matchAll(/readonly [A-Z_]+:\s*"([a-z-]+)"/g)].map(m => m[1]).sort();
        const offered = softPhone.SIGNALING_REGIONS.map(r => r.value).filter(Boolean).sort();

        expect(offered).toEqual(sdkValues);
    });

    it('offers automatic first, because that is the default behaviour', () => {
        expect(softPhone.SIGNALING_REGIONS[0].value).toBe('');
    });

    it('has no Middle East region -- an agent there must choose Europe, and the list should not imply otherwise', () => {
        const values = softPhone.SIGNALING_REGIONS.map(r => r.value);

        expect(values.some(v => /middle|me\b|dubai/i.test(v))).toBe(false);
    });
});
