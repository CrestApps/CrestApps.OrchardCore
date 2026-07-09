/*
 * Keeps the Contact Center soft-phone tab and inbound-offer recovery on the real-time hub so queue
 * sign-in/sign-out do not reload the page and a reconnect restores only the still-valid offer.
 */
(function (window, document) {
    'use strict';

    function isBlockingActiveCall(call) {
        if (!call) {
            return false;
        }

        var state = call.state;

        if (typeof state === 'number') {
            state = ['Idle', 'Connecting', 'Ringing', 'Connected', 'OnHold', 'Disconnected', 'Failed'][state] || 'Idle';
        }

        var inbound = call.direction === 1 || call.direction === 'Inbound';

        return (state === 'Connecting' || state === 'Connected' || state === 'OnHold') ||
            (state === 'Ringing' && !inbound);
    }

    function getSelectedValues(select) {
        if (!select) {
            return [];
        }

        return Array.prototype.slice.call(select.options)
            .filter(function (option) { return option.selected && !option.disabled; })
            .map(function (option) { return option.value; });
    }

    function applySelectedValues(select, values) {
        if (!select) {
            return;
        }

        var selected = new Set((values || []).map(function (value) { return String(value); }));

        Array.prototype.forEach.call(select.options, function (option) {
            option.selected = selected.has(option.value);
        });
    }

    function refreshPickers(root) {
        if (!window.jQuery || !root) {
            return;
        }

        var $pickers = window.jQuery(root).find('.selectpicker');

        if ($pickers.length && typeof $pickers.selectpicker === 'function') {
            $pickers.selectpicker('refresh');
        }
    }

    function setBusy(root, busy) {
        Array.prototype.forEach.call(root.querySelectorAll('button, select'), function (element) {
            element.disabled = busy;
        });
    }

    function updateMembershipUi(root, snapshot) {
        if (!root || !snapshot) {
            return;
        }

        var isSignedIn = !!(snapshot.queueIds && snapshot.queueIds.length) || !!(snapshot.campaignIds && snapshot.campaignIds.length);
        var signedInText = root.getAttribute('data-contact-center-signed-in-text') || 'Signed in';
        var offlineText = root.getAttribute('data-contact-center-offline-text') || 'Offline';
        var badge = root.querySelector('[data-contact-center-membership-badge]');
        var signInPanel = root.querySelector('[data-contact-center-sign-in-panel]');
        var signOutPanel = root.querySelector('[data-contact-center-sign-out-panel]');
        var queueSelect = root.querySelector('select[name="selectedQueueIds"]');
        var campaignSelect = root.querySelector('select[name="selectedCampaignIds"]');

        if (badge) {
            badge.classList.toggle('text-bg-success', isSignedIn);
            badge.classList.toggle('text-bg-secondary', !isSignedIn);
            badge.textContent = isSignedIn ? signedInText : offlineText;
        }

        if (signInPanel) {
            signInPanel.hidden = isSignedIn;
        }

        if (signOutPanel) {
            signOutPanel.hidden = !isSignedIn;
        }

        applySelectedValues(queueSelect, snapshot.queueIds || []);
        applySelectedValues(campaignSelect, snapshot.campaignIds || []);
        refreshPickers(root);
    }

    function invokeHub(client, method) {
        if (!client || !client.connection || !client.started) {
            return Promise.resolve(null);
        }

        var args = Array.prototype.slice.call(arguments, 2);

        return client.started.then(function () {
            return client.connection.invoke.apply(client.connection, [method].concat(args));
        });
    }

    function postQueuedVoiceSync(root, api, client) {
        if (!root || !api || !client) {
            return Promise.resolve();
        }

        var connection = api.getConnection && api.getConnection();

        if (!connection || connection.state !== 'Connected') {
            return Promise.resolve();
        }

        if (api.__contactCenterQueuedVoiceSyncPromise) {
            return api.__contactCenterQueuedVoiceSyncPromise;
        }

        api.__contactCenterQueuedVoiceSyncPromise = invokeHub(client, 'SyncQueuedVoiceWork')
            .catch(function () { })
            .finally(function () {
                api.__contactCenterQueuedVoiceSyncPromise = null;
            });

        return api.__contactCenterQueuedVoiceSyncPromise;
    }

    function restorePendingIncomingOffer(root, api, client) {
        if (!root || !api || !client || typeof api.setIncomingOffer !== 'function') {
            return Promise.resolve(null);
        }

        var connection = api.getConnection && api.getConnection();
        var currentCall = api.getCurrentCall && api.getCurrentCall();

        if (!connection || connection.state !== 'Connected' || isBlockingActiveCall(currentCall)) {
            return Promise.resolve(null);
        }

        if (api.__contactCenterPendingOfferPromise) {
            return api.__contactCenterPendingOfferPromise;
        }

        api.__contactCenterPendingOfferPromise = invokeHub(client, 'GetCurrentIncomingOffer')
            .then(function (offer) {
                if (offer && offer.call && !isBlockingActiveCall(api.getCurrentCall && api.getCurrentCall())) {
                    api.setIncomingOffer(offer.call, offer.context || null);
                }

                return offer;
            })
            .catch(function () {
                return null;
            })
            .finally(function () {
                api.__contactCenterPendingOfferPromise = null;
            });

        return api.__contactCenterPendingOfferPromise;
    }

    function recoverSoftPhoneState(root, api, client) {
        return restorePendingIncomingOffer(root, api, client)
            .then(function (offer) {
                return offer ? null : postQueuedVoiceSync(root, api, client);
            });
    }

    function bindMembershipForms(root, api, client) {
        if (!root || root.__contactCenterMembershipBound) {
            return;
        }

        root.__contactCenterMembershipBound = true;

        var signInForm = root.querySelector('[data-contact-center-sign-in-form]');
        var signOutForm = root.querySelector('[data-contact-center-sign-out-form]');
        var queueSelect = root.querySelector('select[name="selectedQueueIds"]');
        var campaignSelect = root.querySelector('select[name="selectedCampaignIds"]');

        if (signInForm) {
            signInForm.addEventListener('submit', function (event) {
                if (!client) {
                    return;
                }

                event.preventDefault();
                setBusy(root, true);

                invokeHub(client, 'SignIn', getSelectedValues(queueSelect), getSelectedValues(campaignSelect))
                    .then(function (snapshot) {
                        updateMembershipUi(root, snapshot);

                        return recoverSoftPhoneState(root, api, client);
                    })
                    .catch(function (error) {
                        if (api && typeof api.showError === 'function') {
                            api.showError(error && error.message ? error.message : String(error));
                        }
                    })
                    .finally(function () {
                        setBusy(root, false);
                    });
            });
        }

        if (signOutForm) {
            signOutForm.addEventListener('submit', function (event) {
                if (!client) {
                    return;
                }

                event.preventDefault();
                setBusy(root, true);

                invokeHub(client, 'SignOut')
                    .then(function (snapshot) {
                        updateMembershipUi(root, snapshot);

                        if (api && typeof api.clearIncomingOffer === 'function') {
                            api.clearIncomingOffer();
                        }
                    })
                    .catch(function (error) {
                        if (api && typeof api.showError === 'function') {
                            api.showError(error && error.message ? error.message : String(error));
                        }
                    })
                    .finally(function () {
                        setBusy(root, false);
                    });
            });
        }
    }

    function wireSoftPhone(root, api) {
        if (!root || !api || api.__contactCenterQueuedVoiceSyncBound) {
            return;
        }

        var hubUrl = root.getAttribute('data-contact-center-hub-url');
        var client = window.contactCenterRealTime && hubUrl
            ? window.contactCenterRealTime.connect({
                hubUrl: hubUrl,
                onSnapshot: function (snapshot) {
                    updateMembershipUi(root, snapshot);
                }
            })
            : null;

        api.__contactCenterQueuedVoiceSyncBound = true;
        bindMembershipForms(root, api, client);

        if (client && client.connection) {
            client.connection.on('OfferReceived', function () {
                recoverSoftPhoneState(root, api, client);
            });

            client.connection.on('OfferRevoked', function (notification) {
                if (typeof api.clearIncomingOffer === 'function') {
                    var reason = notification && notification.reason;
                    var accepted = reason === 2 || reason === 'Accepted';
                    var acceptPending = typeof api.isIncomingAcceptPending === 'function' && api.isIncomingAcceptPending();

                    api.clearIncomingOffer({
                        preserveCurrentCall: accepted && acceptPending,
                        preservePendingAccept: accepted && acceptPending
                    });
                }
            });
        }

        api.started.then(function () {
            if (!client) {
                return null;
            }

            return client.started;
        }).then(function () {
            return recoverSoftPhoneState(root, api, client);
        }).then(function () {
            var connection = api.getConnection && api.getConnection();

            if (connection && typeof connection.onreconnected === 'function') {
                connection.onreconnected(function () {
                    return recoverSoftPhoneState(root, api, client);
                });
            }
        }).catch(function () { });
    }

    function connectSoftPhone(root, attemptsRemaining) {
        var api = window.telephonySoftPhone &&
            window.telephonySoftPhone.getInstance &&
            window.telephonySoftPhone.getInstance();

        if (api) {
            wireSoftPhone(root, api);

            return;
        }

        if (attemptsRemaining <= 0) {
            return;
        }

        window.setTimeout(function () {
            connectSoftPhone(root, attemptsRemaining - 1);
        }, 100);
    }

    function initialize() {
        document.querySelectorAll('[data-contact-center-hub-url]').forEach(function (root) {
            connectSoftPhone(root, 30);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialize, { once: true });
    } else {
        initialize();
    }
})(window, document);
