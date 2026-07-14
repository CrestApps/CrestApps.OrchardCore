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

        return !inbound &&
            (state === 'Connecting' || state === 'Ringing' || state === 'Connected' || state === 'OnHold');
    }

    function getSelectedValues(select) {
        if (!select) {
            return [];
        }

        return Array.prototype.slice.call(select.options)
            .filter(function (option) { return option.selected && !option.disabled; })
            .map(function (option) { return option.value; });
    }

    function escapeHtml(value) {
        var node = document.createElement('div');
        node.textContent = value == null ? '' : String(value);

        return node.innerHTML;
    }

    function applySelectedValues(select, values) {
        if (!select) {
            return;
        }

        var selected = new Set((values || []).map(function (value) { return String(value); }));

        Array.prototype.forEach.call(select.options, function (option) {
            option.selected = selected.has(option.value);
        });

        refreshMembershipPicker(select);
    }

    function refreshMembershipPicker(select) {
        if (!select || !window.jQuery || !window.jQuery.fn || typeof window.jQuery.fn.selectpicker !== 'function') {
            return;
        }

        var picker = window.jQuery(select);

        if (picker.data('selectpicker')) {
            picker.selectpicker('refresh');
        }
    }

    function initializeMembershipPickers(root) {
        if (!root || !window.jQuery || !window.jQuery.fn || typeof window.jQuery.fn.selectpicker !== 'function') {
            return;
        }

        root.querySelectorAll('[data-contact-center-picker]').forEach(function (select) {
            var picker = window.jQuery(select);

            if (!picker.data('selectpicker')) {
                picker.selectpicker();
            }
        });
    }

    function setBusy(root, busy) {
        Array.prototype.forEach.call(root.querySelectorAll('button, select'), function (element) {
            element.disabled = busy;

            if (element.matches('select[data-contact-center-picker]')) {
                refreshMembershipPicker(element);
            }
        });
    }

    function setPresenceBusy(busy) {
        Array.prototype.forEach.call(document.querySelectorAll('[data-contact-center-presence-form] button'), function (button) {
            button.disabled = busy;
        });
    }

    function updatePresenceUi(snapshot) {
        if (!snapshot) {
            return;
        }

        var container = document.querySelector('[data-contact-center-presence]');

        if (!container) {
            return;
        }

        var status = snapshot.presenceStatus || 'Offline';
        var reason = snapshot.presenceReason;
        var requested = snapshot.requestedPresenceStatus;
        var option = container.querySelector('[data-presence-status="' + status + '"]');
        var text = container.querySelector('[data-contact-center-presence-text]');
        var pending = container.querySelector('[data-contact-center-pending-presence]');
        var label = reason || (option && option.getAttribute('data-presence-label')) || status;

        if (text) {
            text.textContent = label;
        }

        if (requested === 'Break') {
            if (!pending) {
                pending = document.createElement('span');
                pending.className = 'badge text-bg-warning';
                pending.setAttribute('data-contact-center-pending-presence', '');
                pending.textContent = container.getAttribute('data-break-pending-text') || 'Break pending';
                container.querySelector('[data-contact-center-presence-toggle]').appendChild(pending);
            }
        } else if (pending) {
            pending.remove();
        }
    }

    function showMembershipError(root, api, message) {
        var error = root && root.querySelector('[data-contact-center-membership-error]');

        if (error) {
            error.textContent = message || '';
            error.hidden = !message;
        }

        if (message && api && typeof api.showError === 'function') {
            api.showError(message);
        }
    }

    function getOptionText(select, value) {
        if (!select) {
            return value;
        }

        var option = Array.prototype.find.call(select.options, function (candidate) {
            return candidate.value === String(value);
        });

        return option ? option.text : value;
    }

    function renderMembershipList(root, snapshot, queueSelect, campaignSelect) {
        var list = root.querySelector('[data-contact-center-membership-list]');

        if (!list) {
            return;
        }

        var queueText = root.getAttribute('data-contact-center-queue-text') || 'Queue';
        var campaignText = root.getAttribute('data-contact-center-campaign-text') || 'Campaign';
        var signOutText = root.getAttribute('data-contact-center-sign-out-item-text') || 'Sign out';
        var memberships = [];

        (snapshot.queueIds || []).forEach(function (queueId) {
            memberships.push({
                kind: 'queue',
                id: queueId,
                type: queueText,
                name: getOptionText(queueSelect, queueId)
            });
        });

        (snapshot.campaignIds || []).forEach(function (campaignId) {
            memberships.push({
                kind: 'campaign',
                id: campaignId,
                type: campaignText,
                name: getOptionText(campaignSelect, campaignId)
            });
        });

        list.innerHTML = memberships.map(function (membership) {
            return '<div class="list-group-item px-0 py-2 d-flex align-items-center justify-content-between gap-2">' +
                '<span class="small"><span class="text-body-secondary">' + escapeHtml(membership.type) + ':</span> ' + escapeHtml(membership.name) + '</span>' +
                '<button type="button" class="btn btn-sm btn-outline-secondary" data-contact-center-membership-sign-out data-membership-kind="' + escapeHtml(membership.kind) + '" data-membership-id="' + escapeHtml(membership.id) + '">' + escapeHtml(signOutText) + '</button>' +
            '</div>';
        }).join('');
    }

    function updateMembershipUi(root, snapshot) {
        if (!root || !snapshot) {
            return;
        }

        root.__contactCenterMembershipSnapshot = snapshot;
        updatePresenceUi(snapshot);

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
        renderMembershipList(root, snapshot, queueSelect, campaignSelect);
        showMembershipError(root, null, null);
    }

    function bindPresenceForms(root, api, client) {
        if (!client || document.__contactCenterPresenceBound) {
            return;
        }

        document.__contactCenterPresenceBound = true;

        Array.prototype.forEach.call(document.querySelectorAll('[data-contact-center-presence-form]'), function (form) {
            form.addEventListener('submit', function (event) {
                var formData = new FormData(form);
                var status = event.submitter && event.submitter.getAttribute('data-presence-value');
                var reason = formData.get('presenceReason');

                if (!client.connection || client.connection.state !== 'Connected') {
                    return;
                }

                event.preventDefault();
                setPresenceBusy(true);

                invokeHub(client, 'SetPresence', Number(status), reason)
                    .then(function (snapshot) {
                        updatePresenceUi(snapshot);
                        updateMembershipUi(root, snapshot);

                        if (snapshot && snapshot.presenceStatus === 'Available') {
                            return recoverSoftPhoneState(root, api, client);
                        }

                        return null;
                    })
                    .catch(function (error) {
                        var statusInput = form.querySelector('input[name="status"]');

                        if (!statusInput) {
                            statusInput = document.createElement('input');
                            statusInput.type = 'hidden';
                            statusInput.name = 'status';
                            form.appendChild(statusInput);
                        }

                        statusInput.value = status;
                        HTMLFormElement.prototype.submit.call(form);
                    })
                    .finally(function () {
                        setPresenceBusy(false);
                    });
            });
        });
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

    function restorePendingIncomingOffer(root, api, client, attemptsRemaining) {
        if (!root || !api || !client || typeof api.setIncomingOffer !== 'function') {
            return Promise.resolve(null);
        }

        attemptsRemaining = typeof attemptsRemaining === 'number' ? attemptsRemaining : 3;

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
                if (attemptsRemaining > 0) {
                    return new Promise(function (resolve) {
                        window.setTimeout(resolve, 250);
                    }).then(function () {
                        api.__contactCenterPendingOfferPromise = null;

                        return restorePendingIncomingOffer(root, api, client, attemptsRemaining - 1);
                    });
                }

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

    function openAssignedDialerActivity(root, notification) {
        if (!notification || !notification.autoOpenActivity || !notification.activityItemId) {
            return;
        }

        var template = root.getAttribute('data-contact-center-complete-activity-url-template');

        if (template) {
            window.location.assign(template.replace('__activityId__', encodeURIComponent(notification.activityItemId)));
        }
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

        initializeMembershipPickers(root);

        if (signInForm) {
            signInForm.addEventListener('submit', function (event) {
                var queueIds = getSelectedValues(queueSelect);
                var campaignIds = getSelectedValues(campaignSelect);

                if (!queueIds.length && !campaignIds.length) {
                    event.preventDefault();
                    showMembershipError(
                        root,
                        api,
                        root.getAttribute('data-contact-center-selection-required') || 'Select at least one queue or campaign before signing in.');

                    return;
                }

                if (!client) {
                    return;
                }

                event.preventDefault();
                setBusy(root, true);
                showMembershipError(root, null, null);

                invokeHub(client, 'SignIn', queueIds, campaignIds)
                    .then(function (snapshot) {
                        updateMembershipUi(root, snapshot);

                        return recoverSoftPhoneState(root, api, client);
                    })
                    .catch(function (error) {
                        showMembershipError(root, api, error && error.message ? error.message : String(error));
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
                showMembershipError(root, null, null);

                invokeHub(client, 'SignOut')
                    .then(function (snapshot) {
                        updateMembershipUi(root, snapshot);

                        if (api && typeof api.clearIncomingOffer === 'function') {
                            api.clearIncomingOffer();
                        }
                    })
                    .catch(function (error) {
                        showMembershipError(root, api, error && error.message ? error.message : String(error));
                    })
                    .finally(function () {
                        setBusy(root, false);
                    });
            });
        }

        root.addEventListener('click', function (event) {
            var button = event.target.closest('[data-contact-center-membership-sign-out]');

            if (!button || !client) {
                return;
            }

            event.preventDefault();

            var snapshot = root.__contactCenterMembershipSnapshot || {
                queueIds: getSelectedValues(queueSelect),
                campaignIds: getSelectedValues(campaignSelect)
            };
            var kind = button.getAttribute('data-membership-kind');
            var membershipId = button.getAttribute('data-membership-id');
            var queueIds = (snapshot.queueIds || []).filter(function (queueId) {
                return kind !== 'queue' || queueId !== membershipId;
            });
            var campaignIds = (snapshot.campaignIds || []).filter(function (campaignId) {
                return kind !== 'campaign' || campaignId !== membershipId;
            });
            var method = queueIds.length || campaignIds.length ? 'UpdateMemberships' : 'SignOut';
            var args = method === 'UpdateMemberships' ? [queueIds, campaignIds] : [];

            setBusy(root, true);
            showMembershipError(root, null, null);

            invokeHub.apply(null, [client, method].concat(args))
                .then(function (updatedSnapshot) {
                    updateMembershipUi(root, updatedSnapshot);

                    if (method === 'SignOut' && api && typeof api.clearIncomingOffer === 'function') {
                        api.clearIncomingOffer();
                    }
                })
                .catch(function (error) {
                    showMembershipError(root, api, error && error.message ? error.message : String(error));
                })
                .finally(function () {
                    setBusy(root, false);
                });
        });
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
                },
                onReconnected: function () {
                    recoverSoftPhoneState(root, api, client);
                }
            })
            : null;

        api.__contactCenterQueuedVoiceSyncBound = true;
        bindMembershipForms(root, api, client);
        bindPresenceForms(root, api, client);

        if (client && client.connection) {
            client.connection.on('OfferReceived', function (notification) {
                openAssignedDialerActivity(root, notification);
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
