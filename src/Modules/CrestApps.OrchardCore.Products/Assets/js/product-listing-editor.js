// @ts-check
/*
 * The product listing editor.
 *
 * Shows the filters, sorts and default sort options of the product types the listing shows: every type, or only the
 * ticked ones. What every product has (the price, the type) always shows.
 *
 * Filters and sorts are reordered by dragging their handle, or with the arrow keys on it. Each row still posts its
 * Order number: a move hands the group's own numbers out again in the new order, so rows of other groups keep their
 * place between them.
 */
(function () {
  'use strict';

  // The numbers a group's rows had, handed out again in the order the rows now stand in.
  function reassignOrders(orders) {
    return orders
      .map(function (order) { return Number(order) || 0; })
      .sort(function (a, b) { return a - b; });
  }

  // Moves the item at "from" to "to" and returns the new array.
  function move(items, from, to) {
    var copy = items.slice();

    if (from < 0 || from >= copy.length || to < 0 || to >= copy.length || from === to) {
      return copy;
    }

    var item = copy.splice(from, 1)[0];
    copy.splice(to, 0, item);

    return copy;
  }

  function countUsed(rows) {
    return rows.filter(function (row) {
      return row.used && row.visible;
    }).length;
  }

  globalThis.CrestAppsProductListingEditor = {
    reassignOrders: reassignOrders,
    move: move,
    countUsed: countUsed,
  };

  if (typeof document === 'undefined' || !document.querySelectorAll) {
    return;
  }

  function rowsOf(list) {
    return Array.prototype.filter.call(list.children, function (child) {
      return child.hasAttribute('data-product-row');
    });
  }

  function renumber(list) {
    var inputs = rowsOf(list).map(function (row) {
      return row.querySelector('[data-product-order]');
    }).filter(Boolean);

    var orders = reassignOrders(inputs.map(function (input) { return input.value; }));

    inputs.forEach(function (input, index) {
      input.value = orders[index];
    });
  }

  function announce(editor, text) {
    var status = editor.querySelector('[data-product-editor-status]');

    if (status) {
      status.textContent = text;
    }
  }

  function setUp(editor) {
    var every = editor.querySelector('[data-product-every-type]');
    var boxes = editor.querySelectorAll('[data-product-type]');
    var typeList = editor.querySelector('[data-product-type-list]');
    var defaultSort = editor.querySelector('[data-product-default-sort]');

    function shows(types, picked) {
      return !types || picked === null || types.split(',').some(function (type) {
        return picked.indexOf(type) >= 0;
      });
    }

    // "4 of 9 filters in use" beside the section's toggle.
    function summarize() {
      editor.querySelectorAll('[data-product-summary]').forEach(function (summary) {
        var section = editor.querySelector('[data-product-rows="' + summary.getAttribute('data-product-summary') + '"]');

        if (!section) {
          return;
        }

        var rows = Array.prototype.map.call(section.querySelectorAll('[data-product-row]'), function (row) {
          var use = row.querySelector('[data-product-use]');
          var group = row.closest('[data-product-types]');

          return { used: !!(use && use.checked), visible: !(group && group.hidden) };
        });

        var used = countUsed(rows);
        var total = rows.filter(function (row) { return row.visible; }).length;
        var template = summary.getAttribute('data-template') || '{0} of {1}';

        summary.textContent = template.replace('{0}', used).replace('{1}', total);
      });
    }

    function update() {
      var listEvery = !every || every.checked;
      var picked = listEvery ? null : Array.prototype.filter.call(boxes, function (box) { return box.checked; })
        .map(function (box) { return box.value; });

      if (typeList) {
        typeList.querySelectorAll('input').forEach(function (box) {
          box.disabled = listEvery;
        });
      }

      editor.querySelectorAll('.product-listing-group[data-product-types]').forEach(function (group) {
        group.hidden = !shows(group.getAttribute('data-product-types'), picked);
      });

      if (defaultSort) {
        defaultSort.querySelectorAll('optgroup[data-product-types]').forEach(function (group) {
          var visible = shows(group.getAttribute('data-product-types'), picked);

          group.hidden = !visible;
          group.disabled = !visible;
        });

        var selected = defaultSort.selectedOptions[0];

        if (selected && selected.parentElement && selected.parentElement.disabled) {
          defaultSort.value = '';
        }
      }

      summarize();
    }

    // A filter's styles are the ones that can draw its kind.
    editor.querySelectorAll('[data-product-facet-kind]').forEach(function (kind) {
      var row = kind.closest('[data-product-row]');
      var style = row ? row.querySelector('[data-product-facet-style]') : null;

      if (!style) {
        return;
      }

      function sync() {
        style.querySelectorAll('option').forEach(function (option) {
          var kinds = option.getAttribute('data-kinds');
          var supported = !kinds || kinds.split(',').indexOf(kind.value) >= 0;

          option.hidden = !supported;
          option.disabled = !supported;
        });

        if (style.selectedOptions[0] && style.selectedOptions[0].disabled) {
          style.value = '';
        }
      }

      kind.addEventListener('change', sync);
      sync();
    });

    // A section's settings show while the page offers it.
    editor.querySelectorAll('[data-product-section-toggle]').forEach(function (toggle) {
      var section = editor.querySelector('[data-product-section="' + toggle.getAttribute('data-product-section-toggle') + '"]');

      function sync() {
        if (section) {
          section.hidden = !toggle.checked;
        }
      }

      toggle.addEventListener('change', sync);
      sync();
    });

    // Dragging a row by its handle.
    editor.querySelectorAll('[data-product-sortable]').forEach(function (list) {
      if (typeof globalThis.Sortable === 'undefined') {
        return;
      }

      globalThis.Sortable.create(list, {
        handle: '[data-product-drag]',
        draggable: '[data-product-row]',
        animation: 150,
        ghostClass: 'product-listing-row-ghost',
        onEnd: function () {
          renumber(list);
        },
      });
    });

    // The arrow keys on a handle move its row, for keyboard users.
    editor.addEventListener('keydown', function (event) {
      var handle = event.target.closest && event.target.closest('[data-product-drag]');

      if (!handle || (event.key !== 'ArrowUp' && event.key !== 'ArrowDown')) {
        return;
      }

      var row = handle.closest('[data-product-row]');
      var list = row && row.parentElement;

      if (!list) {
        return;
      }

      event.preventDefault();

      var rows = rowsOf(list);
      var index = rows.indexOf(row);
      var target = event.key === 'ArrowUp' ? index - 1 : index + 1;

      if (target < 0 || target >= rows.length) {
        return;
      }

      if (event.key === 'ArrowUp') {
        list.insertBefore(row, rows[target]);
      } else {
        list.insertBefore(row, rows[target].nextSibling);
      }

      renumber(list);
      handle.focus();

      var template = editor.getAttribute('data-moved-template') || '{0}: {1} of {2}';

      announce(editor, template
        .replace('{0}', handle.getAttribute('data-product-drag') || '')
        .replace('{1}', target + 1)
        .replace('{2}', rows.length));
    });

    editor.addEventListener('change', function (event) {
      if (event.target.matches && event.target.matches('[data-product-use]')) {
        summarize();
      }
    });

    editor.querySelectorAll('[name$="ListEveryType"]').forEach(function (radio) {
      radio.addEventListener('change', update);
    });

    boxes.forEach(function (box) {
      box.addEventListener('change', update);
    });

    update();
  }

  function start() {
    document.querySelectorAll('[data-product-listing-editor]').forEach(setUp);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }
})();
