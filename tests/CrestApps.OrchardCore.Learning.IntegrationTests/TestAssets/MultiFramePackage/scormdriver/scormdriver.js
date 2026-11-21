// A small stand-in for a SCORM driver such as Articulate's: it runs in the driver page, finds the SCORM 1.2 API by
// walking up window.parent (then the opener), and offers the content frame a few calls. The content frame reaches it
// through window.parent, so both frames must share an origin.
(function () {
    'use strict';

    var log = [];

    function findApi(win) {
        var tries = 0;

        while (win && tries < 10) {
            try {
                if (win.API) {
                    return win.API;
                }
            } catch (error) {
                log.push('Error grabbing 1.2 API-' + error.name);
            }

            if (!win.parent || win.parent === win) {
                break;
            }

            win = win.parent;
            tries++;
        }

        try {
            return window.opener && window.opener.API ? window.opener.API : null;
        } catch (error) {
            log.push('Error grabbing 1.2 API from the opener-' + error.name);

            return null;
        }
    }

    var api = null;

    window.Driver = {
        log: log,
        initialize: function () {
            api = findApi(window.parent === window ? window : window.parent);

            if (!api) {
                log.push('LMS Initialize Failed');

                return false;
            }

            return api.LMSInitialize('') === 'true';
        },
        studentName: function () {
            return api ? api.LMSGetValue('cmi.core.student_name') : '';
        },
        score: function (raw) {
            return api.LMSSetValue('cmi.core.score.min', '0') === 'true' &&
                api.LMSSetValue('cmi.core.score.max', '100') === 'true' &&
                api.LMSSetValue('cmi.core.score.raw', String(raw)) === 'true';
        },
        complete: function () {
            return api.LMSSetValue('cmi.core.lesson_status', 'passed') === 'true' && api.LMSCommit('') === 'true';
        },
        finish: function () {
            return api.LMSFinish('') === 'true';
        },
    };
})();
