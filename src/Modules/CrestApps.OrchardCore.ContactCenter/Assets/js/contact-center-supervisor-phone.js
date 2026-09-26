/*
 * A supervisor's own engagement, in their soft phone.
 *
 * When the supervisor listens to, coaches or joins a call from the live dashboard, the server tells this phone (over
 * the Contact Center hub) the token of the leg it is about to ring it with. The phone arms for that token, answers
 * that leg by itself (soft-phone/monitor-leg.js), and never shows it as a call of its own: this banner shows it
 * instead -- who is being monitored, a Listen / Whisper / Barge switcher that changes the mode on the same leg, and
 * Stop. After a takeover the call is the supervisor's, and Stop becomes Hang up.
 *
 * Concatenated after shared/supervisor-actions.js, which decides how the banner reads.
 */
(function (window, document) {
    'use strict';

    var contactCenter = window.CrestAppsContactCenter || {};

    function readStrings(banner) {
        try {
            return JSON.parse(banner.getAttribute('data-cc-monitor-strings') || '{}') || {};
        } catch (error) {
            return {};
        }
    }

    // The banner is shown where the phone shows its calls, whatever tab is open, so it is placed above the call list.
    function place(banner) {
        var phone = banner.closest('.telephony-soft-phone') || document;
        var anchor = phone.querySelector('[data-telephony-active-calls]') || phone.querySelector('[data-telephony-error]');

        if (anchor && anchor.parentNode && anchor.previousElementSibling !== banner) {
            anchor.parentNode.insertBefore(banner, anchor);
        }
    }

    function wire(banner, api) {
        if (!banner || !api || banner.__ccMonitorBound || typeof api.armMonitorLeg !== 'function') {
            return;
        }

        banner.__ccMonitorBound = true;

        var strings = readStrings(banner);
        var hubUrl = banner.getAttribute('data-cc-monitor-hub-url');
        var client = window.contactCenterRealTime && hubUrl
            ? window.contactCenterRealTime.connect({ hubUrl: hubUrl })
            : null;
        var engagement = null;
        var pending = false;

        place(banner);

        function render() {
            var html = typeof contactCenter.monitorBannerHtml === 'function'
                ? contactCenter.monitorBannerHtml(engagement, strings)
                : '';

            banner.innerHTML = html;
            banner.hidden = !html;
            banner.setAttribute('data-cc-monitor-phase', engagement ? engagement.phase : '');

            if (pending) {
                banner.querySelectorAll('button').forEach(function (button) {
                    button.disabled = true;
                });
            }
        }

        function apply(event) {
            var previous = engagement;

            engagement = contactCenter.nextMonitorEngagement(engagement, event);

            if (!engagement) {
                return;
            }

            if (engagement.phase === 'requested' && engagement.token && (!previous || previous.token !== engagement.token)) {
                api.armMonitorLeg(engagement.token, {
                    interactionId: engagement.interactionId,
                    agentName: engagement.agentName,
                    mode: engagement.mode
                });
            }

            // Whether the supervisor is heard follows the mode: silent while listening, heard while coaching or joining,
            // and on a call they took over. Live, the phone kept its microphone off on every mode.
            if (engagement.phase !== 'ended' && engagement.token && typeof api.setMonitorLegMode === 'function' &&
                (!previous || previous.token !== engagement.token || previous.mode !== engagement.mode)) {
                api.setMonitorLegMode(engagement.token, engagement.mode);
            }

            if (engagement.phase === 'ended' && previous && previous.phase !== 'ended') {
                // Nothing is expected any more, and a leg still up is let go.
                api.disarmMonitorLeg(engagement.token);
                api.hangupMonitorLeg(engagement.token);
            }

            render();
        }

        function invoke(method) {
            var args = Array.prototype.slice.call(arguments, 1);

            if (!client || !client.connection) {
                return Promise.reject(new Error(strings.unavailable || 'The Contact Center connection is not available.'));
            }

            return Promise.resolve(client.started).then(function () {
                return client.connection.invoke.apply(client.connection, [method].concat(args));
            });
        }

        function run(operation) {
            pending = true;
            render();

            return operation()
                .then(function (result) {
                    if (result && result.succeeded === false && typeof api.showError === 'function') {
                        api.showError(result.reason || strings.failed || 'The supervisor action could not be completed.');
                    }

                    return result;
                })
                .catch(function (error) {
                    if (typeof api.showError === 'function') {
                        api.showError(error && error.message ? error.message : String(error));
                    }
                })
                .finally(function () {
                    pending = false;
                    render();
                });
        }

        if (client && client.connection) {
            client.connection.on('SupervisorEngagementChanged', function (notification) {
                apply({ source: 'hub', notification: notification });
            });
        }

        api.onMonitorLeg(function (event) {
            apply({ source: 'phone', type: event && event.type, token: event && event.token });
        });

        banner.addEventListener('click', function (event) {
            if (!engagement || pending) {
                return;
            }

            var modeButton = event.target.closest('[data-cc-monitor-mode]');
            var stopButton = event.target.closest('[data-cc-monitor-stop]');
            var current = engagement;

            if (modeButton) {
                var mode = modeButton.getAttribute('data-cc-monitor-mode');
                var modeIndex = (contactCenter.MONITOR_MODES || []).indexOf(mode);

                if (modeIndex < 0 || mode === current.mode) {
                    return;
                }

                run(function () {
                    return invoke('SwitchMonitorMode', current.interactionId, modeIndex);
                });

                return;
            }

            if (!stopButton) {
                return;
            }

            if (current.phase === 'tookOver') {
                // The call is the supervisor's: hanging up their leg ends it, like any agent hanging up.
                api.hangupMonitorLeg(current.token);

                return;
            }

            run(function () {
                return invoke('StopMonitoring', current.interactionId).then(function (result) {
                    // Stopped or not, the supervisor asked to stop hearing the call: their own leg goes either way.
                    api.hangupMonitorLeg(current.token);

                    return result;
                });
            });
        });

        render();
    }

    function connect(banner, attemptsRemaining) {
        var api = window.telephonySoftPhone &&
            window.telephonySoftPhone.getInstance &&
            window.telephonySoftPhone.getInstance();

        if (api) {
            wire(banner, api);

            return;
        }

        if (attemptsRemaining > 0) {
            window.setTimeout(function () {
                connect(banner, attemptsRemaining - 1);
            }, 100);
        }
    }

    function initialize() {
        document.querySelectorAll('[data-cc-monitor-banner]').forEach(function (banner) {
            connect(banner, 50);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialize, { once: true });
    } else {
        initialize();
    }
})(window, document);
