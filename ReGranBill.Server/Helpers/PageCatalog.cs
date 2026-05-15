namespace ReGranBill.Server.Helpers;

public sealed record PageDefinition(string Key, string Label, string Group, string GroupLabel, bool Hidden = false);

/// <summary>
/// Canonical list of every "main" page that can be granted to a role. Supporting routes
/// (per-id edits, prints, add-rate-*) inherit their parent page on the frontend and are
/// not enumerated here. Backend mirrors the frontend's <c>config/page-catalog.ts</c>; both
/// must stay in sync. Used by RoleService validation and exposed via /api/pages.
/// </summary>
public static class PageCatalog
{
    public static readonly IReadOnlyList<PageDefinition> All = new[]
    {
        new PageDefinition("delivery-challan",       "Delivery Challan",         "vouchers", "Vouchers"),
        new PageDefinition("purchase-voucher",       "Purchase Voucher",         "vouchers", "Vouchers"),
        new PageDefinition("sale-return",            "Sale Return",              "vouchers", "Vouchers"),
        new PageDefinition("purchase-return",        "Purchase Return",          "vouchers", "Vouchers"),
        new PageDefinition("receipt-voucher",        "Receipt Voucher",          "vouchers", "Vouchers"),
        new PageDefinition("payment-voucher",        "Payment Voucher",          "vouchers", "Vouchers"),
        new PageDefinition("journal-voucher",        "Journal Voucher",          "vouchers", "Vouchers"),
        new PageDefinition("production-voucher",     "Production Voucher",       "production", "Production"),
        new PageDefinition("formulations",           "Formulations",             "production", "Production"),
        new PageDefinition("washing-room",           "Washing Room",             "production", "Production"),
        new PageDefinition("pending",                "Pending Review",           "review",   "Review"),
        new PageDefinition("rated-vouchers",         "Rated Vouchers",           "review",   "Review"),
        new PageDefinition("voucher-editor",         "Voucher Editor",           "review",   "Review"),
        new PageDefinition("soa",                    "Statement of Account",     "reports",  "Reports"),
        new PageDefinition("customer-ledger",        "Customer / Vendor Ledger", "reports",  "Reports"),
        new PageDefinition("master-report",          "Master Report",            "reports",  "Reports"),
        new PageDefinition("account-closing-report", "Account Closing",          "reports",  "Reports"),
        new PageDefinition("sale-purchase-report",   "Sale / Purchase Register", "reports",  "Reports"),
        new PageDefinition("product-stock-report",   "Product Stock",            "reports",  "Reports"),
        new PageDefinition("raw-material-lot-report","Raw Material Lots",        "reports",  "Reports"),
        new PageDefinition("metadata",               "Categories & Accounts",    "masters",  "Masters"),
        new PageDefinition("users",                  "Users",                    "admin",    "Admin"),
        new PageDefinition("roles",                  "Roles",                    "admin",    "Admin"),
        new PageDefinition("company-settings",       "Company Settings",         "admin",    "Admin"),

        // Hidden permissions: not navigable, only assignable. They never appear in the sidebar
        // but show up in the role editor under a "Permissions" group.
        new PageDefinition("voucher-rates",           "View / Edit Rates",        "permissions", "Permissions", Hidden: true),
    };

    public static readonly HashSet<string> ValidKeys =
        new(All.Select(p => p.Key), StringComparer.Ordinal);

    public static bool IsKnown(string key) => ValidKeys.Contains(key);
}
