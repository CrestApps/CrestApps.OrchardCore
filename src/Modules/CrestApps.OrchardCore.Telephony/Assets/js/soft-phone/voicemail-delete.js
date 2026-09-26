/*
 * Deleting the voicemails an agent selected, and saying which ones did not go and why.
 *
 * The phone used to fire every delete at once and reload the list whatever came back. A refusal answered with a
 * redirect was followed by the browser to a page that said 200, so a voicemail the server would not delete simply
 * stayed in the list with no word about it. Here each answer is read for what it is: only a 2xx that was not reached
 * through a redirect counts as deleted, and every other answer carries a reason the agent can act on. The requests go
 * one at a time, so a single inbox is never written by several deletes at once.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    var DEFAULT_TEXT = {
        refused: 'The server refused the request. Reload the page and try again.',
        expired: 'The page is out of date. Reload it and try again.',
        signedOut: 'You are signed out. Sign in again and retry.',
        forbidden: 'You do not have access to this voicemail.',
        notFound: 'This voicemail no longer exists.',
        legalHold: 'This voicemail is under legal hold and must be kept.',
        network: 'The server could not be reached.',
        failed: 'The server could not delete it.'
    };

    var STRING_KEYS = {
        refused: 'voicemailDeleteRefused',
        expired: 'voicemailDeleteExpired',
        signedOut: 'voicemailDeleteSignedOut',
        forbidden: 'voicemailDeleteForbidden',
        notFound: 'voicemailDeleteNotFound',
        legalHold: 'voicemailDeleteLegalHold',
        network: 'voicemailDeleteNetwork',
        failed: 'voicemailDeleteServerFailed'
    };

    // Why a delete's answer is not a deletion, or null when it is one. The answer is a fetch Response (or anything
    // shaped like one); the request is sent with redirect: 'manual', so a redirect arrives as an opaque redirect.
    function voicemailDeleteFailureReason(response) {
        if (!response) {
            return 'network';
        }

        var status = response.status;

        if (response.type === 'opaqueredirect' || response.redirected === true || (status >= 300 && status < 400)) {
            return 'refused';
        }

        if (status >= 200 && status < 300) {
            return null;
        }

        switch (status) {
            case 400:
                return 'expired';
            case 401:
                return 'signedOut';
            case 403:
                return 'forbidden';
            case 404:
                return 'notFound';
            case 409:
                return 'legalHold';
            default:
                return 'failed';
        }
    }

    // Deletes the voicemails one after another through deleteOne(id), which returns a promise of the server's answer,
    // and resolves to { deleted: [ids], failed: [{ id, reason }] } once every one has been tried.
    function deleteVoicemailsInTurn(ids, deleteOne) {
        var outcome = { deleted: [], failed: [] };

        return (ids || []).reduce(function (previous, id) {
            if (!id) {
                return previous;
            }

            return previous.then(function () {
                return Promise.resolve()
                    .then(function () { return deleteOne(id); })
                    .then(voicemailDeleteFailureReason, function () { return 'network'; })
                    .then(function (reason) {
                        if (reason) {
                            outcome.failed.push({ id: id, reason: reason });
                        } else {
                            outcome.deleted.push(id);
                        }
                    });
            });
        }, Promise.resolve()).then(function () {
            return outcome;
        });
    }

    // The sentence shown beside a voicemail that was not deleted.
    function voicemailDeleteFailureText(reason, strings) {
        var key = Object.prototype.hasOwnProperty.call(DEFAULT_TEXT, reason) ? reason : 'failed';
        var localized = strings ? strings[STRING_KEYS[key]] : null;

        return localized || DEFAULT_TEXT[key];
    }

    // The message for the whole attempt: null when every voicemail was deleted; otherwise one voicemail's reason, or
    // how many of the selected voicemails were left behind (each row then carries its own reason).
    function describeVoicemailDeleteFailures(outcome, strings) {
        var failed = outcome && outcome.failed ? outcome.failed : [];

        if (!failed.length) {
            return null;
        }

        var total = failed.length + (outcome.deleted ? outcome.deleted.length : 0);

        if (total === 1) {
            return ((strings && strings.voicemailDeleteFailed) || 'The voicemail could not be deleted.') + ' ' +
                voicemailDeleteFailureText(failed[0].reason, strings);
        }

        var template = (strings && strings.voicemailDeletePartiallyFailed) ||
            '{0} of {1} voicemails could not be deleted. They are still selected.';

        return template.replace('{0}', String(failed.length)).replace('{1}', String(total));
    }

    softPhone.voicemailDeleteFailureReason = voicemailDeleteFailureReason;
    softPhone.deleteVoicemailsInTurn = deleteVoicemailsInTurn;
    softPhone.voicemailDeleteFailureText = voicemailDeleteFailureText;
    softPhone.describeVoicemailDeleteFailures = describeVoicemailDeleteFailures;
}(typeof globalThis !== 'undefined' ? globalThis : window));
