// @ts-check
/*
 * The variant picker of the Add to cart form.
 *
 * Without JavaScript the form offers one list of every variant on sale, such as "M / Red — $25". The script shows one
 * list per option instead (Size, then Color), keeps the variant list in step so it is still what the form posts,
 * offers only the values that make a variant on sale with the ones already picked, and on a product's page shows the
 * chosen variant's price, SKU and image.
 */
(function () {
  'use strict';

  // The variant whose options are exactly the chosen ones, or null while an option is not chosen.
  function findVariant(variants, chosen) {
    return variants.find(function (variant) {
      return Object.keys(variant.options).every(function (name) {
        return chosen[name] === variant.options[name];
      });
    }) || null;
  }

  // The values of an option that, with the other options already chosen, make a variant on sale.
  function availableValues(variants, chosen, optionName) {
    var values = [];

    variants.forEach(function (variant) {
      var fits = Object.keys(chosen).every(function (name) {
        return name === optionName || !chosen[name] || variant.options[name] === chosen[name];
      });

      var value = variant.options[optionName];

      if (fits && value !== undefined && values.indexOf(value) < 0) {
        values.push(value);
      }
    });

    return values;
  }

  globalThis.CrestAppsVariantPicker = {
    findVariant: findVariant,
    availableValues: availableValues,
  };

  if (typeof document === 'undefined' || !document.querySelectorAll) {
    return;
  }

  function readVariants(select) {
    return Array.prototype.map.call(select.options, function (option) {
      var options;

      try {
        options = JSON.parse(option.getAttribute('data-options') || '{}');
      } catch (e) {
        options = {};
      }

      return {
        id: option.value,
        options: options,
        price: option.getAttribute('data-price'),
        sku: option.getAttribute('data-sku'),
        image: option.getAttribute('data-image'),
        availability: option.getAttribute('data-availability'),
        tone: option.getAttribute('data-availability-tone'),
        buyable: option.getAttribute('data-buyable') !== 'false',
      };
    });
  }

  // On a product's page: its price, SKU and gallery become the chosen variant's.
  function showOnPage(form, variant) {
    var summary = form.closest('.product-summary');

    if (!summary || !variant) {
      return;
    }

    var amount = summary.querySelector('.product-summary-price .product-price-amount');

    if (amount && variant.price) {
      amount.textContent = variant.price;

      var from = summary.querySelector('.product-summary-price .product-price-from');

      if (from) {
        from.hidden = true;
      }
    }

    var sku = summary.querySelector('.product-summary-sku');
    var template = form.getAttribute('data-sku-template');

    if (sku && variant.sku && template) {
      sku.textContent = template.replace('{0}', variant.sku);
    }

    if (!variant.image) {
      return;
    }

    // A thumbnail moves the carousel; Bootstrap handles its click.
    var thumbnail = summary.querySelector('.product-gallery-thumbnails [data-media-path="' + CSS.escape(variant.image) + '"]');

    if (thumbnail) {
      thumbnail.click();
    }
  }

  // The stock line under the options says what's left of the chosen variant, for products whose stock is counted.
  function showAvailability(form, variant) {
    var line = form.querySelector('[data-product-availability]');

    if (!line) {
      return;
    }

    if (!variant || !variant.availability) {
      line.hidden = true;

      return;
    }

    line.textContent = variant.availability;
    line.className = 'product-availability small mb-2 text-' + (variant.tone || 'success');
    line.hidden = false;
  }

  function setUp(form) {
    var select = form.querySelector('[data-product-variant]');
    var picker = form.querySelector('[data-product-variant-options]');
    var submit = form.querySelector('[type="submit"]');

    if (!select || !picker) {
      return;
    }

    var variants = readVariants(select);
    var lists = Array.prototype.slice.call(picker.querySelectorAll('[data-product-option]'));

    function chosen() {
      var values = {};

      lists.forEach(function (list) {
        values[list.getAttribute('data-product-option')] = list.value;
      });

      return values;
    }

    function sync() {
      var picked = chosen();

      lists.forEach(function (list) {
        var available = availableValues(variants, picked, list.getAttribute('data-product-option'));

        Array.prototype.forEach.call(list.options, function (option) {
          if (option.value) {
            option.disabled = available.indexOf(option.value) < 0;
          }
        });
      });

      var variant = findVariant(variants, picked);

      if (variant) {
        select.value = variant.id;
      }

      // The form cannot be sent until the options make a variant on sale that's in stock.
      if (submit) {
        submit.disabled = !variant || !variant.buyable;
      }

      showAvailability(form, variant);
      showOnPage(form, variant);
    }

    // The options start at the variant the list starts at, the first on sale.
    var first = variants.find(function (variant) {
      return variant.id === select.value;
    }) || variants[0];

    lists.forEach(function (list) {
      if (first && first.options[list.getAttribute('data-product-option')] !== undefined) {
        list.value = first.options[list.getAttribute('data-product-option')];
      }

      list.addEventListener('change', sync);
    });

    form.querySelector('[data-product-variant-fallback]').hidden = true;
    picker.classList.remove('d-none');

    sync();
  }

  function start() {
    document.querySelectorAll('form[data-product-variant-form]').forEach(setUp);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }
})();
