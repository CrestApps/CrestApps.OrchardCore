/*
 * Shared behavior for the commerce admin lists (coupons, transactions, payments, refunds, installment plans).
 *
 * A filter picked in the list header applies at once, the way the content items list behaves: it posts the filter
 * form it belongs to (named by its form attribute, so the list itself can hold other forms). The page size is chosen
 * from Orchard Core's own pager under the list.
 */
(function () {
    'use strict';

    function submitFilters(select) {
        var form = select.form;

        if (!form) {
            return;
        }

        var submit = form.querySelector('[name="submit.Filter"]');

        if (submit) {
            submit.click();
        } else {
            form.submit();
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        var filters = document.querySelectorAll('[data-commerce-list-filter]');

        for (var i = 0; i < filters.length; i++) {
            filters[i].addEventListener('change', function (event) {
                submitFilters(event.currentTarget);
            });
        }
    });
})();
