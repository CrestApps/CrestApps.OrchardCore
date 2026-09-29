/*
 * The decisions the composer makes about pictures an agent attaches, kept free of the DOM so they can be tested.
 *
 * A picture message is held to what the channel carries: a number of pictures and a total size. A phone photo is
 * usually several times larger than a carrier accepts, so the composer shrinks a still picture to its share of the
 * budget rather than refusing it. An animated GIF cannot be redrawn without losing its animation, so it is sent as it
 * is or not at all.
 *
 * Concatenated ahead of the scripts that use it by the module asset pipeline. It attaches to a shared namespace
 * rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var messaging = root.CrestAppsMessaging = root.CrestAppsMessaging || {};

    var sendableTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'];

    function isSendableImageType(type) {
        return sendableTypes.indexOf(String(type || '').toLowerCase()) >= 0;
    }

    // Splits newly dropped files into the pictures that can be attached, the files that are not pictures, and the
    // pictures that do not fit because the message already carries as many as it may.
    function planAttachments(existingCount, incoming, maxCount) {
        var accepted = [];
        var notImages = [];
        var overCount = [];
        var room = Math.max(0, (maxCount || 0) - (existingCount || 0));

        Array.prototype.forEach.call(incoming || [], function (file) {
            if (!file || !isSendableImageType(file.type)) {
                notImages.push(file);
            } else if (accepted.length < room) {
                accepted.push(file);
            } else {
                overCount.push(file);
            }
        });

        return { accepted: accepted, notImages: notImages, overCount: overCount };
    }

    // Each picture's share of the message's size budget.
    function perFileBudget(maxBytes, count) {
        if (!maxBytes || maxBytes <= 0) {
            return 0;
        }

        return Math.floor(maxBytes / Math.max(1, count || 1));
    }

    // Whether a picture must be redrawn smaller to fit its share. A GIF never is: it is sent whole or refused.
    function needsShrinking(file, budget) {
        return !!file && budget > 0 && file.size > budget && String(file.type).toLowerCase() !== 'image/gif';
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

    messaging.isSendableImageType = isSendableImageType;
    messaging.planAttachments = planAttachments;
    messaging.perFileBudget = perFileBudget;
    messaging.needsShrinking = needsShrinking;
    messaging.fitDimensions = fitDimensions;
    messaging.shrinkSteps = shrinkSteps;
    messaging.totalAttachmentSize = totalSize;
}(typeof globalThis !== 'undefined' ? globalThis : window));
