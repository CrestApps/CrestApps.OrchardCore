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
