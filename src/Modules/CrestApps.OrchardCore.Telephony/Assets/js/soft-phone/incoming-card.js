// @ts-check
/*
 * The labels of a matched-record card's actions in the incoming-call panel.
 *
 * A card's actions used to always read "Answer & open" and "Open", whatever the card opened. The Contact Center now
 * points the card for the offered activity's customer at that activity -- the notes and disposition for the call --
 * and a generic "open" no longer tells the agent where it goes, so a card may name its own actions.
 *
 * Part of the soft phone: an ES module that soft-phone.js, or another of its helpers, imports, and that the unit tests
 * import directly.
 */

function pick(value, fallback) {
    return typeof value === 'string' && value.trim() ? value : fallback;
}

// { answerAndOpen, open }: the card's own labels, else the phone's generic ones.
export function incomingCardActionLabels(card, strings) {
    card = card || {};
    strings = strings || {};

    return {
        answerAndOpen: pick(card.answerAndOpenText, pick(strings.answerAndOpen, 'Answer & open')),
        open: pick(card.openText, pick(strings.open, 'Open'))
    };
}
