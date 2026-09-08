import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

const softPhoneSource = readFileSync('src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone.js', 'utf8');
const reportSource = readFileSync('src/Abstractions/CrestApps.OrchardCore.Telephony.Abstractions/Models/CallQualityReport.cs', 'utf8');

/**
 * Reads the keys of the call-quality payload the soft phone sends to the hub.
 */
function readPayloadKeys() {
    const start = softPhoneSource.indexOf('var payload = {');

    if (start < 0) {
        throw new Error('The call-quality payload could not be located in soft-phone.js.');
    }

    const body = softPhoneSource.slice(start, softPhoneSource.indexOf('};', start));

    return [...body.matchAll(/^\s{16}([A-Za-z0-9_]+)\s*:/gm)].map(match => match[1]);
}

/**
 * Reads the public property names of the server-side report the payload binds to.
 */
function readReportProperties() {
    return [...reportSource.matchAll(/public\s+[A-Za-z<>?\[\]]+\s+([A-Za-z0-9_]+)\s*\{\s*get;/g)].map(match => match[1]);
}

// A quality sample is bound by name. A key the server has no property for does not fail, does not warn, and does
// not appear anywhere -- it silently arrives as the type's default. That is how every sample came in reporting a
// round-trip time of zero: the client sent "rttMs" and the contract calls it "RoundTripTimeMs". The MOS beside it
// had been computed from the real value, so calls were logged as Poor with 0ms round trip and no packet loss,
// which reads as a broken score rather than the half-second latency it actually was.
describe('the call-quality payload', () => {
    const payloadKeys = readPayloadKeys();
    const reportProperties = readReportProperties();

    it('sends something', () => {
        expect(payloadKeys.length).toBeGreaterThan(5);
        expect(reportProperties.length).toBeGreaterThan(5);
    });

    it.each(readPayloadKeys())('sends "%s", which the report can bind', key => {
        const match = reportProperties.some(property => property.toLowerCase() === key.toLowerCase());

        expect(match, `CallQualityReport has no property matching the payload key "${key}", so it binds to nothing and arrives as its default.`).toBe(true);
    });

    it('sends the round-trip time under the name the report binds', () => {
        // Named explicitly because this is the one that was wrong, and the one whose absence is least visible:
        // a missing round trip reads as a perfect connection rather than a missing measurement.
        expect(payloadKeys).toContain('roundTripTimeMs');
        expect(payloadKeys).not.toContain('rttMs');
    });
});
