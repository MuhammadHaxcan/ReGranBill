export type PageGroup = 'vouchers' | 'review' | 'reports' | 'masters' | 'admin' | 'permissions';

export interface PageDefinition {
  key: string;
  label: string;
  group: PageGroup;
  groupLabel: string;
  route: string;
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
  { key: 'delivery-challan',       label: 'Delivery Challan',         group: 'vouchers', groupLabel: 'Vouchers', route: '/delivery-challan' },
  { key: 'purchase-voucher',       label: 'Purchase Voucher',         group: 'vouchers', groupLabel: 'Vouchers', route: '/purchase-voucher' },
  { key: 'sale-return',            label: 'Sale Return',              group: 'vouchers', groupLabel: 'Vouchers', route: '/sale-return' },
  { key: 'purchase-return',        label: 'Purchase Return',          group: 'vouchers', groupLabel: 'Vouchers', route: '/purchase-return' },
  { key: 'receipt-voucher',        label: 'Receipt Voucher',          group: 'vouchers', groupLabel: 'Vouchers', route: '/receipt-voucher' },
  { key: 'payment-voucher',        label: 'Payment Voucher',          group: 'vouchers', groupLabel: 'Vouchers', route: '/payment-voucher' },
  { key: 'journal-voucher',        label: 'Journal Voucher',          group: 'vouchers', groupLabel: 'Vouchers', route: '/journal-voucher' },
  { key: 'pending',                label: 'Pending Review',           group: 'review',   groupLabel: 'Review',   route: '/pending' },
  { key: 'rated-vouchers',         label: 'Rated Vouchers',           group: 'review',   groupLabel: 'Review',   route: '/rated-vouchers' },
  { key: 'voucher-editor',         label: 'Voucher Editor',           group: 'review',   groupLabel: 'Review',   route: '/voucher-editor' },
  { key: 'soa',                    label: 'Statement of Account',     group: 'reports',  groupLabel: 'Reports',  route: '/soa' },
  { key: 'customer-ledger',        label: 'Customer / Vendor Ledger', group: 'reports',  groupLabel: 'Reports',  route: '/customer-ledger' },
  { key: 'master-report',          label: 'Master Report',            group: 'reports',  groupLabel: 'Reports',  route: '/master-report' },
  { key: 'account-closing-report', label: 'Account Closing',          group: 'reports',  groupLabel: 'Reports',  route: '/account-closing-report' },
  { key: 'sale-purchase-report',   label: 'Sale / Purchase Register', group: 'reports',  groupLabel: 'Reports',  route: '/sale-purchase-report' },
  { key: 'product-stock-report',   label: 'Product Stock',            group: 'reports',  groupLabel: 'Reports',  route: '/product-stock-report' },
  { key: 'metadata',               label: 'Categories & Accounts',    group: 'masters',  groupLabel: 'Masters',  route: '/metadata' },
  { key: 'users',                  label: 'Users',                    group: 'admin',    groupLabel: 'Admin',    route: '/users' },
  { key: 'roles',                  label: 'Roles',                    group: 'admin',    groupLabel: 'Admin',    route: '/roles' },
  { key: 'company-settings',       label: 'Company Settings',         group: 'admin',    groupLabel: 'Admin',    route: '/company-settings' },

  // Hidden permissions: not navigable, only assignable. Show up under "Permissions" in the role editor.
  { key: 'voucher-rates',          label: 'View / Edit Rates',        group: 'permissions', groupLabel: 'Permissions', route: '', hidden: true },
];

export const PAGE_GROUPS: PageGroupDefinition[] = [
  { group: 'vouchers',    label: 'Vouchers',    order: 1 },
  { group: 'review',      label: 'Review',      order: 2 },
  { group: 'reports',     label: 'Reports',     order: 3 },
  { group: 'masters',     label: 'Masters',     order: 4 },
  { group: 'admin',       label: 'Admin',       order: 5 },
  { group: 'permissions', label: 'Permissions', order: 6 },
];

const PAGE_BY_KEY = new Map(PAGES.map(p => [p.key, p]));
export const findPage = (key: string): PageDefinition | undefined => PAGE_BY_KEY.get(key);
