import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/audio-devices.js';

const softPhone = globalThis.CrestAppsSoftPhone;

describe('isVirtualAudioDevice', () => {
    it('recognises the loopback and routing devices that break a call', () => {
        // Seen live as the microphone on a computer whose calls went silent in both directions.
        expect(softPhone.isVirtualAudioDevice('CABLE Output (VB-Audio Virtual Cable)')).toBe(true);
        expect(softPhone.isVirtualAudioDevice('CABLE Input (VB-Audio Virtual Cable)')).toBe(true);
        expect(softPhone.isVirtualAudioDevice('Default - CABLE Input (VB-Audio Virtual Cable)')).toBe(true);
        expect(softPhone.isVirtualAudioDevice('VoiceMeeter Output (VB-Audio VoiceMeeter VAIO)')).toBe(true);
        expect(softPhone.isVirtualAudioDevice('Stereo Mix (Realtek(R) Audio)')).toBe(true);
        expect(softPhone.isVirtualAudioDevice('BlackHole 2ch')).toBe(true);
        expect(softPhone.isVirtualAudioDevice('Soundflower (2ch)')).toBe(true);
        expect(softPhone.isVirtualAudioDevice('Line 1 (Virtual Audio Cable)')).toBe(true);
    });

    it('leaves real hardware alone', () => {
        expect(softPhone.isVirtualAudioDevice('Headset Microphone (Realtek(R) Audio)')).toBe(false);
        expect(softPhone.isVirtualAudioDevice('Microphone Array (Intel® Smart Sound Technology for Digital Microphones)')).toBe(false);
        expect(softPhone.isVirtualAudioDevice('Headset (WH-1000XM5) (Bluetooth)')).toBe(false);
        expect(softPhone.isVirtualAudioDevice('Speakers (Realtek(R) Audio)')).toBe(false);
    });

    it('leaves the noise-cancelling microphones agents choose on purpose alone', () => {
        expect(softPhone.isVirtualAudioDevice('Microphone (NVIDIA Broadcast)')).toBe(false);
        expect(softPhone.isVirtualAudioDevice('Krisp Microphone (Krisp Audio)')).toBe(false);
    });

    it('says nothing about a device with no label', () => {
        // Labels are empty until microphone permission is granted.
        expect(softPhone.isVirtualAudioDevice('')).toBe(false);
        expect(softPhone.isVirtualAudioDevice(undefined)).toBe(false);
        expect(softPhone.isVirtualAudioDevice(null)).toBe(false);
    });
});

describe('resolveDeviceLabel', () => {
    const devices = [
        { kind: 'audiooutput', deviceId: 'default', label: 'Default - CABLE Input (VB-Audio Virtual Cable)' },
        { kind: 'audiooutput', deviceId: 'speakers', label: 'Speakers (Realtek(R) Audio)' },
        { kind: 'audioinput', deviceId: 'default', label: 'Default - Headset Microphone (Realtek(R) Audio)' },
    ];

    it('names the device a selection points at', () => {
        expect(softPhone.resolveDeviceLabel(devices, 'audiooutput', 'speakers')).toBe('Speakers (Realtek(R) Audio)');
    });

    it('names what the system default really is when nothing was picked', () => {
        // A virtual cable is most often reached this way: set as the Windows default, never chosen in the phone.
        expect(softPhone.resolveDeviceLabel(devices, 'audiooutput', null)).toBe('Default - CABLE Input (VB-Audio Virtual Cable)');
        expect(softPhone.resolveDeviceLabel(devices, 'audioinput', '')).toBe('Default - Headset Microphone (Realtek(R) Audio)');
    });

    it('returns nothing for an unknown device or list', () => {
        expect(softPhone.resolveDeviceLabel(devices, 'audiooutput', 'gone')).toBe('');
        expect(softPhone.resolveDeviceLabel(undefined, 'audiooutput', 'speakers')).toBe('');
    });
});

// Live (2026-09-26), a supervisor's Bluetooth headset dropped: the phone could not capture it, gave up registering and
// waited for a Retry click while the computer's own microphone sat unused. Confirmed fixed live on 2026-09-27.
describe('shouldFallBackToDefaultMicrophone', () => {
    const fallsBack = softPhone.shouldFallBackToDefaultMicrophone;
    const failure = name => ({ name });

    it('falls back when the chosen microphone is not there any more', () => {
        expect(fallsBack(failure('NotFoundError'), 'headset-1')).toBe(true);
        expect(fallsBack(failure('OverconstrainedError'), 'headset-1')).toBe(true);
        expect(fallsBack(failure('DevicesNotFoundError'), 'headset-1')).toBe(true);
    });

    it('never works around a permission refusal', () => {
        expect(fallsBack(failure('NotAllowedError'), 'headset-1')).toBe(false);
        expect(fallsBack(failure('SecurityError'), 'headset-1')).toBe(false);
    });

    it('does nothing when the default microphone was already in use', () => {
        expect(fallsBack(failure('NotFoundError'), null)).toBe(false);
        expect(fallsBack(failure('NotFoundError'), '')).toBe(false);
    });

    it('does not fall back on a failure it cannot name', () => {
        expect(fallsBack(null, 'headset-1')).toBe(false);
        expect(fallsBack(new Error('boom'), 'headset-1')).toBe(false);
    });
});
