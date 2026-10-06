/*
 * Contact Center call recording player.
 *
 * Enhances the call recording page: the play button and the time beside each transcript line play the recording
 * from where the line was said, the line being heard is highlighted as the recording plays, and the copy button
 * copies a line as "hh:mm:ss SPEAKER text". The player seeks with HTTP byte ranges, which the media endpoint
 * honors. Localized strings are read from the root element's data-config attribute.
 *
 * On the call recordings list, a [data-cc-list-play] button plays that recording in a player opened under its row;
 * one recording plays at a time, and pressing the button again closes the player.
 */
(function (window, document) {
    'use strict';

    var activeClass = 'bg-primary-subtle';

    function parseConfig(root) {
        try {
            var config = JSON.parse(root.getAttribute('data-config') || '{}');
            config.strings = config.strings || {};

            return config;
        } catch (error) {
            return { strings: {} };
        }
    }

    function offsetOf(phrase) {
        var value = parseFloat(phrase.getAttribute('data-offset'));

        return isNaN(value) ? null : value;
    }

    function copyText(text) {
        if (navigator.clipboard && window.isSecureContext) {
            return navigator.clipboard.writeText(text);
        }

        return new Promise(function (resolve, reject) {
            var area = document.createElement('textarea');
            area.value = text;
            area.setAttribute('readonly', '');
            area.style.position = 'fixed';
            area.style.opacity = '0';
            document.body.appendChild(area);
            area.select();

            try {
                if (document.execCommand('copy')) {
                    resolve();
                } else {
                    reject(new Error('copy refused'));
                }
            } catch (error) {
                reject(error);
            } finally {
                document.body.removeChild(area);
            }
        });
    }

    function flashTitle(button, text) {
        var original = button.getAttribute('data-original-title') || button.getAttribute('title') || '';
        button.setAttribute('data-original-title', original);
        button.setAttribute('title', text);
        button.classList.add('text-success');

        window.setTimeout(function () {
            button.setAttribute('title', original);
            button.classList.remove('text-success');
        }, 1500);
    }

    function initialize(root) {
        var strings = parseConfig(root).strings;
        var player = root.querySelector('[data-cc-recording-player]');
        var phrases = Array.prototype.slice.call(root.querySelectorAll('[data-cc-phrase]'))
            .filter(function (phrase) {
                return offsetOf(phrase) !== null;
            });
        var active = null;

        function playFrom(seconds) {
            if (!player) {
                return;
            }

            player.currentTime = seconds;

            var playing = player.play();

            if (playing && typeof playing.catch === 'function') {
                playing.catch(function () {
                    // The browser refused to start playback; the position is still set for the user to press play.
                });
            }
        }

        function highlight() {
            if (!player) {
                return;
            }

            var now = player.currentTime;
            var current = null;

            // The line being heard is the last one that began at or before the playback position.
            for (var i = 0; i < phrases.length; i++) {
                if (offsetOf(phrases[i]) <= now + 0.05) {
                    current = phrases[i];
                } else {
                    break;
                }
            }

            if (current === active) {
                return;
            }

            if (active) {
                active.classList.remove(activeClass);
            }

            active = current;

            if (active) {
                active.classList.add(activeClass);
            }
        }

        root.addEventListener('click', function (event) {
            var seek = event.target.closest('[data-cc-seek], [data-cc-play]');

            if (seek && root.contains(seek)) {
                event.preventDefault();

                var phrase = seek.closest('[data-cc-phrase]');
                var offset = phrase ? offsetOf(phrase) : null;

                if (offset !== null) {
                    playFrom(offset);
                }

                return;
            }

            var copy = event.target.closest('[data-cc-copy]');

            if (copy && root.contains(copy)) {
                event.preventDefault();

                copyText(copy.getAttribute('data-cc-copy')).then(function () {
                    flashTitle(copy, strings.copied || 'Copied');
                }, function () {
                    flashTitle(copy, strings.copyFailed || 'Could not copy');
                });
            }
        });

        if (player) {
            player.addEventListener('timeupdate', highlight);
            player.addEventListener('seeked', highlight);
        }
    }

    function closeListPlayer(button) {
        var row = button.closest('li');
        var holder = row ? row.querySelector('[data-cc-list-player]') : null;

        if (holder) {
            var audio = holder.querySelector('audio');

            if (audio) {
                audio.pause();
                audio.removeAttribute('src');
                audio.load();
            }

            holder.remove();
        }

        button.classList.remove('active');
        button.setAttribute('aria-pressed', 'false');
    }

    function initializeList(list) {
        list.addEventListener('click', function (event) {
            var button = event.target.closest('[data-cc-list-play]');

            if (!button || !list.contains(button)) {
                return;
            }

            event.preventDefault();

            var wasOpen = button.classList.contains('active');

            // One recording plays at a time.
            list.querySelectorAll('[data-cc-list-play].active').forEach(closeListPlayer);

            if (wasOpen) {
                return;
            }

            var row = button.closest('li');

            if (!row) {
                return;
            }

            var holder = document.createElement('div');
            holder.className = 'mt-2';
            holder.setAttribute('data-cc-list-player', '');

            var audio = document.createElement('audio');
            audio.controls = true;
            audio.autoplay = true;
            audio.preload = 'auto';
            audio.className = 'w-100';
            audio.src = button.getAttribute('data-cc-list-play');

            holder.appendChild(audio);
            row.appendChild(holder);

            button.classList.add('active');
            button.setAttribute('aria-pressed', 'true');
        });
    }

    function start() {
        document.querySelectorAll('[data-cc-call-recording]').forEach(initialize);
        document.querySelectorAll('[data-cc-call-recording-list]').forEach(initializeList);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})(window, document);
