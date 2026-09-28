import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/dial-target.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/in-call-controls.js';

const { planInCallControls } = globalThis.CrestAppsSoftPhone;

const capable = {
    canHold: true,
    canResume: true,
    canMute: true,
    canHangup: true,
    canTransfer: true,
    canAddCall: true,
    canDial: true
};

function plan(overrides) {
    return planInCallControls(Object.assign({ active: true, stateName: 'Connected', lineCount: 1 }, capable, overrides));
}

// The agent's own call controls sit in one row -- Mute, Hold, Keypad and Hang up -- and the actions that bring in or
// hand over another party in a second, labelled row: Transfer, Add call, and ending every call.
describe('planInCallControls', () => {
    it('offers only the dial button with no call', () => {
        const controls = planInCallControls(Object.assign({ active: false, stateName: 'Idle', lineCount: 0 }, capable));

        expect(controls.dial).toBe(true);
        expect(controls.callControls).toBe(false);
        expect(controls.options).toBe(false);
        expect(controls.showKeypad).toBe(true);
    });

    it('puts Mute, Hold, Keypad and Hang up in the primary row on a connected call, with the keypad closed', () => {
        const controls = plan({});

        expect(controls.callControls).toBe(true);
        expect(controls.mute).toEqual({ visible: true, pressed: false });
        expect(controls.hold).toEqual({ visible: true, pressed: false });
        expect(controls.keypad).toEqual({ visible: true, pressed: false });
        expect(controls.hangup).toEqual({ visible: true, kind: 'hangup' });
        expect(controls.showKeypad).toBe(false);
        expect(controls.dial).toBe(false);
    });

    it('shows mute and hold pressed while they are on', () => {
        expect(plan({ muted: true }).mute).toEqual({ visible: true, pressed: true });
        expect(plan({ stateName: 'OnHold' }).hold).toEqual({ visible: true, pressed: true });
    });

    it('opens the keypad for digits when the agent asks for it', () => {
        const controls = plan({ keypadOpen: true });

        expect(controls.keypad.pressed).toBe(true);
        expect(controls.showKeypad).toBe(true);
    });

    it('keeps the keypad open on a held call, where it enters a number to add, and offers dial once one is entered', () => {
        expect(plan({ stateName: 'OnHold', numberIsCallDisplay: true })).toMatchObject({ showKeypad: true, dial: false });
        expect(plan({ stateName: 'OnHold', numberIsCallDisplay: false })).toMatchObject({ showKeypad: true, dial: true });
        expect(plan({ stateName: 'OnHold' }).keypad.visible).toBe(false);
    });

    it('puts Transfer and Add call in the secondary row', () => {
        const controls = plan({});

        expect(controls.options).toBe(true);
        expect(controls.transfer).toBe(true);
        expect(controls.addCall).toEqual({ visible: true, disabledReason: '' });
        expect(controls.endAll).toEqual({ visible: false, kind: 'calls' });
    });

    it('explains an Add call that cannot hold the current call', () => {
        expect(plan({ canHold: false }).addCall).toEqual({ visible: true, disabledReason: 'hold-unavailable' });
        expect(plan({ canDial: false }).addCall.visible).toBe(false);
    });

    it('offers ending every call once two lines are up', () => {
        expect(plan({ lineCount: 2 }).endAll).toEqual({ visible: true, kind: 'calls' });
    });

    it('turns Hang up into Leave in a conference the others stay in, and ends it for everyone separately', () => {
        const controls = plan({ lineCount: 2, isConference: true, conferenceParties: 2 });

        expect(controls.hangup).toEqual({ visible: true, kind: 'leave' });
        expect(controls.endAll).toEqual({ visible: true, kind: 'conference' });
    });

    it('keeps Hang up in a conference with only one other party left, whom leaving would strand', () => {
        expect(plan({ lineCount: 1, isConference: true, conferenceParties: 1 }).hangup.kind).toBe('hangup');
    });

    it('replaces the call controls with Call and Back to call while a call is being added', () => {
        const controls = plan({ stateName: 'OnHold', addingCall: true, numberIsCallDisplay: false });

        expect(controls.callControls).toBe(false);
        expect(controls.options).toBe(false);
        expect(controls.dial).toBe(true);
        expect(controls.cancelAdd).toBe(true);
        expect(controls.showKeypad).toBe(true);
    });

    it('hides the keypad while the transfer panel is open', () => {
        expect(plan({ transferOpen: true, keypadOpen: true }).showKeypad).toBe(false);
        expect(plan({ transferOpen: true }).keypad.visible).toBe(false);
    });

    it('hides the call controls while an offer rings, which is answered or declined on its own card', () => {
        const controls = plan({ stateName: 'Ringing', ringingOffer: true });

        expect(controls.hangup.visible).toBe(false);
        expect(controls.mute.visible).toBe(false);
        expect(controls.options).toBe(false);
    });

    it('lets a call still connecting be cancelled, and nothing else', () => {
        const controls = plan({ stateName: 'Connecting' });

        expect(controls.hangup.visible).toBe(true);
        expect(controls.mute.visible).toBe(false);
        expect(controls.hold.visible).toBe(false);
        expect(controls.keypad.visible).toBe(false);
        expect(controls.transfer).toBe(false);
        expect(controls.addCall.visible).toBe(false);
    });

    it('offers nothing a provider cannot do', () => {
        const controls = planInCallControls({ active: true, stateName: 'Connected', lineCount: 2 });

        expect(controls.mute.visible).toBe(false);
        expect(controls.hold.visible).toBe(false);
        expect(controls.hangup.visible).toBe(false);
        expect(controls.transfer).toBe(false);
        expect(controls.addCall.visible).toBe(false);
        expect(controls.endAll.visible).toBe(false);
    });
});
