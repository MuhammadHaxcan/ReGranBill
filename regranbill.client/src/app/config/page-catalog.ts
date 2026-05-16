export type PageGroup = 'vouchers' | 'production' | 'review' | 'reports' | 'masters' | 'admin' | 'permissions';

export interface PageDefinition {
  key: string;
  label: string;
  group: PageGroup;
  groupLabel: string;
  route: string;
  /** Short one-line summary shown in the command palette. */
  description?: string;
  /**
   * Hidden pages don't render in the sidebar — they're permission flags assigned to roles
   * (e.g. `voucher-rates`). The role editor still shows them under a "Permissions" group.
   */
  hidden?: boolean;
}

export interface PageGroupDefinition {
  group: PageGroup;
  label: string;
  order: number;
}

/**
 * Source-of-truth catalog for every "main" page that can be granted to a role.
 * Supporting routes (per-id edits, prints, add-rate-*) inherit their parent's
 * page key via `data: { pageKey }` in the routing module — they never appear in
 * the sidebar. Backend mirrors this list in `Helpers/PageCatalog.cs`; both must
 * stay in sync.
 */
export const PAGES: PageDefinition[] = [
  { key: 'delivery-challan',       label: 'Delivery Challan',         group: 'vouchers', groupLabel: 'Vouchers', route: '/delivery-challan',         description: 'Issue goods to a customer — creates the DC and downstream inventory.' },
  { key: 'purchase-voucher',       label: 'Purchase Voucher',         group: 'vouchers', groupLabel: 'Vouchers', route: '/purchase-voucher',         description: 'Record goods received from a vendor with lots and rates.' },
  { key: 'sale-return',            label: 'Sale Return',              group: 'vouchers', groupLabel: 'Vouchers', route: '/sale-return',              description: 'Reverse a previous sale — customer returns goods.' },
  { key: 'purchase-return',        label: 'Purchase Return',          group: 'vouchers', groupLabel: 'Vouchers', route: '/purchase-return',          description: 'Return goods back to a vendor against an existing purchase.' },
  { key: 'receipt-voucher',        label: 'Receipt Voucher',          group: 'vouchers', groupLabel: 'Vouchers', route: '/receipt-voucher',          description: 'Record money received — cash or bank credit.' },
  { key: 'payment-voucher',        label: 'Payment Voucher',          group: 'vouchers', groupLabel: 'Vouchers', route: '/payment-voucher',          description: 'Record money paid out — cash or bank debit.' },
  { key: 'journal-voucher',        label: 'Journal Voucher',          group: 'vouchers', groupLabel: 'Vouchers', route: '/journal-voucher',          description: 'Free-form double-entry journal for adjustments.' },
  { key: 'production-voucher',     label: 'Production Voucher',       group: 'production', groupLabel: 'Production', route: '/production-voucher',   description: 'Consume raw materials and produce output / byproducts.' },
  { key: 'formulations',           label: 'Formulations',             group: 'production', groupLabel: 'Production', route: '/formulations',         description: 'Recipe templates that prefill production vouchers.' },
  { key: 'washing-room',           label: 'Washing Room',             group: 'production', groupLabel: 'Production', route: '/washing-voucher',      description: 'Wash unwashed material into washed output with shortage.' },
  { key: 'pending',                label: 'Pending Review',           group: 'review',   groupLabel: 'Review',   route: '/pending',                   description: 'Vouchers awaiting rate entry, grouped by type.' },
  { key: 'rated-vouchers',         label: 'Rated Vouchers',           group: 'review',   groupLabel: 'Review',   route: '/rated-vouchers',            description: 'All fully rated vouchers — view, print, or delete.' },
  { key: 'voucher-editor',         label: 'Voucher Editor',           group: 'review',   groupLabel: 'Review',   route: '/voucher-editor',            description: 'Search and edit any saved voucher by number or party.' },
  { key: 'soa',                    label: 'Statement of Account',     group: 'reports',  groupLabel: 'Reports',  route: '/soa',                       description: 'Per-account ledger of all transactions in a date range.' },
  { key: 'customer-ledger',        label: 'Customer / Vendor Ledger', group: 'reports',  groupLabel: 'Reports',  route: '/customer-ledger',           description: 'Party ledger showing billed / paid / outstanding by item.' },
  { key: 'master-report',          label: 'Master Report',            group: 'reports',  groupLabel: 'Reports',  route: '/master-report',             description: 'Full trial balance with category and account totals.' },
  { key: 'account-closing-report', label: 'Account Closing',          group: 'reports',  groupLabel: 'Reports',  route: '/account-closing-report',    description: 'Opening, movement and closing balances per account.' },
  { key: 'sale-purchase-report',   label: 'Sale / Purchase Register', group: 'reports',  groupLabel: 'Reports',  route: '/sale-purchase-report',      description: 'Date-range summary of sales and purchases by product.' },
  { key: 'product-stock-report',   label: 'Product Stock',            group: 'reports',  groupLabel: 'Reports',  route: '/product-stock-report',      description: 'Live on-hand product quantity and weight.' },
  { key: 'raw-material-lot-report',label: 'Raw Material Lots',        group: 'reports',  groupLabel: 'Reports',  route: '/raw-material-lot-report',   description: 'Lot-wise raw material stock with consumption history.' },
  { key: 'metadata',               label: 'Categories & Accounts',    group: 'masters',  groupLabel: 'Masters',  route: '/metadata',                  description: 'Manage chart of accounts — categories and accounts.' },
  { key: 'users',                  label: 'Users',                    group: 'admin',    groupLabel: 'Admin',    route: '/users',                     description: 'Create users and assign roles.' },
  { key: 'roles',                  label: 'Roles',                    group: 'admin',    groupLabel: 'Admin',    route: '/roles',                     description: 'Define roles and which pages each role can access.' },
  { key: 'company-settings',       label: 'Company Settings',         group: 'admin',    groupLabel: 'Admin',    route: '/company-settings',          description: 'Company name, address, logo and invoice defaults.' },

  // Hidden permissions: not navigable, only assignable. Show up under "Permissions" in the role editor.
  { key: 'voucher-rates',          label: 'View / Edit Rates',        group: 'permissions', groupLabel: 'Permissions', route: '', hidden: true },
];

export const PAGE_GROUPS: PageGroupDefinition[] = [
  { group: 'vouchers',    label: 'Vouchers',    order: 1 },
  { group: 'production',  label: 'Production',  order: 2 },
  { group: 'review',      label: 'Review',      order: 3 },
  { group: 'reports',     label: 'Reports',     order: 4 },
  { group: 'masters',     label: 'Masters',     order: 5 },
  { group: 'admin',       label: 'Admin',       order: 6 },
  { group: 'permissions', label: 'Permissions', order: 7 },
];

const PAGE_BY_KEY = new Map(PAGES.map(p => [p.key, p]));
export const findPage = (key: string): PageDefinition | undefined => PAGE_BY_KEY.get(key);
