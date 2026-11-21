# CrestApps.OrchardCore.Learning.Commerce

Sells courses and learning paths through the CrestApps commerce suite.

- **Courses as products**: courses and learning paths get a price; `LearningProductPart` says what buying one grants (an enrollment, a learning path, or seats for an account) and for how long.
- **Seats**: an account admin buys seats, gives them to members, invites people by e-mail with the seat held until they accept, and takes them back on the **Course seats** page.
- **Fulfilment**: the order records each paid line as a pending purchase inside the checkout; the grants are made right after it, and retried or recorded on the purchase and the order when they cannot be.
- **Refunds**: a full refund or a cancellation takes the open enrollments and the seats back; a completed enrollment keeps its certificate.

See the [Learning Commerce documentation](https://orchardcore.crestapps.com/docs/modules/learning-commerce).
