---
sidebar_label: Content Fields
title: Content Fields
description: Add the CrestApps phone number field to a content type and set it up for your editors.
technical_manual:
  - modules/content-fields
---

A **content field** is one box on a content item's form, such as a title, a date or a phone number. Orchard Core includes many fields. CrestApps adds the **Phone Field**: a phone number box with a country flag picker that stores every number in one international format. Use it whenever a content type needs a phone number that people will call or text, for example a location's front desk or a partner's main line.

| | |
| --- | --- |
| **Menu** | Content > Content Definition > Content Types |
| **Permission** | Edit content types |
| **Feature** | CrestApps Content Fields |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of enabling Content Fields, adding the Phone Field, and using the country-aware phone input">
  <source src="/img/docs/content-fields.mp4" type="video/mp4" />
</video>

## Add a phone field to a content type

1. Open **Content > Content Definition > Content Types** and edit the content type.
2. Click **Add Field**.
3. Enter the display name editors will see, for example *Main phone*. Orchard Core fills in the **Technical Name** for you.
4. Choose **PhoneField** as the field type and save.
5. On the field's settings page, set the options below and save.

| Field | What it does |
| --- | --- |
| **Required** | Editors must enter a number before they can save the item. |
| **Hint** | Help text shown under the phone box. |
| **Initial country** | Which flag is selected when the box is empty. **Globe** (the default) selects no country. **Current culture** uses the country of the language the editor is using, for example the United States for English (United States). **Specific** always selects the country you choose. |
| **Country** | Shown only for **Specific**. The country selected every time the box is empty. |

Adding and arranging other fields works the same way for every field. See the [Orchard Core content types documentation](https://docs.orchardcore.net/en/latest/reference/modules/ContentTypes/) for the rest of the content type editor.

:::tip
If most of your numbers are in one country, set **Initial country** to **Specific** and pick that country. Editors can then type local numbers without choosing a flag each time.
:::

## Enter a phone number

1. Click the flag to choose the country, or leave the one already selected.
2. Type the number. The box formats it as you type.
3. Save the item.

The site keeps the full international number and the country you picked, so the right flag shows the next time someone edits the item. That matters for countries that share a calling code, such as the United States and Canada.

If the number is not a valid phone number, the item is not saved and the form shows *The (field name) field does not contain a valid phone number.*

## What you see on a saved number

- The number is shown grouped for easy reading, and you can click it to dial it from a device that makes calls.
- When [phone number verification](phone-number-verification.md) has checked the number, an icon shows the result: a green check for verified, a red cross for invalid, a warning sign when the check failed, and a grey question mark when it has not been checked. Point at the icon to see the details and the date it was checked.
- When your site's calling or texting features are on, call and text buttons can appear next to the number.
