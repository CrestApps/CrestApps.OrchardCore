// @ts-check
/*
 * The line items editor of invoices, estimates, credit notes and recurring plans.
 *
 * Lines are added with "Add line" and removed with each row's remove button, in the browser; rows move up and down
 * with their arrows. Each row's amount and the totals are worked out as you type, for display only: the server's
 * calculator works everything out again when the form is saved, and its tax is the one kept. A row's tax is
 * estimated from the rate the line had when it was last saved; a new line, or one whose tax category changed, shows
 * its tax once saved.
 *
 * Where Products is on, a row's product button finds a product and fills the line's name, price and tax category.
 */
(function () {
  'use strict';

  // A number input's value is always written with a dot; a decimal comma is read too.
  function parseAmount(text) {
    if (text === null || text === undefined) {
      return null;
    }

    var value = String(text).trim().replace(',', '.');

    if (value === '' || !/^-?\d*\.?\d+$/.test(value)) {
      return null;
    }

    return Number(value);
  }

  function round(amount, decimals) {
    var factor = Math.pow(10, decimals);

    return Math.round((amount + Number.EPSILON * Math.sign(amount)) * factor) / factor;
  }

  // The line's quantity times its price, less its discount kept between nothing and the whole line.
  function lineAmount(line, decimals) {
    var quantity = line.quantity === null || line.quantity === undefined ? 1 : line.quantity;
    var price = line.unitPrice === null || line.unitPrice === undefined ? 0 : line.unitPrice;
    var amount = round(quantity * price, decimals);

    if (amount <= 0) {
      return amount;
    }

    var percent = Math.min(100, Math.max(0, line.discountPercent || 0));

    return round(amount - round(amount * percent / 100, decimals), decimals);
  }

  // The tax a line's amount carries at a rate (in percent): added on top of a net amount, or taken out of a gross one.
  function estimateTax(amount, rate, pricesIncludeTax, decimals) {
    if (rate === null || rate === undefined) {
      return null;
    }

    return pricesIncludeTax
      ? round(amount - amount / (1 + rate / 100), decimals)
      : round(amount * rate / 100, decimals);
  }

  function totals(rows, options) {
    var decimals = options.decimals;
    var subtotal = 0;
    var tax = 0;
    var taxUnknown = false;

    rows.forEach(function (row) {
      subtotal += row.amount;

      var estimate = estimateTax(row.amount, row.rate, options.pricesIncludeTax, decimals);

      if (estimate === null) {
        taxUnknown = taxUnknown || row.amount !== 0;
      } else {
        tax += estimate;
      }
    });

    subtotal = round(subtotal, decimals);
    tax = round(tax, decimals);

    return {
      subtotal: subtotal,
      tax: tax,
      total: options.pricesIncludeTax ? subtotal : round(subtotal + tax, decimals),
      taxUnknown: taxUnknown,
    };
  }

  // A posted field's name with the index of its row: Lines[7].UnitPrice for row 2 is Lines[2].UnitPrice.
  function renumber(name, prefix, index) {
    var pattern = new RegExp('^' + prefix + '\\[[^\\]]*\\]');

    return pattern.test(name) ? name.replace(pattern, prefix + '[' + index + ']') : name;
  }

  function move(items, from, to) {
    var copy = items.slice();

    if (from < 0 || from >= copy.length || to < 0 || to >= copy.length || from === to) {
      return copy;
    }

    var item = copy.splice(from, 1)[0];
    copy.splice(to, 0, item);

    return copy;
  }

  function hasAmount(kind) {
    return kind !== 'Text' && kind !== 'Section';
  }

  globalThis.CrestAppsDocumentLines = {
    parseAmount: parseAmount,
    lineAmount: lineAmount,
    estimateTax: estimateTax,
    totals: totals,
    renumber: renumber,
    move: move,
    hasAmount: hasAmount,
  };

  if (typeof document === 'undefined' || !document.querySelectorAll) {
    return;
  }

  function setUp(editor) {
    var body = editor.querySelector('[data-lines-body]');
    var template = editor.querySelector('template[data-line-template]');
    var decimals = Number(editor.getAttribute('data-decimals') || '2');
    var pricesIncludeTax = editor.getAttribute('data-prices-include-tax') === 'true';
    var currency = editor.getAttribute('data-currency') || '';
    var culture = editor.getAttribute('data-culture') || undefined;
    var searchUrl = editor.getAttribute('data-product-search-url');
    var productUrl = editor.getAttribute('data-product-line-url');
    var dirty = false;
    var format;

    try {
      format = currency
        ? new Intl.NumberFormat(culture, { style: 'currency', currency: currency })
        : new Intl.NumberFormat(culture, { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
    } catch {
      format = new Intl.NumberFormat(undefined, { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
    }

    function money(amount) {
      return format.format(amount);
    }

    function rows() {
      return Array.prototype.slice.call(body.querySelectorAll('[data-line-row]'));
    }

    function field(row, name) {
      return row.querySelector('[data-line-field="' + name + '"]');
    }

    function read(row, name) {
      var input = field(row, name);

      return input ? parseAmount(input.value) : null;
    }

    function rowRate(row) {
      var rate = row.getAttribute('data-line-rate');

      return rate === null || rate === '' ? null : Number(rate);
    }

    function renumberAll() {
      rows().forEach(function (row, index) {
        row.querySelectorAll('[name]').forEach(function (input) {
          input.setAttribute('name', renumber(input.getAttribute('name'), 'Lines', index));
        });

        // Each number input is followed by the __Invariant field that names it, so its dot is read the same in every culture.
        row.querySelectorAll('input[name="__Invariant"]').forEach(function (marker) {
          marker.value = marker.previousElementSibling ? marker.previousElementSibling.getAttribute('name') : '';
        });

        var position = row.querySelector('[data-line-position]');

        if (position) {
          position.textContent = String(index + 1);
        }
      });
    }

    function showKind(row) {
      var kind = field(row, 'kind');

      row.classList.toggle('document-line-no-amount', !!kind && !hasAmount(kind.value));
    }

    function update() {
      var lines = rows().map(function (row) {
        var kind = field(row, 'kind');
        var amount = kind && !hasAmount(kind.value)
          ? 0
          : lineAmount({ quantity: read(row, 'quantity'), unitPrice: read(row, 'unitPrice'), discountPercent: read(row, 'discountPercent') }, decimals);
        var rate = rowRate(row);
        var saved = row.getAttribute('data-line-saved-tax');

        // Until something changes, a line shows the tax the server gave it; after, an estimate at its rate.
        var tax = !dirty && saved !== null && saved !== ''
          ? Number(saved)
          : estimateTax(amount, rate, pricesIncludeTax, decimals);
        var amountCell = row.querySelector('[data-line-amount]');
        var taxCell = row.querySelector('[data-line-tax]');

        if (amountCell) {
          amountCell.textContent = kind && !hasAmount(kind.value) ? '' : money(amount);
        }

        if (taxCell) {
          taxCell.textContent = tax === null
            ? (amount === 0 ? '' : editor.getAttribute('data-tax-on-save') || '')
            : money(tax) + (rate ? ' (' + rate.toLocaleString(culture, { maximumFractionDigits: 3 }) + '%)' : '');
        }

        return { amount: amount, rate: rate };
      });

      var result = totals(lines, { pricesIncludeTax: pricesIncludeTax, decimals: decimals });
      var subtotal = editor.querySelector('[data-lines-subtotal]');
      var tax = editor.querySelector('[data-lines-tax]');
      var total = editor.querySelector('[data-lines-total]');

      // Until something changes, the footer keeps the totals the server worked out.
      if (dirty) {
        if (subtotal) {
          subtotal.textContent = money(result.subtotal);
        }

        editor.querySelectorAll('[data-lines-saved]').forEach(function (row) { row.hidden = true; });
        editor.querySelectorAll('[data-lines-estimate]').forEach(function (row) { row.hidden = false; });

        if (tax) {
          tax.textContent = money(result.tax) + (result.taxUnknown ? ' +' : '');
        }

        if (total) {
          total.textContent = money(result.total) + (result.taxUnknown && !pricesIncludeTax ? ' +' : '');
        }

        // The payment schedule follows the total as it changes.
        editor.dispatchEvent(new CustomEvent('documentlines:change', { bubbles: true, detail: { total: result.total } }));
      }
    }

    function changed() {
      dirty = true;
      rows().forEach(function (row) { row.removeAttribute('data-line-saved-tax'); });
      update();
    }

    function addRow() {
      var fragment = template.content.cloneNode(true);
      var row = fragment.querySelector('[data-line-row]');

      body.appendChild(fragment);
      renumberAll();
      showKind(row);
      changed();

      var name = field(row, 'name');

      if (name) {
        name.focus();
      }
    }

    function fill(row, product) {
      var name = field(row, 'name');
      var price = field(row, 'unitPrice');
      var category = field(row, 'taxCategory');
      var productId = field(row, 'productId');
      var sku = field(row, 'sku');

      if (name) {
        name.value = product.name || '';
      }

      if (price && product.price !== null && product.price !== undefined) {
        price.value = String(product.price);
      }

      if (productId) {
        productId.value = product.id || '';
      }

      if (sku) {
        sku.value = product.sku || '';
      }

      if (category && product.taxCategoryCode) {
        if (!Array.prototype.some.call(category.options, function (option) { return option.value === product.taxCategoryCode; })) {
          category.appendChild(new Option(product.taxCategoryCode, product.taxCategoryCode));
        }

        category.value = product.taxCategoryCode;
      }

      // The rate the line had no longer applies to what was picked; the tax shows once saved.
      row.setAttribute('data-line-rate', '');
      changed();
    }

    function search(row, query) {
      var results = row.querySelector('[data-line-product-results]');

      if (!results || !searchUrl) {
        return;
      }

      fetch(searchUrl + (searchUrl.indexOf('?') < 0 ? '?' : '&') + 'query=' + encodeURIComponent(query), { headers: { Accept: 'application/json' } })
        .then(function (response) { return response.ok ? response.json() : []; })
        .then(function (items) {
          results.replaceChildren();

          (items || []).slice(0, 10).forEach(function (item) {
            var button = document.createElement('button');

            button.type = 'button';
            button.className = 'list-group-item list-group-item-action py-1';
            button.textContent = item.text;
            button.setAttribute('data-line-product-id', item.value);
            results.appendChild(button);
          });

          results.hidden = results.children.length === 0;
        })
        .catch(function () { results.hidden = true; });
    }

    function pick(row, id) {
      if (!productUrl) {
        return;
      }

      fetch(productUrl.replace('__id__', encodeURIComponent(id)), { headers: { Accept: 'application/json' } })
        .then(function (response) { return response.ok ? response.json() : null; })
        .then(function (product) {
          if (product) {
            fill(row, product);
          }

          var panel = row.querySelector('[data-line-product-panel]');

          if (panel) {
            panel.hidden = true;
          }
        });
    }

    var timer = 0;

    editor.addEventListener('input', function (event) {
      var target = /** @type {HTMLElement} */ (event.target);
      var row = target.closest('[data-line-row]');

      if (target.matches('[data-line-product-query]') && row) {
        clearTimeout(timer);
        timer = setTimeout(function () { search(row, /** @type {HTMLInputElement} */ (target).value); }, 250);

        return;
      }

      if (row) {
        changed();
      }
    });

    editor.addEventListener('change', function (event) {
      var target = /** @type {HTMLElement} */ (event.target);
      var row = target.closest('[data-line-row]');

      if (!row) {
        return;
      }

      if (target.matches('[data-line-field="kind"]')) {
        showKind(row);
      }

      // Another tax category has another rate: the tax shows once saved.
      if (target.matches('[data-line-field="taxCategory"]')) {
        row.setAttribute('data-line-rate', '');
      }

      changed();
    });

    editor.addEventListener('click', function (event) {
      var target = /** @type {HTMLElement} */ (event.target);
      var button = target.closest('button');

      if (!button || !editor.contains(button)) {
        return;
      }

      var row = button.closest('[data-line-row]');

      if (button.matches('[data-line-add]')) {
        addRow();
      } else if (row && button.matches('[data-line-remove]')) {
        row.remove();
        renumberAll();
        changed();
      } else if (row && button.matches('[data-line-move]')) {
        var all = rows();
        var from = all.indexOf(row);
        var order = move(all, from, from + Number(button.getAttribute('data-line-move')));

        order.forEach(function (item) { body.appendChild(item); });
        renumberAll();
        changed();
        button.focus();
      } else if (row && button.matches('[data-line-details]')) {
        var details = /** @type {HTMLElement} */ (row.querySelector('[data-line-details-field]'));

        if (details) {
          details.hidden = !details.hidden;
          button.setAttribute('aria-expanded', String(!details.hidden));

          if (!details.hidden) {
            var area = details.querySelector('textarea');

            if (area) {
              area.focus();
            }
          }
        }
      } else if (row && button.matches('[data-line-product]')) {
        var panel = /** @type {HTMLElement} */ (row.querySelector('[data-line-product-panel]'));

        if (panel) {
          panel.hidden = !panel.hidden;

          var query = /** @type {HTMLInputElement} */ (panel.querySelector('[data-line-product-query]'));

          if (!panel.hidden && query) {
            query.focus();
            search(row, query.value);
          }
        }
      } else if (row && button.matches('[data-line-product-id]')) {
        pick(row, button.getAttribute('data-line-product-id'));
      }
    });

    rows().forEach(showKind);
    update();
  }

  function start() {
    document.querySelectorAll('[data-document-lines]').forEach(setUp);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }
})();
