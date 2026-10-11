---
sidebar_label: Taxes
title: Taxes
description: Set up the categories, tax types, jurisdictions, tables and rules your site uses to charge tax, then classify your products so checkout adds the right tax.
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

A **category** groups what you sell for tax purposes, for example *Electronics*, *Food* or *Tobacco*. Rules can target a category, and products are classified with one.

1. Open **Commerce > Taxation > Categories** and click **Add Tax Category**.
2. Fill in the fields and click **Save**.

| Field | What it does |
| --- | --- |
| **Name** | The label you see in lists and drop-downs. Required. |
| **Code** | The unique code rules and products use, for example *ELEC*. Required. Drop-downs show categories as *Name (Code)*. |
| **Parent code** | The **Code** of a broader category, to organize categories in a hierarchy, for example *Television* under *Electronics*. Leave empty for a top-level category. |
| **Description** | Optional notes. |

## 2. Review tax types

A **tax type** is a label for the kind of tax a rule produces, such as *SalesTax* or *VAT*. It groups taxes in reports and never changes the amount.

Your site starts with a list of common types: *SalesTax*, *VAT*, *GST*, *HST*, *PST*, *QST*, *ExciseTax*, *AlcoholTax*, *TobaccoTax*, *TourismTax*, *LodgingTax*, *EnvironmentalTax*, *DigitalServicesTax* and *Other*. To add one, open **Commerce > Taxation > Types**, click **Add Tax Type**, enter a **Name** and an optional **Description**, and click **Save**. Delete the ones you never use to keep the rule editor short.

## 3. Create jurisdictions

A **jurisdiction** is a place that taxes, such as a country, a state or a city.

1. Open **Commerce > Taxation > Jurisdictions** and click **Add Tax Jurisdiction**.
2. Fill in the fields and click **Save**.

| Field | What it does |
| --- | --- |
| **Name** | For example *California*. Required. |
| **Code** | A short code, for example *US-CA*. Required. |
| **Level** | **Country**, **State**, **Province**, **Region**, **County**, **City**, **Special** or **Other**. |
| **Parent jurisdiction** | The larger place this one belongs to, for example *United States* for *California*. Leave at **— None —** for a top-level jurisdiction. |
| **Country** | The country the jurisdiction is in. |
| **Region** / **County** / **City** / **Postal code** | The parts of an address that fall in this jurisdiction. Fill in only what defines it; leave the rest empty. |
| **Effective from** / **Effective to** | Optional dates between which the jurisdiction is in use. The end cannot be before the start. |

## 4. Create tax tables (optional)

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
| **Base amount** | Stored with the row, but not used by the built-in calculation methods. Leave it at 0. |

Ranges cannot overlap, and only one row can be open-ended. A table that a rule uses cannot be deleted: the message says how many rules use it, so update or remove those rules first.

## 5. Create tax rules

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
| **Minimum amount** / **Maximum amount** | Optional. The rule applies only when the taxable amount is at least the minimum and below the maximum. |
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
The checkout does not ask buyers for an address, so it cannot tell which jurisdiction a buyer is in. At checkout, every enabled rule that matches an item's category applies, whatever its **Jurisdiction**. Create rules only for the taxes you charge every buyer.
:::

## 6. Classify your products

Rules only apply to items that carry the **Taxation** part. Add it to each content type you sell, usually the same type that has the **Product** part (see [Products and Currencies](products-and-currencies.md)).

| | |
| --- | --- |
| **Menu** | Content > Content Definition > Content Types |
| **Permission** | Edit content types |
| **Feature** | Taxation |

<AskYourAdmin />

1. Open **Content > Content Definition > Content Types** and edit the content type.
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

A television is charged 7.5%. A pack of cigarettes matches both rules and is charged 7.5% plus 30%. Each tax shows as its own line in the buyer's order summary.
