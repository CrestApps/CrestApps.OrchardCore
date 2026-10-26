using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// The stable names of the data sources the store's modules publish and of their data sets, so a data set of one
/// module can declare that its fields identify the records of another without referencing it. Report designs store
/// these names, so they must never change.
/// </summary>
/// <remarks>
/// Every data set of a store data source identifies its records with an <see cref="ItemIdField"/> field, except the
/// products, which are content items identified by <see cref="ContentItemIdField"/>, and their variants, identified by
/// <see cref="VariantIdField"/>. A reference to a data source that is not enabled is simply unused.
/// </remarks>
public static class CommerceReportDataSets
{
    /// <summary>
    /// The name of the field that identifies the record of a row in every store data set.
    /// </summary>
    public const string ItemIdField = "ItemId";

    /// <summary>
    /// The name of the field that identifies a product, which is a content item.
    /// </summary>
    public const string ContentItemIdField = "ContentItemId";

    /// <summary>
    /// The name of the field that identifies a variant of a product sold in variants.
    /// </summary>
    public const string VariantIdField = "VariantId";

    /// <summary>
    /// Creates a reference to the identifier of the records of a store data set.
    /// </summary>
    /// <param name="source">The technical name of the data source.</param>
    /// <param name="dataSet">The technical name of the data set.</param>
    /// <returns>The reference.</returns>
    public static DataFieldReference Reference(string source, string dataSet)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentException.ThrowIfNullOrEmpty(dataSet);

        return new DataFieldReference(source, dataSet, ItemIdField);
    }

    /// <summary>
    /// Creates a reference to the identifier of a product.
    /// </summary>
    /// <returns>The reference.</returns>
    public static DataFieldReference ProductReference()
    {
        return new DataFieldReference(ProductSource.Name, ProductSource.Products, ContentItemIdField);
    }

    /// <summary>
    /// Creates a reference to the identifier of a variant of a product.
    /// </summary>
    /// <returns>The reference.</returns>
    public static DataFieldReference VariantReference()
    {
        return new DataFieldReference(ProductSource.Name, ProductSource.Variants, VariantIdField);
    }

    /// <summary>
    /// The names of the orders data source and its data sets.
    /// </summary>
    public static class OrderSource
    {
        /// <summary>
        /// The technical name of the orders data source.
        /// </summary>
        public const string Name = "Orders";

        /// <summary>
        /// The orders data set: one row per order, drafts and quotes included.
        /// </summary>
        public const string Orders = "Orders";

        /// <summary>
        /// The order lines data set: one row per product line of an order.
        /// </summary>
        public const string OrderLines = "OrderLines";

        /// <summary>
        /// The order refunds data set: one row per refund given for an order.
        /// </summary>
        public const string OrderRefunds = "OrderRefunds";

        /// <summary>
        /// The order disputes data set: one row per dispute opened against an order's payment.
        /// </summary>
        public const string OrderDisputes = "OrderDisputes";

        /// <summary>
        /// The order discounts data set: one row per discount an order was given.
        /// </summary>
        public const string OrderDiscounts = "OrderDiscounts";

        /// <summary>
        /// The order taxes data set: one row per tax an order was charged, by jurisdiction.
        /// </summary>
        public const string OrderTaxes = "OrderTaxes";

        /// <summary>
        /// The returns data set: one row per return a buyer asked for.
        /// </summary>
        public const string Returns = "Returns";

        /// <summary>
        /// The gift cards data set: one row per gift card.
        /// </summary>
        public const string GiftCards = "GiftCards";
    }

    /// <summary>
    /// The names of the products data source and its data sets.
    /// </summary>
    public static class ProductSource
    {
        /// <summary>
        /// The technical name of the products data source.
        /// </summary>
        public const string Name = "Products";

        /// <summary>
        /// The products data set: one row per product.
        /// </summary>
        public const string Products = "Products";

        /// <summary>
        /// The product variants data set: one row per variant of a product sold in variants.
        /// </summary>
        public const string Variants = "ProductVariants";

        /// <summary>
        /// The product prices data set: one row per named price a product is offered at, such as a monthly and an
        /// annual plan.
        /// </summary>
        public const string Prices = "ProductPrices";

        /// <summary>
        /// The product bundle items data set: one row per product a bundle is made of.
        /// </summary>
        public const string BundleItems = "ProductBundleItems";

        /// <summary>
        /// The product attributes data set: one row per attribute value a listed product is filtered by.
        /// </summary>
        public const string Attributes = "ProductAttributes";
    }

    /// <summary>
    /// The names of the customers data source and its data sets.
    /// </summary>
    public static class CustomerSource
    {
        /// <summary>
        /// The technical name of the customers data source.
        /// </summary>
        public const string Name = "Customers";

        /// <summary>
        /// The customers data set: one row per customer.
        /// </summary>
        public const string Customers = "Customers";

        /// <summary>
        /// The loyalty accounts data set: one row per customer's loyalty points account.
        /// </summary>
        public const string LoyaltyAccounts = "LoyaltyAccounts";

        /// <summary>
        /// The loyalty activity data set: one row per change to a customer's loyalty points.
        /// </summary>
        public const string LoyaltyActivity = "LoyaltyActivity";

        /// <summary>
        /// The store credit accounts data set: one row per customer's store credit in one currency.
        /// </summary>
        public const string StoreCreditAccounts = "StoreCreditAccounts";

        /// <summary>
        /// The store credit activity data set: one row per change to a customer's store credit.
        /// </summary>
        public const string StoreCreditActivity = "StoreCreditActivity";

        /// <summary>
        /// The referrals data set: one row per customer's referral link.
        /// </summary>
        public const string Referrals = "Referrals";

        /// <summary>
        /// The referral rewards data set: one row per friend's first order a customer was rewarded for.
        /// </summary>
        public const string ReferralRewards = "ReferralRewards";
    }

    /// <summary>
    /// The names of the inventory data source and its data sets.
    /// </summary>
    public static class InventorySource
    {
        /// <summary>
        /// The technical name of the inventory data source.
        /// </summary>
        public const string Name = "Inventory";

        /// <summary>
        /// The stock items data set: one row per product or variant whose stock is counted, with its units at every
        /// location together.
        /// </summary>
        public const string StockItems = "StockItems";

        /// <summary>
        /// The stock levels data set: one row per product or variant stocked at a location, with its units there.
        /// </summary>
        public const string StockLevels = "StockLevels";

        /// <summary>
        /// The stock movements data set: one row per change to the units on hand of a stock item.
        /// </summary>
        public const string StockMovements = "StockMovements";

        /// <summary>
        /// The stock reservations data set: one row per hold of units for an order.
        /// </summary>
        public const string StockReservations = "StockReservations";

        /// <summary>
        /// The purchase orders data set: one row per order of stock from a supplier.
        /// </summary>
        public const string PurchaseOrders = "PurchaseOrders";

        /// <summary>
        /// The purchase order lines data set: one row per stock item a purchase order asks for.
        /// </summary>
        public const string PurchaseOrderLines = "PurchaseOrderLines";

        /// <summary>
        /// The back-in-stock alerts data set: one row per request to be told that a product is back in stock.
        /// </summary>
        public const string StockAlerts = "StockAlerts";

        /// <summary>
        /// The locations data set: one row per place stock is kept.
        /// </summary>
        public const string Locations = "Locations";
    }

    /// <summary>
    /// The names of the checkout data source and its data sets.
    /// </summary>
    public static class CheckoutSource
    {
        /// <summary>
        /// The technical name of the checkout data source.
        /// </summary>
        public const string Name = "Checkout";

        /// <summary>
        /// The checkout sessions data set: one row per checkout a buyer started.
        /// </summary>
        public const string CheckoutSessions = "CheckoutSessions";

        /// <summary>
        /// The coupons data set: one row per coupon.
        /// </summary>
        public const string Coupons = "Coupons";

        /// <summary>
        /// The coupon redemptions data set: one row per use of a coupon code at a checkout.
        /// </summary>
        public const string CouponRedemptions = "CouponRedemptions";

        /// <summary>
        /// The saved payment methods data set: one row per payment method a customer kept for later charges.
        /// </summary>
        public const string SavedPaymentMethods = "SavedPaymentMethods";
    }

    /// <summary>
    /// The names of the carts data source and its data sets.
    /// </summary>
    public static class CartSource
    {
        /// <summary>
        /// The technical name of the carts data source.
        /// </summary>
        public const string Name = "Carts";

        /// <summary>
        /// The carts data set: one row per cart.
        /// </summary>
        public const string Carts = "Carts";

        /// <summary>
        /// The cart lines data set: one row per product line of a cart.
        /// </summary>
        public const string CartLines = "CartLines";

        /// <summary>
        /// The wishlists data set: one row per shopper's wishlist.
        /// </summary>
        public const string Wishlists = "Wishlists";

        /// <summary>
        /// The wishlist items data set: one row per product saved to a wishlist.
        /// </summary>
        public const string WishlistItems = "WishlistItems";
    }

    /// <summary>
    /// The names of the transactions data source and its data sets.
    /// </summary>
    public static class TransactionSource
    {
        /// <summary>
        /// The technical name of the transactions data source.
        /// </summary>
        public const string Name = "Transactions";

        /// <summary>
        /// The transactions data set: one row per transaction.
        /// </summary>
        public const string Transactions = "Transactions";

        /// <summary>
        /// The payment attempts data set: one row per payment the store tried to take.
        /// </summary>
        public const string PaymentAttempts = "PaymentAttempts";

        /// <summary>
        /// The payment refunds data set: one row per refund of a payment.
        /// </summary>
        public const string PaymentRefunds = "PaymentRefunds";
    }

    /// <summary>
    /// The names of the subscriptions data source and its data sets.
    /// </summary>
    public static class SubscriptionSource
    {
        /// <summary>
        /// The technical name of the subscriptions data source.
        /// </summary>
        public const string Name = "Subscriptions";

        /// <summary>
        /// The subscriptions data set: one row per subscription.
        /// </summary>
        public const string Subscriptions = "Subscriptions";

        /// <summary>
        /// The subscription lines data set: one row per line a subscription bills each cycle.
        /// </summary>
        public const string SubscriptionLines = "SubscriptionLines";

        /// <summary>
        /// The installment plans data set: one row per installment plan.
        /// </summary>
        public const string InstallmentPlans = "InstallmentPlans";

        /// <summary>
        /// The installments data set: one row per payment of an installment plan, the down payment included.
        /// </summary>
        public const string Installments = "Installments";
    }

    /// <summary>
    /// The names of the companies data source and its data sets.
    /// </summary>
    public static class CompanySource
    {
        /// <summary>
        /// The technical name of the companies data source.
        /// </summary>
        public const string Name = "Companies";

        /// <summary>
        /// The companies data set: one row per company that buys from the store.
        /// </summary>
        public const string Companies = "Companies";

        /// <summary>
        /// The company buyers data set: one row per person who buys for a company.
        /// </summary>
        public const string CompanyBuyers = "CompanyBuyers";

        /// <summary>
        /// The price lists data set: one row per price list companies buy on.
        /// </summary>
        public const string PriceLists = "PriceLists";

        /// <summary>
        /// The price list entries data set: one row per price a price list sets for a SKU.
        /// </summary>
        public const string PriceListEntries = "PriceListEntries";
    }

    /// <summary>
    /// The names of the point of sale data source and its data sets.
    /// </summary>
    public static class PointOfSaleSource
    {
        /// <summary>
        /// The technical name of the point of sale data source.
        /// </summary>
        public const string Name = "PointOfSale";

        /// <summary>
        /// The registers data set: one row per register.
        /// </summary>
        public const string Registers = "Registers";

        /// <summary>
        /// The register shifts data set: one row per shift worked at a register.
        /// </summary>
        public const string Shifts = "RegisterShifts";
    }

    /// <summary>
    /// The names of the marketplace data source and its data sets.
    /// </summary>
    public static class MarketplaceSource
    {
        /// <summary>
        /// The technical name of the marketplace data source.
        /// </summary>
        public const string Name = "Marketplace";

        /// <summary>
        /// The vendors data set: one row per seller of the marketplace.
        /// </summary>
        public const string Vendors = "Vendors";

        /// <summary>
        /// The vendor earnings data set: one row per entry of a vendor's earnings, a sale or a refund.
        /// </summary>
        public const string Earnings = "VendorEarnings";

        /// <summary>
        /// The vendor payouts data set: one row per transfer of a vendor's released earnings.
        /// </summary>
        public const string Payouts = "VendorPayouts";
    }

    /// <summary>
    /// The names of the shipping data source and its data sets.
    /// </summary>
    public static class ShippingSource
    {
        /// <summary>
        /// The technical name of the shipping data source.
        /// </summary>
        public const string Name = "Shipping";

        /// <summary>
        /// The shipments data set: one row per shipment of an order.
        /// </summary>
        public const string Shipments = "Shipments";

        /// <summary>
        /// The shipment lines data set: one row per order line a shipment holds units of.
        /// </summary>
        public const string ShipmentLines = "ShipmentLines";

        /// <summary>
        /// The shipping zones data set: one row per zone of countries and regions shipping methods apply to.
        /// </summary>
        public const string ShippingZones = "ShippingZones";

        /// <summary>
        /// The shipping methods data set: one row per shipping method a zone offers.
        /// </summary>
        public const string ShippingMethods = "ShippingMethods";
    }

    /// <summary>
    /// The names of the reviews data source and its data sets.
    /// </summary>
    public static class ReviewSource
    {
        /// <summary>
        /// The technical name of the reviews data source.
        /// </summary>
        public const string Name = "Reviews";

        /// <summary>
        /// The product reviews data set: one row per review of a product.
        /// </summary>
        public const string Reviews = "ProductReviews";
    }

    /// <summary>
    /// The names of the downloads data source and its data sets.
    /// </summary>
    public static class DownloadSource
    {
        /// <summary>
        /// The technical name of the downloads data source.
        /// </summary>
        public const string Name = "Downloads";

        /// <summary>
        /// The download entitlements data set: one row per file a buyer may download.
        /// </summary>
        public const string Entitlements = "DownloadEntitlements";
    }

    /// <summary>
    /// The names of the bookings data source and its data sets.
    /// </summary>
    public static class BookingSource
    {
        /// <summary>
        /// The technical name of the bookings data source.
        /// </summary>
        public const string Name = "Bookings";

        /// <summary>
        /// The bookings data set: one row per booking.
        /// </summary>
        public const string Bookings = "Bookings";

        /// <summary>
        /// The booking slots data set: one row per date of a booking product, such as a departure, a night or a time
        /// slot.
        /// </summary>
        public const string Slots = "BookingSlots";

        /// <summary>
        /// The booking balances data set: one row per balance left to pay on an order whose bookings were paid by
        /// deposit.
        /// </summary>
        public const string Balances = "BookingBalances";
    }
}
