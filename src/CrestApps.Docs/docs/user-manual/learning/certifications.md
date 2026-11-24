---
sidebar_label: Certifications
title: Certifications
description: "Let people earn a certification by completing courses and passing quizzes, see who holds it, renew it before it expires, design the printed certificate, and check a certificate in public."
technical_manual:
  - modules/learning-certifications
  - modules/skills
---

A certification can be earned through learning: when a person completes the courses and passes the quizzes you choose, the site issues the certificate, with a number, a printed certificate and a public page that proves it. Before it expires, renewal opens and the holder renews it the same way.

| | |
| --- | --- |
| **Menu** | **Content** → **Certification Types (the certification editor)**; **Content** → **Certificates**; People on this path |
| **Permission** | Manage skills and certifications to set up the path; View enrollments to see the people; Manage enrollments to start someone |
| **Feature** | Learning Certifications (turns on Skills: Issued Certificates) |

<AskYourAdmin />

## Set up a certification path

1. Open the certification in **Content** → **Certification Types**, or add one. Set **Valid for (months)** and the **Reminders (days before expiry)**.
2. In **Issued certificates**, choose whether the site numbers the certificates (and the prefix), and the **Certificate template** to print them with. **Preview a certificate** shows a sample.
3. In **Earned through learning**, turn on **People earn this certification by completing courses and passing assessments**, and add the requirements of the first certificate: a course completed, or a quiz passed, each with an optional minimum grade. Choose whether all of them are needed or any one.
4. Add the renewal requirements, or leave them empty to renew with the same ones.
5. Choose how expiry is counted (from the day the requirements are met, from the previous expiry to keep the anniversary, or a fixed day of the year), when renewal opens (days before expiry) and the grace period after expiry.
6. **Save**.

## Who is on the path

**People on this path** (from the certification editor) lists each person with their state, cycle and certificate. Filter by state, open a certificate, or:

- **Start on the path**: type a user name to start someone by hand. If they already meet the requirements, the certificate is issued at once.
- **Check now**: checks a person again, such as after you fixed a requirement.

## Renewal

On the day renewal opens, the holder gets a notification, is enrolled again in the renewal courses (if you chose so) and sees **Renewal open** on **My certifications**. When they complete the renewal requirements, a new certificate is issued and linked to the old one; the old one stays in their history. If they do not renew, the certificate expires and the path shows **Lapsed**; within the grace period they can still renew, after it they start over.

## Revoke a certificate

If a quiz result a completed course rested on is voided or lowered, the enrollment is flagged for review. Its review card offers **Revoke the certificate**; the verification page shows it as revoked at once. A certificate can also be withdrawn in **Content** → **Certificates**.

## For learners

- **My certifications**, in the **Your account** menu, shows each certification, where they stand, the certificate and its verification page, and what renews it.
- The **verification page** proves a certificate to anyone with its code: the holder's name, the certification, the issuer, the dates and whether it is valid, expired or revoked. The code and a QR code are printed on the certificate.

## Design the printed certificate

Each issued certificate can be opened as a PDF. A certificate template decides how it looks. Without one, the site prints its default design.

1. Go to **Content** → **Content Items**, create a **Certificate Template** and give it a title.
2. Choose the **Paper** (US Letter or A4) and tick **Landscape** to lay the page on its side.
3. In **Font**, enter a font installed on the server, such as Noto Sans for letters beyond Latin. Leave it empty for the default font. The font is embedded in each certificate.
4. Pick the **Accent color** and the **Text color**, and tick **Frame the page with a border** for a double border in the accent color.
5. Add a **Background image** if you like: a JPEG or PNG in the shape of the page. It is stretched over the whole page, behind the text.
6. Tick **Print the QR code of the verification page** and set its **Position and size**. Scanning the code opens the certificate's verification page.
7. Fill in the text blocks. Each row is one block of text: what it says, where it starts (**Left** and **Top**), its **Width**, its **Size**, how it is aligned, how many **Lines** it may take, and whether it is **Bold** or uses the **Accent** color. A width of 0 spans the page. The text can show the holder's name, the certificate number, the dates, the verification code and the certification's name and issuer; the hint above the rows lists the words to use. Clear a block's text to remove it. Save to get three more empty rows.
8. **Publish**.

Positions are in points, 72 to the inch, from the top left of the page. US Letter on its side is 792 by 612 points, and A4 is 842 by 595.

To use the template, open the certification in **Content** → **Certification Types** and choose it as the **Certificate template** under **Issued certificates**. **Preview a certificate** shows a sample with the saved settings.

## Verify a certificate

Anyone can check a certificate on the site's **Verify a certificate** page, with no account. They enter the **Verification code** printed on the certificate, under its QR code, or scan the QR code to go straight to the result.

The page shows whether the certificate is **Valid**, **Expired**, **Revoked** or **Not valid yet**, with the holder, the certification, who issued it, its number, the date it was issued, the date it is valid until and, for a revoked one, the date it was revoked. It says when a certificate was renewed with a newer one. **View the certificate** opens the PDF. A code that matches nothing says so. The page shows nothing more about the holder, and search engines do not list it.

## Education credits

Education credits, such as CPD hours or CEUs, count the learning people do. Open **Learning** → **Education Credits** and click **Add credit type** to create a type, with its unit and whether people can record learning done elsewhere.

- To award credits for a course, edit the course and fill in **Education credits**: the type, the number of credits and, if they expire, for how many months they count. People earn them once when they complete the course. Learning paths can award credits the same way once the part is added to the Learning Path type.
- People see their credits on **My education credits**, where they can also record learning done elsewhere with a certificate as evidence. Reviewers accept or reject it under **Learning** → **External Learning**; accepted learning adds the credits.
- A certification can be renewed by credits: in the **Earned through learning** card, add the requirement **Education credits earned**, choose the credit type and put the number of credits in **Minimum grade or credits**. Only credits earned in the current cycle count.
- To load many records at once, use the **Record external learning** step in a data pipeline.
