---
sidebar_label: Content Fields
title: Content Fields
description: Add the CrestApps phone number and address fields to a content type and set them up for your editors.
technical_manual:
  - modules/content-fields
---

A **content field** is one box on a content item's form, such as a title, a date or a phone number. Orchard Core includes many fields. CrestApps adds two:

- The **Phone Field**: a phone number box with a country flag picker that stores every number in one international format. Use it whenever a content type needs a phone number that people will call or text, for example a location's front desk or a partner's main line.
- The **Address Field**: a postal address that lays itself out the way the chosen country writes addresses and checks it against that country's rules. Use it for a venue, an office, a home or a work address; a content type can have several.

| | |
| --- | --- |
| **Menu** | Content > Content Definition > Content Types |
| **Permission** | Edit content types |
| **Feature** | CrestApps Content Fields (Phone Field), Address Field |

<AskYourAdmin />

<YouTube video="content-fields" title="Video overview of the phone and address fields" />

<YouTube video="site-administration-walkthroughs" title="Video of enabling Content Fields, adding the Phone Field, and using the country-aware phone input" start={7} end={42} />

## Add a phone field to a content type

<YouTube video="content-fields" title="Video of adding a phone field to a content type" start={104} end={156} />


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

## Add an address field to a content type

<YouTube video="content-fields" title="Video of adding an address field to a content type" start={156} end={203} />


Turn on the **Address Field** feature first. It also turns on **Addresses**.

1. Open **Content > Content Definition > Content Types** and edit the content type.
2. Click **Add Field**, enter the display name editors will see, for example *Location* or *Home address*, choose **AddressField** and save.
3. On the field's settings page, set the options below and save.

| Field | What it does |
| --- | --- |
| **Required** | Editors must enter an address before they can save the item. Otherwise the address can be left blank. |
| **Hint** | Help text shown above the address. |
| **Ask for a recipient name**, **Ask for a company**, **Ask for a phone number** | Which of these the address asks for. The recipient name and the company are ticked for a new field and the phone number is not. A venue might need only the company; a mailing address needs the recipient. |
| **Countries offered** | The countries editors can choose. Select none to offer every country your site serves. |
| **Default country** | The country a new address starts in. |

## Enter an address

<YouTube video="content-fields" title="Video of entering a phone number and an address" start={203} end={269} />


1. Choose the **Country** first. The rest of the form follows it: the fields that country uses, in its order, with its names, such as **State** and **ZIP code** for the United States, **Province** and **Postal code** for Canada, or **Post town** and **Postcode** for the United Kingdom. Where a country has a list of states or provinces, you pick one from it.
2. Fill in the address and save the item.

The address is checked against its country: the fields marked * are required, a postal code has to look like that country's, and it has to belong to the state or province chosen. If something is wrong, the item is not saved and the field shows what to fix, for example *Postal code V6B 1A1 isn't in Ontario.*

On the saved item, the address is shown the way its country's post writes it.

## Manage the countries

<YouTube video="content-fields" title="Video of managing the list of countries and setting up address suggestions" start={269} end={320} />

When the **Address Countries** feature is on, each country the address field can offer is a content item of the type **Country**. They are added the first time the feature is turned on, from the standard list of countries.

1. Open **Content > Content Items**, open the **Content Type** menu and choose **Country**.
2. Type a name in the search box to find a country. The list shows only the items of that type.
3. Click **Edit** to rename a country, **New** and **Country** to add one, or use **Actions** to delete one.

Address fields offer only the countries that are published here, so deleting the countries your site does not serve keeps them out of every list. A field's **Countries offered** setting narrows the list further for that one field.

## Turn on address suggestions

When the **Address Suggestions (Google Places)** feature is on, people typing a street in an address get suggestions, and the rest of the address fills in from the one they pick. Suggestions come from Google Places, which bills your site for each session, so they are off until you give the site a key.

1. Open **Settings > Address suggestions**. You need the **Manage address suggestions** permission.
2. Paste the **Google Places API key** and save. Create the key for the *Places API (New)* in the Google Cloud console, and restrict it to that API.

The site keeps the key on the server and never shows it again: leave the box blank to keep the saved key, tick **Remove the key** to stop suggesting addresses. Without a key, people type their address themselves.
