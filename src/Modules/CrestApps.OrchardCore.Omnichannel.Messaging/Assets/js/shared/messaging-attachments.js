/*
 * The decisions the composer makes about the files an agent attaches, kept free of the DOM so they can be tested.
 *
 * Each channel says which file formats it carries (SMS: pictures; email could add documents), how many per message
 * and how large they may be together. The composer offers only those formats and holds a message to those limits.
 * A channel whose carriers cap the message size, as SMS does, also has its still pictures shrunk to their share of the
 * budget rather than refused. An animated GIF cannot be redrawn without losing its animation, so it is sent as it is
 * or not at all, and so is every file that is not a picture.
 *
 * Concatenated ahead of the scripts that use it by the module asset pipeline. It attaches to a shared namespace
 * rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var messaging = root.CrestAppsMessaging = root.CrestAppsMessaging || {};

    function extensionOf(name) {
        var match = /\.[^.\\/]+$/.exec(String(name || ''));

        return match ? match[0].toLowerCase() : '';
    }

    // The channel format a file is, by its declared type or, when the browser gave none it knows, by its extension.
    // The server decides for certain from the bytes; this only keeps the composer from offering what will be refused.
    function findFormat(file, formats) {
        if (!file) { return null; }

        var type = String(file.type || '').toLowerCase();
        var extension = extensionOf(file.name);

        return (formats || []).filter(function (format) {
            return (type && String(format.contentType).toLowerCase() === type) ||
                (extension && (format.extensions || []).some(function (item) { return String(item).toLowerCase() === extension; }));
        })[0] || null;
    }

    // Splits newly dropped files into the ones that can be attached, the ones the channel does not carry, and the
    // ones that do not fit because the message already carries as many as it may.
    function planAttachments(existingCount, incoming, maxCount, formats) {
        var accepted = [];
        var notAllowed = [];
        var overCount = [];
        var room = Math.max(0, (maxCount || 0) - (existingCount || 0));

        Array.prototype.forEach.call(incoming || [], function (file) {
            if (!findFormat(file, formats)) {
                notAllowed.push(file);
            } else if (accepted.length < room) {
                accepted.push(file);
            } else {
                overCount.push(file);
            }
        });

        return { accepted: accepted, notAllowed: notAllowed, overCount: overCount };
    }

    // Each file's share of the message's size budget.
    function perFileBudget(maxBytes, count) {
        if (!maxBytes || maxBytes <= 0) {
            return 0;
        }

        return Math.floor(maxBytes / Math.max(1, count || 1));
    }

    // Whether a file must be redrawn smaller to fit its share: only a still picture, on a channel that shrinks them.
    function needsShrinking(file, budget, format, shrinkImages) {
        return !!file && !!format && !!shrinkImages && !!format.canShrink && budget > 0 && file.size > budget;
    }

    // The size a picture is redrawn at so its longer edge is at most maxEdge, keeping its proportions. A picture that
    // is already small enough keeps its size.
    function fitDimensions(width, height, maxEdge) {
        var longest = Math.max(width || 0, height || 0);

        if (!longest || !maxEdge || longest <= maxEdge) {
            return { width: width || 0, height: height || 0 };
        }

        var scale = maxEdge / longest;

        return {
            width: Math.max(1, Math.round(width * scale)),
            height: Math.max(1, Math.round(height * scale)),
        };
    }

    // The attempts made to fit a picture, largest first: a quality step, then a smaller edge, until one fits.
    function shrinkSteps() {
        return [
            { maxEdge: 2048, quality: 0.85 },
            { maxEdge: 1600, quality: 0.8 },
            { maxEdge: 1280, quality: 0.75 },
            { maxEdge: 1024, quality: 0.7 },
            { maxEdge: 800, quality: 0.65 },
            { maxEdge: 640, quality: 0.6 },
        ];
    }

    function totalSize(files) {
        return Array.prototype.reduce.call(files || [], function (sum, file) {
            return sum + ((file && file.size) || 0);
        }, 0);
    }

    // The value of the file picker's accept attribute for the channel's formats.
    function acceptAttribute(formats) {
        var values = [];

        (formats || []).forEach(function (format) {
            [format.contentType].concat(format.extensions || []).forEach(function (value) {
                if (value && values.indexOf(value) < 0) { values.push(value); }
            });
        });

        return values.join(',');
    }

    messaging.findAttachmentFormat = findFormat;
    messaging.planAttachments = planAttachments;
    messaging.perFileBudget = perFileBudget;
    messaging.needsShrinking = needsShrinking;
    messaging.fitDimensions = fitDimensions;
    messaging.shrinkSteps = shrinkSteps;
    messaging.totalAttachmentSize = totalSize;
    messaging.attachmentAcceptAttribute = acceptAttribute;
}(typeof globalThis !== 'undefined' ? globalThis : window));
