/*
 * Audio devices that are not a microphone or a speaker at all.
 *
 * A virtual audio cable, a mixer such as VoiceMeeter, or a "Stereo Mix" loopback shows up in the device lists like
 * any other microphone or speaker, and picking one breaks a call in a way that looks like the platform's fault.
 * As the microphone it captures the computer's own playback -- the far end's voice -- so the caller hears
 * themselves and nobody's voice is sent. As the speaker it plays the call into the cable, so the agent hears
 * nothing. Observed live: an extension call where each side heard silence, or itself, because one computer had
 * "CABLE Output (VB-Audio Virtual Cable)" selected as its microphone.
 *
 * Noise-cancelling microphones are virtual devices too (Krisp, NVIDIA Broadcast) and are what an agent wants, so
 * only the loopback and routing devices are named here.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // Matched against the device label, case-insensitively.
    var VIRTUAL_DEVICE_PATTERNS = [
        /vb-audio/,
        /\bvb-cable\b/,
        /\bcable (input|output)\b/,
        /virtual (audio )?cable/,
        /voicemeeter/,
        /stereo mix/,
        /what u hear/,
        /wave out mix/,
        /\bblackhole\b/,
        /soundflower/,
        /loopback audio/
    ];

    // Whether a device label names a virtual cable, mixer or loopback rather than real hardware.
    function isVirtualAudioDevice(label) {
        if (typeof label !== 'string' || !label.trim()) {
            return false;
        }

        var normalized = label.toLowerCase();

        for (var i = 0; i < VIRTUAL_DEVICE_PATTERNS.length; i++) {
            if (VIRTUAL_DEVICE_PATTERNS[i].test(normalized)) {
                return true;
            }
        }

        return false;
    }

    // The label of the device a selection resolves to. An empty selection is the system default, which the browser
    // lists as the "default" pseudo-device with the real device's name in its label ("Default - CABLE Input ...").
    function resolveDeviceLabel(devices, kind, deviceId) {
        if (!Array.isArray(devices)) {
            return '';
        }

        var wanted = deviceId || 'default';

        for (var i = 0; i < devices.length; i++) {
            var device = devices[i];

            if (device && device.kind === kind && device.deviceId === wanted) {
                return device.label || '';
            }
        }

        return '';
    }

    softPhone.isVirtualAudioDevice = isVirtualAudioDevice;
    softPhone.resolveDeviceLabel = resolveDeviceLabel;
})(typeof globalThis !== 'undefined' ? globalThis : window);
