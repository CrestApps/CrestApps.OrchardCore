using CrestApps.OrchardCore.Payments.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionConfirmationViewModel
{
    public string SessionId { get; set; }
    public string ServicePlanTitle { get; set; }
    public string ServicePlanDescription { get; set; }
    public string OwnerName { get; set; }
    public string OwnerEmail { get; set; }
    
    // Subscription Details
    public decimal SubscriptionAmount { get; set; }
    public string BillingDuration { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? NextPaymentDate { get; set; }
    public string PaymentMethod { get; set; }
    
    // Initial Payment (if any)
    public decimal? InitialPaymentAmount { get; set; }
    public string InitialPaymentDescription { get; set; }
    
    // Tenant/Site Info (for tenant onboarding)
    public string TenantName { get; set; }
    public string TenantUrl { get; set; }
    public string SiteAdminUsername { get; set; }
    public string SiteAdminEmail { get; set; }
    
    // Management Instructions
    public string ManagementUrl { get; set; }
    public string SupportEmail { get; set; }
}
