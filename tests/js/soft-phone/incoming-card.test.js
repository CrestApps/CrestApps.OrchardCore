import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/incoming-card.js';

const { incomingCardActionLabels } = globalThis.CrestAppsSoftPhone;

const strings = { answerAndOpen: 'Answer & open', open: 'Open' };

// Bug: answering a handed-off call with "Answer & open" landed the agent on the customer's edit screen, not on the
// notes and disposition for the call. The card for the offered activity now opens that activity, and its actions have
// to say so rather than the generic "open".
describe('incomingCardActionLabels', () => {
    it('uses the labels the card gives for what it opens', () => {
        expect(incomingCardActionLabels({ openText: 'Open activity', answerAndOpenText: 'Answer & open activity' }, strings))
            .toEqual({ answerAndOpen: 'Answer & open activity', open: 'Open activity' });
    });

    it('falls back to the phone generic labels', () => {
        expect(incomingCardActionLabels({ url: '/contents/1' }, strings)).toEqual({ answerAndOpen: 'Answer & open', open: 'Open' });
        expect(incomingCardActionLabels({ openText: '  ' }, {})).toEqual({ answerAndOpen: 'Answer & open', open: 'Open' });
        expect(incomingCardActionLabels(null, null)).toEqual({ answerAndOpen: 'Answer & open', open: 'Open' });
    });
});
