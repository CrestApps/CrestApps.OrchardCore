// Fills numbered placeholders: format('{0} of {1}', 3, 10) is "3 of 10". A placeholder with no value stays as it is, so
// a missing argument shows up on the screen instead of becoming "undefined".
export function format(template, ...args) {
    if (template === undefined || template === null) {
        return '';
    }

    return String(template).replace(/\{(\d+)\}/g, (match, index) => (args[index] !== undefined ? String(args[index]) : match));
}
