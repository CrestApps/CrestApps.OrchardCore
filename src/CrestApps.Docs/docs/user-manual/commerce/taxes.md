---
sidebar_label: Taxes
title: Taxes
description: Set up the categories, tax types, jurisdictions, tables and rules your site uses to charge tax, classify your products so checkout adds the right tax, and use the EU VAT rates, import duty, TaxJar and the VAT one-stop-shop report.
technical_manual:
  - modules/taxation
---

The **Taxation** screens decide which taxes your site charges and how much. You describe *what* you sell (categories), *which kinds* of tax exist (types), *where* you tax (jurisdictions) and *how* each tax is worked out (rules). Then you classify your products, and the checkout adds the tax to the order summary for you.

| | |
| --- | --- |
| **Menu** | Commerce > Taxation > Categories, Types, Jurisdictions, Tables and Rules |
| **Permission** | Manage taxation |
| **Feature** | Taxation |

<AskYourAdmin />

Set things up in the order below: rules point to jurisdictions, types, categories and tables, so those must exist first. Every screen is a searchable list with an **Add** button, and **Edit** and **Delete** on each entry. The **Name** of anything you create is fixed once it is saved, and must be unique.

This page covers the screens. For exactly how amounts are calculated, rounded and stored, see the [Taxation technical page](../../modules/taxation.md).

## 1. Create tax categories

This video walks through tax categories, jurisdictions, rules, the tax origin, classifying products and the tax at checkout:

<YouTube video="taxation" title="Video overview of taxation" />

A **category** groups what you sell for tax purposes, for example *Electronics*, *Food* or *Tobacco*. Rules can target a category, and products are classified with one.

1. Open **Commerce > Taxation > Categories** and click **Add Tax Category**.
2. Fill in the fields and click **Save**.

| Field | What it does |
| --- | --- |
| **Name** | The label you see in lists and drop-downs. Required. |
| **Code** | The unique code rules and products use, for example *ELEC*. Required. Drop-downs show categories as *Name (Code)*. |
| **Parent code** | The **Code** of a broader category, to organize categories in a hierarchy, for example *Television* under *Electronics*. A rule for *Electronics* then also taxes televisions; a rule for *Television* doesn't tax other electronics. Leave empty for a top-level category. |
| **Description** | Optional notes. |

## 2. Review tax types

A **tax type** is a label for the kind of tax a rule produces, such as *SalesTax* or *VAT*. It groups taxes in reports and never changes the amount.

Your site starts with a list of common types: *SalesTax*, *VAT*, *GST*, *HST*, *PST*, *QST*, *ExciseTax*, *AlcoholTax*, *TobaccoTax*, *TourismTax*, *LodgingTax*, *EnvironmentalTax*, *DigitalServicesTax*, *ImportDuty* and *Other*. To add one, open **Commerce > Taxation > Types**, click **Add Tax Type**, enter a **Name** and an optional **Description**, and click **Save**. Delete the ones you never use to keep the rule editor short.

## 3. Create jurisdictions

<YouTube video="taxation" title="Video of adding a jurisdiction" start={133} end={166} />

A **jurisdiction** is a place that taxes, such as a country, a state or a city.

1. Open **Commerce > Taxation > Jurisdictions** and click **Add Tax Jurisdiction**.
2. Fill in the fields and click **Save**.

| Field | What it does |
| --- | --- |
| **Name** | For example *California*. Required. |
| **Code** | A short code, for example *US-CA*. Required. |
| **Level** | **Country**, **State**, **Province**, **Region**, **County**, **City**, **Special district** or **Other**. |
| **Parent jurisdiction** | The larger place this one belongs to, for example *United States* for *California*. Leave at **— None —** for a top-level jurisdiction. |
| **Country** | The country the jurisdiction is in. |
| **Region** / **County** / **City** / **Postal code** | The parts of an address that fall in this jurisdiction. Fill in only what defines it; leave the rest empty. |
| **Effective from** / **Effective to** | Optional dates between which the jurisdiction is in use. The end cannot be before the start. |

## 4. Create tax tables (optional)

<YouTube video="taxation" title="Video of tax tables, TaxJar and the EU VAT recipe" start={308} end={348} />

A **tax table** is a list of amount ranges, each with its own rate. You need one only for the **Tax table lookup**, **Progressive** and **Threshold** rules. Skip this step if all your taxes are a simple percentage or a fixed amount.

1. Open **Commerce > Taxation > Tables** and click **Add Tax Table**.
2. Enter a **Name** and, optionally, **Effective from** and **Effective to** dates. A rule only uses the table on dates inside that window.
3. Click **Add row** for each range and fill it in. Use the trash button to remove a row.
4. Click **Save**. Rows are sorted by their minimum when saved.

| Column | What it does |
| --- | --- |
| **Minimum** | Where the range starts. Cannot be negative. |
| **Maximum** | Where the range ends; it must be greater than the minimum. Leave empty on the top range only. |
| **Rate** | The rate for this range, written as a fraction: *0.2* means 20%. |
| **Fixed amount** | An amount added on top of the rate for this range. |

Ranges cannot overlap, and only one row can be open-ended. A table that a rule uses cannot be deleted: the message says how many rules use it, so update or remove those rules first.

## 5. Create tax rules

<YouTube video="taxation" title="Video of adding a percentage tax rule" start={166} end={231} />

A **rule** is one tax: what it applies to, and how it is worked out. Several rules can apply to the same item; for example a general sales tax and an extra tobacco tax.

1. Open **Commerce > Taxation > Rules** and click **Add Tax Rule**.
2. In **Select a calculation method**, click **Add** on the method you want. The method cannot be changed later.
3. Fill in the fields and click **Save**.

| Calculation method | What it does | You enter |
| --- | --- | --- |
| **Percentage** | A percentage of the price. Use it for sales tax, VAT and GST. | **Rate** |
| **Fixed amount** | A set amount per item line, whatever the quantity. | **Fixed amount** |
| **Per unit** | A set amount for every unit bought, such as an excise duty. | **Fixed amount** |
| **Per weight** | A set amount for every unit of weight. | **Fixed amount** |
| **Per volume** | A set amount for every unit of volume. | **Fixed amount** |
| **Progressive** | Taxes each range of a tax table on the part of the price inside it, then adds them up. | **Tax table** |
| **Threshold** | Charges tax only once an amount is reached, using a tax table. | **Tax table** |
| **Tax table lookup** | Takes the rate and fixed amount from the one table row the price falls in. | **Tax table** |

| Field | What it does |
| --- | --- |
| **Name** | Identifies the rule in the admin. Required and unique. |
| **Enabled** | A disabled rule is never applied. The list marks it **Disabled**. |
| **Priority** | The order rules are worked out in. Lower numbers go first. |
| **Tax type** | The kind of tax, from your [tax types](#2-review-tax-types). Required. |
| **Display name** | What buyers see on the order summary, invoices and receipts, for example *CA sales tax*. Empty uses the rule **Name**. |
| **Tax code** | Optional code for your own records. |
| **Jurisdiction** | The jurisdiction the rule belongs to, or **Any jurisdiction**. |
| **Category** | The category the rule applies to, or **Any category** for everything. A rule also applies to items whose **Tax classification** is this category. |
| **Customer type** | **Any customer**, **Consumer (B2C)** or **Business (B2B)**. |
| **Minimum amount** / **Maximum amount** | Optional. The rule applies only when the amount is at least the minimum and below the maximum. |
| **Amounts compared with** | What the minimum and maximum are compared with: **Each item** (the default), each item's taxable amount; or **The whole order**, what the order's items come to before tax, shipping left out. The whole order suits an [import duty](#import-duty) charged only on orders above a value. |
| **Tax is included in the item price** | Tick when your prices already include this tax, so it is worked out of the price instead of added on top. |
| **Tax is compound (calculated on top of other taxes)** | Tick when this tax is charged on the price plus the taxes worked out before it. |
| **Reverse charge (the customer accounts for the tax)** | For a business buyer, the tax is not charged; a zero line marked as reverse charge is shown instead. Other buyers are charged as usual. |
| **Applies to shipping charges** | Tick to tax shipping charges too. |
| **Effective from** / **Effective to** | Optional dates between which the rule is used. The end cannot be before the start. |
| **Rate** | **Percentage** only. Written as a fraction: *0.0725* means 7.25%. |
| **Fixed amount** | Fixed amount, per-unit, per-weight and per-volume rules only. |
| **Tax table** | Table-based rules only. Required for them. |

The list shows each rule's tax type, method, rate or amount, and priority.

:::caution[Jurisdictions at checkout]
A rule tied to a **Jurisdiction** only charges buyers whose address is in that jurisdiction. The checkout taxes an order where it ships to, or, when nothing ships, where the buyer is billed. Until the buyer has given an address, only rules set to **Any jurisdiction** apply. See the [Taxation technical page](../../modules/taxation.md#tax-sourcing).
:::

## Where you sell from

<YouTube video="taxation" title="Video of the tax origin settings" start={231} end={244} />

Some taxes are charged where the seller is rather than where the buyer is. Under **Settings > Commerce > Tax origin**, enter your store's **Country**, **State or province** and, if your taxes depend on them, county, city and postal code. Leave the country blank when every tax you collect is based on where the buyer is.

Customers you mark **Exempt from tax** under [Customers](customers.md) pay no tax when they check out signed in.

## 6. Classify your products

<YouTube video="taxation" title="Video of adding the Taxation part and setting a product's tax category" start={244} end={285} />

Rules only apply to items that carry the **Taxation** part. Add it to each content type you sell, usually the same type that has the **Product** part (see [Products and Currencies](products-and-currencies.md)).

| | |
| --- | --- |
| **Menu** | Design > Content Definition > Content Types |
| **Permission** | Edit content types |
| **Feature** | Taxation |

<AskYourAdmin />

1. Open **Design > Content Definition > Content Types** and edit the content type.
2. Click **Add Parts**, tick **Taxation**, and save.
3. Click **Edit** next to the **Taxation** part, set the options below, and save.

| Field | What it does |
| --- | --- |
| **Default tax category** | The category new items of this type start with. |
| **Default tax classification** | An optional, more specific category new items start with, for example *Television* under *Electronics*. |
| **Allow editors to override the classification** | Ticked (the default): editors can pick a different category on each item. Cleared: every item always uses the defaults above. |

Create your categories first: these drop-downs only list existing categories, plus **None**.

On each item, the **Taxation** part shows:

| Field | What it does |
| --- | --- |
| **Taxable** | Ticked by default. Clear it for an item that is never taxed. |
| **Tax category** | The item's category. Shown only when editors may override the classification. |
| **Tax classification** | An optional, more specific category. Shown only when editors may override the classification. |
| **External tax code** | Optional. A code used by an outside tax service. |

### Classify many products at once with a taxonomy

Instead of setting a category on every item, you can set it once on a taxonomy term and tag products with the term. This needs the **Taxonomies** feature.

1. Create a taxonomy, for example *Product categories*, with terms such as *Electronics* and *Tobacco*.
2. Add the **Taxation** part to the taxonomy's term content type, and set the **Tax category** on each term.
3. Add a taxonomy field that uses this taxonomy to your product content type, and tag each product with its term.
4. On the products, leave **Tax category** at **None**. New items start with the content type's **Default tax category**, so set that default to **None** too.

A product with no category of its own takes the category of its tagged term. A category set on the product itself always wins.

## Example: sales tax plus a tobacco tax

1. **Categories:** add *Electronics* (code *ELEC*) and *Tobacco* (code *TOBACCO*).
2. **Jurisdictions:** add *California*, level **State**, country *United States*, region *CA*.
3. **Rules:** add a **Percentage** rule *CA sales tax* with **Tax type** *SalesTax*, **Jurisdiction** *California*, **Category** **Any category**, **Rate** *0.075*. Add a second **Percentage** rule *CA tobacco tax* with **Tax type** *TobaccoTax*, **Category** *Tobacco (TOBACCO)*, **Rate** *0.3*.
4. **Products:** classify televisions as *Electronics* and cigarettes as *Tobacco*.

For a buyer in California, a television is charged 7.5%. A pack of cigarettes matches both rules and is charged 7.5% plus 30%. Each tax shows as its own line in the buyer's order summary. A buyer outside California pays only the tobacco tax, which has no jurisdiction. The checkout taxes an order by the address it ships to, or by the billing address when nothing ships, so the sales tax applies to a buyer in California and not to a buyer elsewhere.

## Prices that include tax

In many countries the price a shopper sees already includes VAT. To sell that way to some countries, add them to a [market](markets.md) with **Prices include tax** on. At checkout, the tax your rules give is then taken out of the prices instead of added on top, so the shopper pays the price they saw.

## Taxes on the order

<YouTube video="taxation" title="Video of the tax in the checkout's order summary" start={285} end={308} />

The checkout's order summary shows each tax once for each rate, whatever the items it was charged on, such as *VAT 19%* and *VAT 7%*. A tax that was taken out of the prices says so: *VAT 19% (included)*.

An order keeps the taxes it was charged: their names, types, places, rates, what they were charged on and the amounts. Reports, such as the [VAT one-stop shop (OSS)](#vat-one-stop-shop-oss-report) report, add them up, and apps see them through the [headless API](headless-api.md).

## EU VAT standard rates

The **EU VAT standard rates** recipe sets up VAT for the European Union in one step. It adds the 27 EU member states as jurisdictions (level **Country**), and a **Percentage** rule for each, with **Tax type** *VAT*, **Display name** *VAT*, the country's standard rate, and **Applies to shipping charges** ticked.

1. Open **Configuration > Recipes**.
2. Find **EU VAT standard rates** and click **Run**.
3. Open **Commerce > Taxation > Rules** to see the rules, named after their countries, such as *Germany VAT*.

:::caution[Check the rates]
VAT rates change. Check each country's rate before you sell there, and add reduced rates for the products they apply to, such as books or food, as rules for those [categories](#1-create-tax-categories).
:::

Running the recipe again puts its jurisdictions and rules back as the recipe has them, so a rate you changed is set back.

## Import duty

Some countries charge **import duty** on goods brought in from abroad, often only on orders above a value. Charge it at checkout, so the buyer pays nothing on delivery:

1. Add a jurisdiction for the destination country, such as *Canada* (level **Country**, country *Canada*), if you don't have one.
2. Add a **Percentage** rule with **Tax type** *ImportDuty*, **Display name** *Import duty*, the country's **Jurisdiction** and its **Rate**.
3. Set **Minimum amount** to the value duty starts at, such as *150*, and **Amounts compared with** to **The whole order**.

An order to that country whose items come to 150 or more, before tax and without shipping, is charged the duty, on its own line in the order summary. A smaller order isn't. Leave **Applies to shipping charges** clear unless the country charges duty on shipping too.

Sites set up before import duty was added get the *ImportDuty* tax type when they're updated.

## VAT one-stop shop (OSS) report

Sellers in the EU who sell to buyers in other EU countries declare the VAT on those sales in one return, the **one-stop shop** (OSS). The **VAT one-stop shop (OSS)** report gives its figures.

| | |
| --- | --- |
| **Menu** | Reports > Commerce > VAT one-stop shop (OSS) |
| **Permission** | Manage orders |
| **Feature** | Orders and Reports |

<AskYourAdmin />

1. Set your store's country under **Settings > Commerce > Tax origin** (see [Where you sell from](#where-you-sell-from)).
2. Open the report and choose the quarter's first and last day as the period. Click **Show**.

For the orders paid in the period, the report adds up the VAT charged on orders to EU countries other than your own, by the country they went to (the shipping address, or the billing address when nothing shipped) and the rate. For each currency you sold in it shows the **VAT due**, the number of countries, and a table with each country and rate: the orders, the **Taxable amount** and the **VAT**, with a total. A refund takes its share of the order's VAT off.

Only taxes whose **Tax type** is *VAT* are counted. Orders to your own country, and to countries outside the EU, are left out. *No order paid in the period was charged VAT in another EU country.* means there's nothing to declare.

## TaxJar

For sales tax in the United States, where rates change by state, county and city, **TaxJar** can work out the tax instead of your own rules. You need a TaxJar account.

| | |
| --- | --- |
| **Menu** | Settings > Commerce > TaxJar |
| **Permission** | Manage taxation |
| **Feature** | TaxJar |

<AskYourAdmin />

1. In TaxJar, get an API token under **Account > TaxJar API**.
2. Open **Settings > Commerce > TaxJar**, fill in the fields (below), and save.

| Field | What it does |
| --- | --- |
| **TaxJar API token** | Your TaxJar token. Once saved it's never shown again; leave it blank to keep it, or enter a new one to replace it. Without a token, your own tax rules work out tax. |
| **Remove the token** | Shown once a token is saved. Stops using TaxJar: your own tax rules work out tax again. |
| **Countries** | The two-letter codes of the countries TaxJar works tax out for, separated by commas. It starts with *US*. |
| **Sandbox** | For a sandbox token from TaxJar, while you try it out. |

Saving restarts your site, so the new settings are used at once. This takes a moment.

For an order to one of those countries, with prices that don't include tax, TaxJar works out the tax on each item and on shipping from your store's address (**Settings > Commerce > Tax origin**) and the address the order goes to. The order summary shows it as *Sales tax* (*VAT* for a country other than the United States), with the combined rate of the places that tax the address. Your own rules aren't used for that order. Orders to other countries, and orders in a market whose prices include tax, use your own rules.

If TaxJar can't be reached, or can't work out the tax, your own tax rules work it out instead, so the sale goes on, and the problem is written to the site's log. Customers marked **Exempt from tax** are sent to TaxJar as exempt.
