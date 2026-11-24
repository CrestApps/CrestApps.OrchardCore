---
sidebar_label: Selling courses
title: Selling courses and seats
description: "Put a price on a course, sell seats to business accounts, give seats to members or invite people to them, and take access back on a refund."
technical_manual:
  - modules/learning-commerce
---

Sell a course in your store like any other product. A person who buys it is enrolled as soon as the order is paid. A business account can buy seats, and its admins hand them out to their people. A full refund or a canceled order takes the access back.

| | |
| --- | --- |
| **Menu** | Content, the course's **Product** section; Course seats in the account menu on the storefront |
| **Permission** | Manage courses to put a price on a course; an admin of a business account hands out its seats |
| **Feature** | Learning Commerce (turns on Products, Orders and Account Members) |

<AskYourAdmin />

## Put a price on a course

1. Open the course in **Content** and go to the **Product** section that Learning Commerce added.
2. Enter the price, and the SKU if you use one. A course you leave without a price is simply not for sale: its page shows no price box at all, and leads with its own summary and **Enroll** button. A learning path is sold the same way, from the **Product** section of the path.
3. Under **What buying it grants**, leave every row unticked to enroll the buyer in this course (or start them on this learning path), with access that never ends. To grant something else, tick **Use** on each row you want and choose:
   - **Enrollment** and a course (type part of its title to find it, or keep **This item**), to enroll the buyer in that course.
   - **Learning path**, to start the buyer on a path.
   - **Seats for an account** and the number of **Seats per unit**, to sell seats instead of a place for the buyer.
4. On each ticked row, enter **Access (days)** for access that ends, such as 365 for a year, or leave it empty for access with no end. Each row has its own access length.
5. Click **Publish**.

Only ticked rows are saved; an unticked row is dropped when you publish, whatever you entered in it. As soon as one row is ticked, the ticked rows are all that buying grants: the buyer is no longer enrolled in this course unless a ticked row says so. For a bundle that includes this course, tick a row for **This item** as well as a row for each other course.

The course now shows its price and **Add to cart** on the storefront. A visitor who is not signed in is asked to sign in before they can buy it, because the course goes to their account.

A purchase never asks for an enrollment key or an approval. A course whose **How learners enroll** is **Enrollment key** or **Approval** is not sold, as an enrollment or as seats: the cart says so. To sell such a course anyway, so that buying it is the way in, tick **Buying it enrolls without the enrollment key or approval** under **What buying it grants**. For a course sold only in the store, choose **Closed**: a purchase is one of the ways in it allows.

## Sell seats to a business

A product with **Seats for an account** can only be bought by an admin of a business account; anyone else sees why in the cart. When the order is paid, the account gets the seats: the number per unit times the quantity bought.

## Hand out seats

Account admins open **Course seats** in their account menu on the storefront. Each course the account bought seats in shows the seats left and who has one.

- **Give a seat** to a member of the account: choose them and click **Give a seat**. They are enrolled at once and told so.
- **Invite** someone who is not a member yet: enter their e-mail and click **Invite**. A seat is held for them, and they get an e-mail with a link. When they sign in or register with that address and accept, they join the account and get the seat.
- **Take back** a seat: the person loses access to the course and the seat is free for someone else.

The page shows each person's progress, so the admin can see who has finished.

## Refunds and cancellations

When an order is refunded in full or canceled, the buyer's enrollment ends and the account's seats from that order are taken back, with the enrollments of everyone holding them. A partial refund keeps everything.

:::note
A learning path bought in the store is not taken back on a refund in this version.
:::
