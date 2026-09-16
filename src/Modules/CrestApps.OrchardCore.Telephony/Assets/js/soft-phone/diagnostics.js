/*
 * Diagnostic text: turning whatever a provider SDK hands us into something a person can read in a log.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // How much of a serialized warning to keep. Long enough to carry a real payload, short enough that a
    // pathological object cannot flood the telemetry channel.
    var WARNING_TEXT_LIMIT = 400;

    // Describes a provider SDK warning.
    //
    // Reading a few hand-picked property names off the object and falling back to the literal string
    // "Provider warning" threw the payload away whenever the SDK used a different shape -- which Telnyx does.
    // Four warnings were logged during a call the caller could barely hear, and every one of them reached the
    // server with no code, no message, and no context: the one signal that would have named the problem
    // ("Low local microphone audio detected") arrived empty. A warning whose content is dropped is worse than
    // no warning at all, because it reads as one that was already looked at.
    //
    // So an unrecognized shape is serialized rather than discarded. It only has to be seen once to be handled
    // properly, and until then it is still readable.
    function describeProviderWarning(warning) {
        if (!warning) {
            return '';
        }

        if (typeof warning === 'string') {
            return warning;
        }

        var code = warning.code || warning.name || warning.type || '';
        var message = warning.message || warning.error || warning.detail || warning.reason || '';

        if (!message) {
            try {
                var serialized = JSON.stringify(warning);

                // "{}" means the properties are non-enumerable (an Error-like object), so name the keys the
                // object does expose rather than reporting an empty payload.
                message = serialized && serialized !== '{}'
                    ? serialized.slice(0, WARNING_TEXT_LIMIT)
                    : 'keys=[' + Object.keys(warning).join(',') + ']';
            } catch (error) {
                // Circular or host objects cannot be serialized; the type is still more than nothing.
                message = 'unserializable ' + (typeof warning);
            }
        }

        if (typeof message !== 'string') {
            message = String(message);
        }

        return (code ? ('[' + code + '] ') : '') + (message || 'no payload');
    }

    // Provider warnings that describe an ordinary pause rather than a fault. The SDK raises LOW_INBOUND_AUDIO
    // (31006) when the audio arriving AT this browser averages below 0.001 for three consecutive seconds,
    // and re-raises it every fifteen seconds while that holds. In a two-party call that is the other person not
    // talking for three seconds -- it fired steadily while an agent read a test passage aloud. It says nothing
    // about what the far end hears, and the soft phone's own inbound probe measures the same direction more
    // usefully. Surfacing it as a warning buried the signals that mattered under twenty identical lines a call.
    //
    // Kept visible as information, never dropped: the description is accurate, and on a call where the far end
    // really has gone silent it is still the first hint. Judged by code, not by text, so a reworded message in a
    // later SDK does not change the classification.
    var PAUSE_DRIVEN_WARNING_CODES = { 31006: true };

    // The diagnostic level a provider warning should be reported at.
    function classifyProviderWarning(warning) {
        var code = warning && typeof warning === 'object' ? (warning.code || (warning.warning && warning.warning.code)) : null;

        return code != null && PAUSE_DRIVEN_WARNING_CODES[code] ? 'info' : 'warning';
    }

    softPhone.PAUSE_DRIVEN_WARNING_CODES = PAUSE_DRIVEN_WARNING_CODES;
    softPhone.classifyProviderWarning = classifyProviderWarning;
    softPhone.describeProviderWarning = describeProviderWarning;
}(typeof globalThis !== 'undefined' ? globalThis : window));
