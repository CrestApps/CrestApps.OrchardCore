// Escapes a value for use in HTML text and in a quoted attribute: all five of & < > " '. The textContent-then-innerHTML
// trick leaves quotes alone, which is safe in text but lets a quote close an attribute, so it is not used here.
export function escapeHtml(value) {
    return String(value === undefined || value === null ? '' : value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}
