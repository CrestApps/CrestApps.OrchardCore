// crestapps-browser: the small helpers every CrestApps page script needs, in one place and exposed as window.crestapps,
// instead of a copy per module that each behaved a little differently. Require the resource and use them:
//
//   crestapps.html.escape(value)                 & < > " '
//   crestapps.text.format(template, ...args)     '{0} of {1}'
//   crestapps.http.request(url, { method, json, form, signal })
//   crestapps.confirm(title, message)            the admin theme's dialog in the admin, the browser's confirm elsewhere
//
// It also binds <form data-confirm="..."> and <button type="submit" data-confirm="..."> (asks before submitting, with
// data-confirm-title, data-confirm-ok and data-confirm-cancel optional) and <form data-once> (submits once).
import { bindForms, confirmAction } from './confirm.js';
import { escapeHtml } from './html.js';
import { antiforgeryToken, HttpError, request } from './http.js';
import { format } from './text.js';

(function (window, document) {
    'use strict';

    // Required by several shapes on one page, the script can be included more than once; bind the forms once.
    if (window.crestapps && window.crestapps.http) {
        return;
    }

    window.crestapps = Object.assign(window.crestapps || {}, {
        html: { escape: escapeHtml },
        text: { format },
        http: { request, HttpError, antiforgeryToken },
        confirm: confirmAction,
    });

    bindForms(document);
})(window, document);
