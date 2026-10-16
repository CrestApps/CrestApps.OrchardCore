// @ts-check
/*
 * The rules the composer's To line applies to an address typed for someone who is not in the contacts, kept free of
 * the DOM so they can be tested.
 *
 * A contact's stored address is trusted as it is, but a typed one is sent exactly as written, so it is offered only
 * once it is already in the form the channel needs. On a phone channel that is E.164: a '+', the country code and the
 * subscriber number, digits only. A national number is never guessed into a country. The server applies the same rule
 * (and checks the number against the numbering plan); this only keeps the composer from offering what it will refuse.
 *
 * An ES module: the scripts that use it import it, and the asset build bundles it into each of them; the unit tests
 * import it directly.
 */

var e164Pattern = /^\+[1-9]\d{6,14}$/;

/**
 * Whether the text is a phone number in E.164 form, exactly as typed.
 * @param {unknown} text
 * @returns {boolean}
 */
export function isE164(text) {
    return typeof text === 'string' && e164Pattern.test(text);
}

/**
 * Whether the text reads as an attempt at a phone number: digits, perhaps with the punctuation people write numbers
 * with, and no letters.
 * @param {unknown} text
 * @returns {boolean}
 */
export function looksLikePhoneNumber(text) {
    var value = typeof text === 'string' ? text.trim() : '';

    return /\d/.test(value) && /^[+\d\s().-]+$/.test(value);
}

/**
 * Whether a search result that is not a contact (a typed address the server accepted) may be offered for the term.
 * It must be the term itself, not a reshaped version of it, and on a phone channel it must be E.164.
 * @param {{ id?: string | null, address?: string | null }} item The search result.
 * @param {string} term The text typed in the search box.
 * @param {boolean} isPhoneChannel Whether the channel's addresses are phone numbers.
 * @returns {boolean}
 */
export function acceptsTypedAddress(item, term, isPhoneChannel) {
    if (!item || item.id || !item.address) { return false; }

    var typed = String(term || '').trim();

    if (item.address.toLowerCase() !== typed.toLowerCase()) { return false; }

    return !isPhoneChannel || isE164(typed);
}

/**
 * Whether to show the hint that a number outside the contacts must be in international format: on a phone channel,
 * when the term looks like a number but none could be offered for it.
 * @param {string} term The text typed in the search box.
 * @param {boolean} isPhoneChannel Whether the channel's addresses are phone numbers.
 * @param {boolean} offeredTypedAddress Whether the typed address was offered.
 * @returns {boolean}
 */
export function shouldShowPhoneHint(term, isPhoneChannel, offeredTypedAddress) {
    return isPhoneChannel && !offeredTypedAddress && looksLikePhoneNumber(term);
}
