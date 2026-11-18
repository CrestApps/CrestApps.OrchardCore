---
sidebar_label: Digital credentials
title: Digital credentials
description: "Define a digital credential (an Open Badges 3.0 badge or certificate), award it when a course, quiz, learning path or certification is done or by hand, let learners download and share it, and revoke it."
technical_manual:
  - modules/learning-credentials
---

A digital credential is a badge or certificate that the site signs, so anyone can check it is real. Learners download it to keep in a digital wallet, or share its verification page. The site's credentials follow the Open Badges 3.0 standard.

| | |
| --- | --- |
| **Menu** | Learning > Credential Definitions; Learning > Credential Awards; My credentials (for learners) |
| **Permission** | Manage credential definitions to define credentials; Award and revoke digital credentials to award and revoke; View digital credential awards to see the awards list; Access learning for My credentials |
| **Feature** | Digital Credentials (turns on Secrets if it is off) |

<AskYourAdmin />

Your administrator sets the site's **Base URL** in the site settings, so every credential carries the site's public address.

## Define a credential

1. Go to **Learning > Credential Definitions** and click **New**, then **Credential definition**.
2. Enter the **Title**, pick an **Image**, and write the **Description** (what the holder achieved) and the **Criteria** (what a person did to earn it).
3. Choose the **Achievement type**, such as Badge or Certificate, and add **Tags** if you want them.
4. Enter **Valid for (months)** if the credential expires; leave it empty if it does not.
5. Under **Award rules**, choose what awards it: a course completed, a quiz passed, a learning path completed or a certification issued. Any one rule awards it. Save to get more rule rows.
6. Under **Alignments**, you can link the credential to a skill or certification of the site, or to a competency of an outside framework (a name and an address).
7. Click **Publish**.

A person gets a credential once while their award is valid. A course completion that rests on a result a learning package reported in the browser does not award a credential unless the course item allows it.

## Award a credential by hand

1. Go to **Learning > Credential Awards** and click **Award a credential**.
2. Choose the **Credential** and the **Person**, and write a **Reason** if you want one (only staff see it).
3. Click **Award**.

The person needs an e-mail address: the credential names them by a protected code made from it, never by the address itself.

## See and revoke awards

**Learning > Credential Awards** lists every award. Search by user name, or filter by credential, status (active, expired, revoked) and issue dates. Click **View** to see an award, the result of the site's check of its signature, and the credential.

To revoke one, click **Revoke** (on the award or in the row's menu), write why, and click **Revoke**. The verification page says the credential is revoked at once, and so does the site's revocation list that other verifiers check. A revocation cannot be undone; you can award the credential again by hand.

Some revocations happen by themselves: when a certificate is revoked, the credentials it earned are revoked too, and so are the credentials of a quiz attempt that is voided or no longer passes after a regrade. When a completed enrollment is flagged for review, nothing is revoked until you decide: the review card on the enrollment's page offers **Revoke the credential** for each credential the completion earned.

## For learners: My credentials

Learners open **My credentials** from the account menu or the user menu. For each credential they can:

- **Download (JSON)** or **Download signed (JWT)**, to keep it or add it to a wallet.
- Copy the **Share link**, the credential's verification page.
- **Show my name** or **Hide my name** on the verification page. The name is hidden until the learner shows it.

The site sends a **Credential awarded** notification when a learner earns one.

## The verification page

Anyone with the share link can open the verification page without signing in. The site checks the credential on the spot (the signature, the expiry and the revocation list) and shows **Valid**, **Expired**, **Revoked** or **Not valid**, with the credential, its image, the issuer and the dates. It shows the holder's name only if the holder chose to show it.

## Personal data

When a person's personal data is erased, their credentials are revoked and their details removed from them. Copies they downloaded or shared stay signed, but the revocation list shows them as revoked.
