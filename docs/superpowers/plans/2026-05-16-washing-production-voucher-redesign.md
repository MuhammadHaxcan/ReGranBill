# Washing & Production Voucher Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign Washing and Production Voucher pages to a two-column workspace (form left, persistent live-balance panel right) while preserving every existing field and behavior.

**Architecture:** Each page becomes a CSS grid: left flexible column with the existing form sections, right 320 px fixed column holding a sticky "Live balance" panel with mass / cost / derived rate / Save / Discard. The existing top sticky balance bar and bottom footer are removed; their contents move into the right panel. No new Angular components. No backend changes.

**Tech Stack:** Angular 21 (NgModule-based, not standalone components). The existing `.prod-*` design tokens are reused. CSS is component-scoped (Angular ViewEncapsulation default).

**Spec:** `docs/superpowers/specs/2026-05-16-washing-production-voucher-redesign.md`

**Testing reality:** This app has no working test suite — `src/app/app.spec.ts` is the only spec file and references a non-existent property. Verification is via `ng build` (compile + template typecheck) + manual smoke tests on the dev server.

---

## File structure

| File | What it does after this plan |
|---|---|
| `regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.html` | Two-column layout: header card + Source card + Outputs card on the left; Live balance panel on the right |
| `regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.css` | Same `.prod-*` tokens as before, plus new two-column grid rules and right-panel styles. Old sticky balance bar styles removed. |
| `regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.ts` | Adds `ConfirmModalService` DI + a `discard()` method that mirrors Production's |
| `regranbill.client/src/app/pages/production-voucher/production-voucher.component.html` | Two-column layout; merged Category/Material column in Inputs; merged "Lumps & loss" card combining Byproducts + Shortage; rate-source hint under input rate cell |
| `regranbill.client/src/app/pages/production-voucher/production-voucher.component.css` | Two-column grid + right-panel styles; updated Inputs table column widths after the merge |
| `regranbill.client/src/app/pages/production-voucher/production-voucher.component.ts` | Adds one computed getter `saveDisabledReason` returning the human reason text |

No files are created. No files are deleted.

---

### Task 1: Add `saveDisabledReason` getter to Production component

**Files:**
- Modify: `regranbill.client/src/app/pages/production-voucher/production-voucher.component.ts`

Production already has `balanceErrorTooltip` (mass) and `costBalanceErrorTooltip` (cost) getters. We're adding a single short reason string the panel will render above the Save button when `canSave` is false. Composed from the two existing getters — no new validation logic.

- [ ] **Step 1: Locate `canSave` getter (around line 637)**

Run: `grep -n "get canSave()" regranbill.client/src/app/pages/production-voucher/production-voucher.component.ts`

Expected: prints one line like `637:  get canSave(): boolean {`. Note the line.

- [ ] **Step 2: Add `saveDisabledReason` getter directly above `canSave`**

Open the file, find the line `get canSave(): boolean {` and insert this getter immediately above it:

```typescript
  get saveDisabledReason(): string {
    if (this.canSave) return '';
    if (!this.isBalanced) {
      const delta = Math.abs(this.balanceDelta).toFixed(2);
      return `Mass is ${delta} kg off`;
    }
    if (!this.isCostBalanced) {
      const delta = Math.abs(this.costBalanceDelta).toFixed(2);
      return `Cost is Rs ${delta} off`;
    }
    return 'Cannot save — check rows';
  }

```

Note the trailing blank line so it sits cleanly above `canSave`.

- [ ] **Step 3: Verify build**

Run from `regranbill.client/`:
```
npx ng build --configuration development
```

Expected output ends with `Application bundle generation complete` and zero `error` lines.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Claude/Desktop/ReGranBooks/ReGranBill
git add regranbill.client/src/app/pages/production-voucher/production-voucher.component.ts
git commit -m "$(cat <<'EOF'
feat(prod-voucher): add saveDisabledReason getter

Surfaces the specific reason Save is disabled (mass off vs cost off)
so the upcoming right-side balance panel can render it inline.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Add discard() to Washing component

**Files:**
- Modify: `regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.ts`

Washing currently only has a Save button. The redesigned right panel adds a Discard button. We need a `discard()` method using the same `ConfirmModalService` pattern Production uses.

- [ ] **Step 1: Check current imports for ConfirmModalService**

Run: `grep -n "ConfirmModalService" regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.ts`

Expected: no matches. Confirms we need to add it.

- [ ] **Step 2: Add the import**

Find the existing import line for `ToastService` (around line 1-10). Immediately after it, add:

```typescript
import { ConfirmModalService } from '../../services/confirm-modal.service';
```

- [ ] **Step 3: Add ConfirmModalService to the constructor**

Find the constructor (around line 67):

```typescript
  constructor(
    private accountService: AccountService,
    private categoryService: CategoryService,
    private inventoryLotService: InventoryLotService,
    private washingService: WashingVoucherService,
    private downstreamService: DownstreamUsageService,
    private toast: ToastService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}
```

Add `private confirmModal: ConfirmModalService,` directly above `private cdr: ChangeDetectorRef`:

```typescript
  constructor(
    private accountService: AccountService,
    private categoryService: CategoryService,
    private inventoryLotService: InventoryLotService,
    private washingService: WashingVoucherService,
    private downstreamService: DownstreamUsageService,
    private toast: ToastService,
    private route: ActivatedRoute,
    private router: Router,
    private confirmModal: ConfirmModalService,
    private cdr: ChangeDetectorRef
  ) {}
```

- [ ] **Step 4: Add `discard()` method directly above the existing `private resetForm()` method**

Find `private resetForm(): void {` (around line 443). Insert the following method above it (with a blank line between):

```typescript
  async discard(): Promise<void> {
    if (this.isEditMode) {
      const confirmed = await this.confirmModal.confirm({
        title: 'Discard changes',
        message: 'Discard all unsaved changes to this voucher?',
        confirmText: 'Discard',
        cancelText: 'Cancel'
      });
      if (!confirmed) return;
      this.router.navigate(['/washing-voucher']);
      return;
    }
    const confirmed = await this.confirmModal.confirm({
      title: 'Discard voucher',
      message: 'Clear all entered fields and start fresh?',
      confirmText: 'Discard',
      cancelText: 'Cancel'
    });
    if (confirmed) this.resetForm();
  }

```

- [ ] **Step 5: Verify build**

Run from `regranbill.client/`:
```
npx ng build --configuration development
```

Expected: `Build succeeded.` / `Application bundle generation complete`, no errors.

- [ ] **Step 6: Commit**

```bash
cd /c/Users/Claude/Desktop/ReGranBooks/ReGranBill
git add regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.ts
git commit -m "$(cat <<'EOF'
feat(wash-voucher): add discard() method

Mirrors Production's discard pattern (confirm modal then reset/navigate)
so the upcoming right-side panel can wire a Discard button.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Rewrite Washing voucher HTML into two-column layout

**Files:**
- Modify: `regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.html` (full file replace)

We replace the entire `<div class="prod-page">...</div>` block. The downstream-usage-banner stays at the top. Source fields are regrouped (3 rows: vendor+category, material+lot, weight+rate+wastage%). Outputs card no longer holds the allowed-wastage % input or the derived-rate read-only input or the output-preview block (those move into the right panel or merge into the card header). The bottom sticky balance bar and excess card and footer are deleted (right panel replaces them).

- [ ] **Step 1: Replace the entire file with the following content**

Open `regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.html` and replace its full content with:

```html
<div class="wp-page" *ngIf="!loading">
  <app-downstream-usage-banner
    *ngIf="isEditMode"
    [usages]="downstreamUsages"
    [sourceLabel]="'washing voucher'">
  </app-downstream-usage-banner>

  <div class="wp-grid">
    <!-- ===================== LEFT: editable form ===================== -->
    <div class="wp-left">

      <!-- Header card -->
      <div class="prod-header">
        <div class="prod-header-row">
          <div>
            <div class="prod-eyebrow">Washing</div>
            <h1 class="prod-title">{{ isEditMode ? 'Edit Washing Voucher' : 'New Washing Voucher' }}</h1>
          </div>
          <span class="prod-voucher-num">{{ voucherNumber }}</span>
        </div>
        <div class="prod-header-fields wash-header-grid">
          <div class="prod-field">
            <label class="prod-label">Date</label>
            <input type="text" class="form-control" bsDatepicker
                   [(ngModel)]="voucherDate"
                   [bsConfig]="{ dateInputFormat: 'DD-MM-YYYY', containerClass: 'theme-default' }" />
          </div>
          <div class="prod-field prod-field-wide">
            <label class="prod-label">Description (optional)</label>
            <input type="text" class="form-control" [(ngModel)]="description"
                   placeholder="e.g. Mixed yellow scrap washing lot" />
          </div>
        </div>
      </div>

      <!-- Source card -->
      <div class="prod-section">
        <div class="prod-sec-hd input-hd">
          <div class="prod-sec-hd-left">
            <span class="prod-sec-dot"></span>
            <span class="prod-sec-title">Source</span>
          </div>
          <span class="prod-sec-summary">vendor → material → lot → weight</span>
        </div>
        <div class="wash-section-body">
          <div class="wash-grid-2">
            <div class="prod-field">
              <label class="prod-label">Source vendor</label>
              <app-searchable-select
                [options]="vendorOptions"
                [(ngModel)]="sourceVendorId"
                placeholder="Pick vendor..."
                (ngModelChange)="onVendorOrUnwashedChanged()">
              </app-searchable-select>
            </div>
            <div class="prod-field">
              <label class="prod-label">Input category</label>
              <app-searchable-select
                [options]="sourceCategoryOptions"
                [(ngModel)]="sourceCategoryId"
                placeholder="Pick category..."
                (ngModelChange)="onSourceCategoryChanged()">
              </app-searchable-select>
            </div>
          </div>
          <div class="wash-grid-2">
            <div class="prod-field">
              <label class="prod-label">Unwashed material</label>
              <app-searchable-select
                [options]="unwashedAccountOptions"
                [(ngModel)]="unwashedAccountId"
                [disabled]="!sourceCategoryId"
                placeholder="Pick unwashed account..."
                (ngModelChange)="onVendorOrUnwashedChanged()">
              </app-searchable-select>
            </div>
            <div class="prod-field">
              <label class="prod-label">Source lot</label>
              <app-searchable-select
                [options]="lotOptions"
                [(ngModel)]="selectedLotId"
                placeholder="Pick lot..."
                (ngModelChange)="onSelectedLotChanged()">
              </app-searchable-select>
            </div>
          </div>
          <div class="wash-grid-3">
            <div class="prod-field">
              <label class="prod-label">Input weight (kg)</label>
              <input type="number" min="0" step="0.01" class="form-control prod-num-input"
                     [max]="getMaxInputWeight()"
                     [(ngModel)]="inputWeightKg"
                     (ngModelChange)="onInputWeightChanged()" />
              <small class="wp-field-hint" *ngIf="selectedLot">
                avail {{ selectedLot.availableWeightKg | number:'1.2-2' }} kg
              </small>
            </div>
            <div class="prod-field">
              <label class="prod-label">Lot rate (Rs / kg)</label>
              <input type="number" min="0" step="0.01" class="form-control prod-num-input"
                     [(ngModel)]="rate" />
              <small class="wp-field-hint" *ngIf="rateSource">↩ from selected lot · editable</small>
            </div>
            <div class="prod-field">
              <label class="prod-label">Allowed wastage</label>
              <div class="wp-inline-suffix">
                <input type="number" min="0" max="100" step="0.1" class="form-control prod-num-input"
                       [(ngModel)]="thresholdPct" />
                <span class="wp-inline-suffix-label">%</span>
              </div>
              <small class="wp-field-hint">excess charges vendor at lot rate</small>
            </div>
          </div>
        </div>
      </div>

      <!-- Washed outputs card -->
      <div class="prod-section">
        <div class="prod-sec-hd output-hd">
          <div class="prod-sec-hd-left">
            <span class="prod-sec-dot"></span>
            <span class="prod-sec-title">Washed outputs</span>
          </div>
          <span class="prod-sec-summary" *ngIf="totalOutputWeightKg > 0">
            derived washed rate <strong>Rs {{ washedRate | number:'1.2-2' }} / kg</strong>
          </span>
        </div>
        <div class="wash-output-table">
          <div class="wash-output-head wash-output-head-5">
            <span>Category</span>
            <span>Raw Material</span>
            <span>Weight (kg)</span>
            <span>Amount</span>
            <span></span>
          </div>
          <div class="wash-output-row wash-output-row-5" *ngFor="let line of outputLines; let i = index">
            <div class="prod-field">
              <app-searchable-select
                [options]="outputCategoryOptions"
                [(ngModel)]="line.categoryId"
                placeholder="Pick category..."
                (ngModelChange)="onOutputCategoryChanged(line, $event)">
              </app-searchable-select>
            </div>
            <div class="prod-field">
              <app-searchable-select
                [options]="getOutputAccountOptions(line)"
                [(ngModel)]="line.accountId"
                [disabled]="!line.categoryId"
                placeholder="Pick output raw material...">
              </app-searchable-select>
            </div>
            <div class="prod-field">
              <input type="number" min="0" step="0.01" class="form-control prod-num-input"
                     [max]="getMaxOutputWeight(line)"
                     [(ngModel)]="line.weightKg"
                     (ngModelChange)="onOutputWeightChanged(line)"
                     placeholder="0.00" />
            </div>
            <div class="wash-line-amount">
              {{ getOutputLineDebit(i) | number:'1.2-2' }}
            </div>
            <button type="button" class="wash-remove-line-btn"
                    (click)="removeOutputLine(i)"
                    [disabled]="outputLines.length === 1">
              Remove
            </button>
          </div>
          <div class="wash-output-foot wash-output-row-5" *ngIf="totalOutputWeightKg > 0">
            <span></span>
            <span class="wp-foot-label">Output totals</span>
            <span class="wp-foot-num">{{ totalOutputWeightKg | number:'1.2-2' }}</span>
            <span class="wp-foot-num">{{ washedCost | number:'1.2-2' }}</span>
            <span></span>
          </div>
        </div>
        <div class="wp-add-row">
          <button type="button" class="wp-add-btn" (click)="addOutputLine()">+ Add output line</button>
        </div>
      </div>
    </div>

    <!-- ===================== RIGHT: live balance panel ===================== -->
    <aside class="wp-right">
      <div class="wp-panel"
           [class.wp-panel-warn]="isWastageOverThreshold">
        <div class="wp-panel-hd">
          <span class="wp-panel-dot"></span>
          <span class="wp-panel-title">Live totals</span>
          <span class="wp-status-pill"
                [class.wp-status-balanced]="!isWastageOverThreshold"
                [class.wp-status-warn]="isWastageOverThreshold">
            {{ isWastageOverThreshold ? 'over threshold' : 'within threshold' }}
          </span>
        </div>

        <div class="wp-panel-label">Mass (kg)</div>
        <div class="wp-rows">
          <div class="wp-row"><span class="wp-row-k">IN</span><span class="wp-row-v">{{ inputWeightKg || 0 | number:'1.2-2' }}</span></div>
          <div class="wp-row"><span class="wp-row-k">OUT</span><span class="wp-row-v">{{ totalOutputWeightKg | number:'1.2-2' }}</span></div>
          <div class="wp-row wp-row-warn">
            <span class="wp-row-k">WASTE</span>
            <span class="wp-row-v">{{ wastageKg | number:'1.2-2' }}
              <small>({{ wastagePct | number:'1.1-1' }}%)</small>
            </span>
          </div>
        </div>

        <div class="wp-callout wp-callout-warn" *ngIf="isWastageOverThreshold">
          <div class="wp-callout-title">Over threshold</div>
          <div class="wp-row"><span class="wp-row-k">Allowed ({{ effectiveThresholdPct }}%)</span><span class="wp-row-v">{{ (wastageKg - excessWastageKg) | number:'1.2-2' }} kg</span></div>
          <div class="wp-row"><span class="wp-row-k">Excess</span><span class="wp-row-v">{{ excessWastageKg | number:'1.2-2' }} kg</span></div>
          <div class="wp-row wp-row-divider"><span class="wp-row-k">→ Charged</span><span class="wp-row-v">Rs {{ excessWastageValue | number:'1.2-2' }}</span></div>
        </div>

        <div class="wp-panel-label">Cost (Rs)</div>
        <div class="wp-rows">
          <div class="wp-row"><span class="wp-row-k">Input</span><span class="wp-row-v">{{ inputCost | number:'1.2-2' }}</span></div>
          <div class="wp-row" *ngIf="isWastageOverThreshold"><span class="wp-row-k">− Excess</span><span class="wp-row-v">{{ excessWastageValue | number:'1.2-2' }}</span></div>
          <div class="wp-row wp-row-divider"><span class="wp-row-k">Net</span><span class="wp-row-v">{{ washedCost | number:'1.2-2' }}</span></div>
        </div>

        <div class="wp-panel-label">Washed rate</div>
        <div class="wp-headline">{{ washedRate | number:'1.2-2' }}
          <small>/ kg</small>
        </div>

        <button type="button" class="wp-save-btn"
                [disabled]="!canSave"
                (click)="save()">
          {{ saving ? 'Saving…' : (isEditMode ? 'Update voucher' : 'Save voucher') }}
        </button>
        <button type="button" class="wp-discard-btn" (click)="discard()">Discard</button>
      </div>
    </aside>
  </div>
</div>

<div class="prod-loading" *ngIf="loading">Loading...</div>
```

- [ ] **Step 2: Verify the build compiles**

Run from `regranbill.client/`:
```
npx ng build --configuration development
```

Expected: `Application bundle generation complete`, no errors. (Visual layout will be partly broken until Task 4 lands the CSS; that's expected — we're just confirming all template bindings resolve.)

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Claude/Desktop/ReGranBooks/ReGranBill
git add regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.html
git commit -m "$(cat <<'EOF'
refactor(wash-voucher): rewrite template for two-column layout

Form sections on the left, live-balance panel on the right.
All existing fields preserved; allowed-wastage moves into the
Source card alongside Input weight + Lot rate.

Layout will be partially broken until the CSS task lands.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Add Washing CSS for two-column layout + right panel

**Files:**
- Modify: `regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.css`

We're appending new rules and removing styles tied to the deleted markup (the old sticky `prod-balance` block, the `wash-excess-card`, the `wash-output-preview`, the `wash-rate-strip`). We keep the existing `.prod-*` token rules because the markup still uses them.

- [ ] **Step 1: Locate and delete the obsolete rule blocks**

Open `washing-voucher.component.css`. Search for and delete the entire CSS rule blocks (including their braces) for each of these selectors:

- `.wash-rate-strip` (and `.wash-rate-pill`, `.wash-rate-value`, `.wash-rate-source` — these belong to the deleted source pill)
- `.wash-excess-card` (and `.wash-excess-head`, `.wash-excess-dot`, `.wash-excess-title`, `.wash-excess-body`, `.wash-excess-label`, `.wash-excess-value`, `.wash-excess-amount`)
- `.wash-output-preview` (and `.wash-output-preview-row`)
- `.prod-balance` (and `.prod-balance-cell`, `.prod-balance-label`, `.prod-balance-value`, `.prod-balance-sep`, `.prod-balance-rhs`, `.prod-balance-status`, `.prod-balance-tag`, `.prod-balance.balanced`, `.prod-balance.near`, `.prod-balance.off`)
- `.prod-footer`

Also remove any references to those selectors inside the responsive `@media` blocks at the bottom of the file.

Use Grep to confirm none remain after deletion:
```
grep -nE "wash-rate-strip|wash-excess|wash-output-preview|prod-balance|prod-footer" regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.css
```
Expected: no matches.

- [ ] **Step 2: Append the new layout + panel rules**

Append the following block at the very end of `washing-voucher.component.css`:

```css
/* ────────────────────────────────────────────────────────────
   Two-column workspace (form left, sticky panel right)
   ──────────────────────────────────────────────────────────── */

.wp-page {
  padding: 0 0 32px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.wp-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 320px;
  gap: 16px;
  align-items: start;
}

.wp-left {
  display: flex;
  flex-direction: column;
  gap: 14px;
  min-width: 0;
}

.wp-right {
  position: relative;
}

@media (max-width: 1023px) {
  .wp-grid {
    grid-template-columns: 1fr;
  }
}

/* ── Right panel ──────────────────────────────────────────── */

.wp-panel {
  position: sticky;
  top: 18px;
  background: #0f172a;
  color: #fff;
  border-radius: 14px;
  padding: 18px;
  box-shadow: 0 4px 12px rgba(15, 23, 42, 0.18);
  display: flex;
  flex-direction: column;
}

.wp-panel-hd {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 14px;
}

.wp-panel-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #34d399;
}

.wp-panel.wp-panel-warn .wp-panel-dot {
  background: #fbbf24;
}

.wp-panel-title {
  font-size: 11px;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  font-weight: 700;
}

.wp-status-pill {
  margin-left: auto;
  font-size: 10px;
  padding: 3px 8px;
  border-radius: 99px;
  font-weight: 700;
  text-transform: lowercase;
  letter-spacing: 0.02em;
}

.wp-status-balanced {
  background: #065f46;
  color: #a7f3d0;
}

.wp-status-warn {
  background: #78350f;
  color: #fcd34d;
}

.wp-panel-label {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #94a3b8;
  margin-top: 14px;
  margin-bottom: 6px;
}

.wp-panel-label:first-of-type {
  margin-top: 0;
}

.wp-rows {
  font-family: 'SF Mono', Menlo, monospace;
  font-size: 13px;
  line-height: 1.7;
}

.wp-row {
  display: flex;
  justify-content: space-between;
}

.wp-row-k {
  color: #94a3b8;
}

.wp-row-v {
  font-weight: 600;
}

.wp-row-v small {
  font-size: 10px;
  color: #cbd5e1;
  margin-left: 2px;
}

.wp-row-warn .wp-row-k,
.wp-row-warn .wp-row-v {
  color: #fbbf24;
  font-weight: 700;
}

.wp-row-divider {
  border-top: 1px solid #334155;
  padding-top: 4px;
  margin-top: 4px;
  color: #fff;
}

.wp-row-divider .wp-row-v {
  font-weight: 700;
}

.wp-callout {
  margin-top: 12px;
  padding: 10px;
  border-radius: 8px;
}

.wp-callout-warn {
  background: rgba(251, 191, 36, 0.12);
  border: 1px solid #fbbf24;
}

.wp-callout-title {
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: #fbbf24;
  margin-bottom: 5px;
}

.wp-callout .wp-row-k {
  color: #cbd5e1;
}

.wp-callout .wp-row-divider {
  border-top-color: rgba(251, 191, 36, 0.4);
}

.wp-headline {
  font-family: 'SF Mono', Menlo, monospace;
  font-size: 24px;
  font-weight: 700;
  color: #fff;
  margin-top: 2px;
}

.wp-headline small {
  font-size: 11px;
  color: #94a3b8;
  margin-left: 2px;
}

.wp-save-btn {
  margin-top: 18px;
  width: 100%;
  padding: 11px;
  background: #4f46e5;
  color: #fff;
  border: none;
  border-radius: 8px;
  font-weight: 700;
  font-size: 13px;
  cursor: pointer;
}

.wp-save-btn:hover:not(:disabled) {
  background: #4338ca;
}

.wp-save-btn:disabled {
  background: #334155;
  color: #64748b;
  cursor: not-allowed;
}

.wp-discard-btn {
  margin-top: 6px;
  width: 100%;
  padding: 9px;
  background: transparent;
  color: #94a3b8;
  border: 1px solid #334155;
  border-radius: 8px;
  font-weight: 600;
  font-size: 12px;
  cursor: pointer;
}

.wp-discard-btn:hover {
  background: #1e293b;
  color: #cbd5e1;
}

/* ── Left-side field hints + tfoot ─────────────────────── */

.wp-field-hint {
  display: block;
  font-size: 10px;
  color: #94a3b8;
  margin-top: 3px;
}

.wp-inline-suffix {
  display: flex;
  align-items: center;
  gap: 6px;
}

.wp-inline-suffix .form-control {
  flex: 1;
}

.wp-inline-suffix-label {
  font-size: 13px;
  font-weight: 600;
  color: #64748b;
}

.wash-output-foot {
  background: var(--slate-50);
  font-family: 'SF Mono', Menlo, monospace;
  font-weight: 700;
  border-top: 2px solid var(--border);
  padding: 10px 12px;
}

.wp-foot-label {
  font-family: 'DM Sans', sans-serif;
  font-size: 11px;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  text-align: right;
  font-weight: 600;
}

.wp-foot-num {
  text-align: right;
}

.prod-sec-summary {
  margin-left: auto;
  font-size: 11px;
  color: #94a3b8;
}

.wp-add-row {
  padding: 10px 14px;
  border-top: 1px solid var(--border);
}

.wp-add-btn {
  border: 1.5px dashed #cbd5e1;
  background: transparent;
  color: var(--primary);
  padding: 6px 14px;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
  font-size: 12px;
}

.wp-add-btn:hover {
  background: var(--primary-muted);
}
```

- [ ] **Step 3: Verify build**

```
npx ng build --configuration development
```

Expected: success, no errors.

- [ ] **Step 4: Smoke-test in the browser**

Start the dev server (from `regranbill.client/`):
```
npm start
```
Then in your browser open the washing-voucher page (typically `https://localhost:<port>/washing-voucher`). Confirm:
- Two columns visible at desktop width (≥ 1024 px)
- Right panel sticks when scrolling the left
- Below 1024 px viewport, the right panel wraps under the left column

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Claude/Desktop/ReGranBooks/ReGranBill
git add regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.css
git commit -m "$(cat <<'EOF'
style(wash-voucher): two-column grid + sticky right panel

Adds the wp-* utility rules backing the new template; removes
sticky balance bar, excess card, output preview, and footer styles
(those blocks are gone from the template).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Rewrite Production voucher HTML into two-column layout

**Files:**
- Modify: `regranbill.client/src/app/pages/production-voucher/production-voucher.component.html` (full file replace)

Several structural changes:
- Two-column workspace (left form, right panel)
- Formulation strip moves into the header card (with a dashed-border background, no longer its own section)
- Inputs table: Category and Material merge into one column (the table goes from 10 columns to 9 — net narrower despite Material being wider)
- Rate cell under Inputs gains a "↩ from lot" hint when `row.rateSource` is set
- Outputs unchanged structurally
- Byproducts and Shortage merge into one "Lumps & loss" card with two sub-sections separated by a divider
- Old top sticky balance bar, bottom cost-balance card, bottom footer all deleted (right panel replaces them)

- [ ] **Step 1: Replace the entire file with the following content**

Open `regranbill.client/src/app/pages/production-voucher/production-voucher.component.html` and replace its full content with:

```html
<div class="wp-page" *ngIf="!loading">
  <app-downstream-usage-banner
    *ngIf="isEditMode"
    [usages]="downstreamUsages"
    [sourceLabel]="'production voucher'">
  </app-downstream-usage-banner>

  <div class="wp-grid">
    <!-- ===================== LEFT: editable form ===================== -->
    <div class="wp-left">

      <!-- Header card (with formulation strip nested) -->
      <div class="prod-header">
        <div class="prod-header-row">
          <div>
            <div class="prod-eyebrow">Production</div>
            <h1 class="prod-title">{{ isEditMode ? 'Edit' : 'New' }} Production Voucher</h1>
          </div>
          <span class="prod-voucher-num">{{ voucherNumber }}</span>
        </div>

        <div class="prod-header-fields">
          <div class="prod-field">
            <label class="prod-label">Date</label>
            <input type="text" class="form-control" bsDatepicker
                   [(ngModel)]="voucherDate"
                   [bsConfig]="{ dateInputFormat: 'DD-MM-YYYY', containerClass: 'theme-default' }" />
          </div>
          <div class="prod-field">
            <label class="prod-label">Lot Number</label>
            <input type="text" class="form-control"
                   [(ngModel)]="lotNumber" placeholder="e.g. LOT-2025-014" />
          </div>
          <div class="prod-field prod-field-wide">
            <label class="prod-label">Description</label>
            <input type="text" class="form-control"
                   [(ngModel)]="description" placeholder="Auto-generated if blank" />
          </div>
        </div>

        <div class="wp-formulation-strip">
          <div class="prod-field prod-field-wide">
            <label class="prod-label">Apply formulation <span class="wp-label-soft">(optional)</span></label>
            <app-searchable-select
              [options]="formulationOptions"
              [(ngModel)]="selectedFormulationId"
              placeholder="Pick a saved formulation…">
            </app-searchable-select>
          </div>
          <div class="prod-field">
            <label class="prod-label">Batch kg</label>
            <input type="number" min="0" step="0.01" class="form-control prod-num-input"
                   [(ngModel)]="formulationBatchKg" />
          </div>
          <div class="prod-field prod-field-action">
            <button type="button" class="pv-btn pv-btn-outline-primary"
                    [disabled]="!selectedFormulationId || formulationBatchKg <= 0"
                    (click)="applyFormulation()">
              Apply →
            </button>
          </div>
        </div>
      </div>

      <!-- Inputs card -->
      <div class="prod-section" (click)="focusSection('input')">
        <div class="prod-sec-hd input-hd">
          <div class="prod-sec-hd-left">
            <span class="prod-sec-dot"></span>
            <span class="prod-sec-title">Inputs</span>
            <span class="prod-sec-count">{{ inputs.length }}</span>
          </div>
          <span class="prod-sec-summary">
            {{ inputKg(inputs) | number:'1.2-2' }} kg · Rs {{ totalInputCost | number:'1.2-2' }}
          </span>
          <button type="button" class="pv-btn pv-btn-ghost" (click)="addInput()">
            + Add input <kbd>Alt+N</kbd>
          </button>
        </div>
        <div class="prod-table-wrapper">
          <table class="prod-lines-table">
            <thead>
              <tr>
                <th class="col-num">#</th>
                <th class="col-account-wide">Category · Material</th>
                <th class="col-account">Lot</th>
                <th class="col-bags">Bags</th>
                <th class="col-kg">Kg</th>
                <th class="col-kg">Rate /kg</th>
                <th class="col-amount">Line cost</th>
                <th class="col-act"></th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let row of inputs; let i = index">
                <td class="col-num">{{ i + 1 }}</td>
                <td class="col-account-wide">
                  <div class="wp-cat-mat">
                    <app-searchable-select [options]="inputCategoryOptions" [compact]="true"
                      [(ngModel)]="row.categoryId" placeholder="Pick category..."
                      (ngModelChange)="onInputCategoryChanged(row)">
                    </app-searchable-select>
                    <app-searchable-select [options]="getFilteredInputAccountOptions(row.categoryId)" [compact]="true"
                      [(ngModel)]="row.accountId" placeholder="Pick material..."
                      [disabled]="!row.categoryId"
                      (ngModelChange)="onInputAccountChanged(row)">
                    </app-searchable-select>
                  </div>
                </td>
                <td class="col-account">
                  <app-searchable-select [options]="getFilteredLotOptions(row)" [compact]="true"
                    [(ngModel)]="row.selectedLotId" placeholder="Pick lot..."
                    (ngModelChange)="onInputLotChanged(row)">
                  </app-searchable-select>
                  <small class="wp-cell-hint" *ngIf="row.rateSource">{{ row.rateSource }}</small>
                </td>
                <td class="col-bags">
                  <input type="number" min="0" step="1" class="form-control form-control-sm prod-num-input"
                         [(ngModel)]="row.qty" (ngModelChange)="onLineChanged()" />
                </td>
                <td class="col-kg">
                  <input type="number" min="0" step="0.01" class="form-control form-control-sm prod-num-input"
                         [(ngModel)]="row.weightKg" [max]="getMaxInputWeight(row)"
                         (ngModelChange)="onInputWeightChanged(row)" />
                </td>
                <td class="col-kg">
                  <input type="number" min="0" step="0.01" class="form-control form-control-sm prod-num-input"
                         [(ngModel)]="row.rate" placeholder="Rate" />
                  <small class="wp-cell-hint" *ngIf="row.rateSource">↩ from lot</small>
                </td>
                <td class="col-amount">
                  {{ ((row.weightKg || 0) * (row.rate || 0)) | number:'1.2-2' }}
                </td>
                <td class="col-act">
                  <button type="button" class="btn-remove" title="Remove row" (click)="removeInput(i)">
                    <svg width="14" height="14" viewBox="0 0 14 14" fill="none"><path d="M4 4l6 6M10 4l-6 6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>
                  </button>
                </td>
              </tr>
            </tbody>
            <tfoot *ngIf="inputs.length > 0">
              <tr>
                <td colspan="3" class="wp-foot-label-cell">Input totals</td>
                <td class="col-bags wp-foot-num">{{ inputBags(inputs) }}</td>
                <td class="col-kg wp-foot-num">{{ inputKg(inputs) | number:'1.2-2' }}</td>
                <td></td>
                <td class="col-amount wp-foot-num">{{ totalInputCost | number:'1.2-2' }}</td>
                <td></td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

      <!-- Outputs card -->
      <div class="prod-section" (click)="focusSection('output')">
        <div class="prod-sec-hd output-hd">
          <div class="prod-sec-hd-left">
            <span class="prod-sec-dot"></span>
            <span class="prod-sec-title">Outputs</span>
            <span class="prod-sec-count">{{ outputs.length }}</span>
          </div>
          <span class="prod-sec-summary">
            {{ inputKg(outputs) | number:'1.2-2' }} kg · Rs {{ totalOutputCost | number:'1.2-2' }}
          </span>
          <button type="button" class="pv-btn pv-btn-ghost" (click)="addOutput()">+ Add output</button>
        </div>
        <div class="prod-table-wrapper">
          <table class="prod-lines-table">
            <thead>
              <tr>
                <th class="col-num">#</th>
                <th class="col-account">Category</th>
                <th class="col-account-wide">Raw Material</th>
                <th class="col-bags">Bags</th>
                <th class="col-kg">Kg</th>
                <th class="col-rate">Rate</th>
                <th class="col-amount">Amount</th>
                <th class="col-act"></th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let row of outputs; let i = index">
                <td class="col-num">{{ i + 1 }}</td>
                <td class="col-account">
                  <app-searchable-select [options]="outputCategoryOptions" [compact]="true"
                    [(ngModel)]="row.categoryId" placeholder="Pick category..."
                    (ngModelChange)="onRowCategoryChanged(row)">
                  </app-searchable-select>
                </td>
                <td class="col-account-wide">
                  <app-searchable-select [options]="getFilteredAccountOptions(AccountType.Product, row.categoryId)" [compact]="true"
                    [(ngModel)]="row.accountId" [disabled]="!row.categoryId" placeholder="Pick output product..."
                    (ngModelChange)="onLineChanged()">
                  </app-searchable-select>
                </td>
                <td class="col-bags">
                  <input type="number" min="0" step="1" class="form-control form-control-sm prod-num-input"
                         [(ngModel)]="row.qty" (ngModelChange)="onLineChanged()" />
                </td>
                <td class="col-kg">
                  <input type="number" min="0" step="0.01" class="form-control form-control-sm prod-num-input"
                         [(ngModel)]="row.weightKg" [max]="getMaxOutputWeight(row)"
                         (ngModelChange)="onOutputWeightChanged(row)" />
                </td>
                <td class="col-rate">
                  <input type="number" min="0" step="0.01" class="form-control form-control-sm prod-num-input"
                         [(ngModel)]="row.rate" placeholder="0.00" />
                </td>
                <td class="col-amount">{{ (row.weightKg || 0) * (row.rate || 0) | number:'1.2-2' }}</td>
                <td class="col-act">
                  <button type="button" class="btn-remove" title="Remove row" (click)="removeOutput(i)">
                    <svg width="14" height="14" viewBox="0 0 14 14" fill="none"><path d="M4 4l6 6M10 4l-6 6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>
                  </button>
                </td>
              </tr>
            </tbody>
            <tfoot *ngIf="outputs.length > 0">
              <tr>
                <td colspan="3" class="wp-foot-label-cell">Output totals</td>
                <td class="col-bags wp-foot-num">{{ inputBags(outputs) }}</td>
                <td class="col-kg wp-foot-num">{{ inputKg(outputs) | number:'1.2-2' }}</td>
                <td></td>
                <td class="col-amount wp-foot-num">{{ totalOutputCost | number:'1.2-2' }}</td>
                <td></td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

      <!-- Lumps & loss card: Byproducts + Shortage merged -->
      <div class="prod-section">
        <div class="prod-sec-hd byproduct-hd">
          <div class="prod-sec-hd-left">
            <span class="prod-sec-dot"></span>
            <span class="prod-sec-title">Lumps &amp; loss</span>
          </div>
          <span class="prod-sec-summary">balances mass + cost</span>
        </div>

        <!-- Byproducts sub-section -->
        <div class="wp-sub-section" (click)="focusSection('byproduct')">
          <div class="wp-sub-hd">
            <span class="wp-sub-title">Byproducts <span class="wp-label-soft">(recoverable lumps)</span></span>
            <button type="button" class="pv-btn pv-btn-ghost wp-sub-btn" (click)="addByproduct()">+ Add byproduct</button>
          </div>
          <div class="prod-table-wrapper" *ngIf="byproducts.length > 0">
            <table class="prod-lines-table">
              <thead>
                <tr>
                  <th class="col-num">#</th>
                  <th class="col-account">Category</th>
                  <th class="col-account-wide">Raw Material</th>
                  <th class="col-bags">Bags</th>
                  <th class="col-kg">Kg</th>
                  <th class="col-rate">Rate</th>
                  <th class="col-amount">Amount</th>
                  <th class="col-act"></th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let row of byproducts; let i = index">
                  <td class="col-num">{{ i + 1 }}</td>
                  <td class="col-account">
                    <app-searchable-select [options]="byproductCategoryOptions" [compact]="true"
                      [(ngModel)]="row.categoryId" placeholder="Pick category..."
                      (ngModelChange)="onRowCategoryChanged(row)">
                    </app-searchable-select>
                  </td>
                  <td class="col-account-wide">
                    <app-searchable-select [options]="getFilteredByproductAccountOptions(row.categoryId)" [compact]="true"
                      [(ngModel)]="row.accountId" [disabled]="!row.categoryId" placeholder="Pick byproduct account..."
                      (ngModelChange)="onLineChanged()">
                    </app-searchable-select>
                  </td>
                  <td class="col-bags">
                    <input type="number" min="0" step="1" class="form-control form-control-sm prod-num-input"
                           [(ngModel)]="row.qty" (ngModelChange)="onLineChanged()" />
                  </td>
                  <td class="col-kg">
                    <input type="number" min="0" step="0.01" class="form-control form-control-sm prod-num-input"
                           [(ngModel)]="row.weightKg" [max]="getMaxByproductWeight(row)"
                           (ngModelChange)="onByproductWeightChanged(row)" />
                  </td>
                  <td class="col-rate">
                    <input type="number" min="0" step="0.01" class="form-control form-control-sm prod-num-input"
                           [(ngModel)]="row.rate" placeholder="0.00" />
                  </td>
                  <td class="col-amount">{{ (row.weightKg || 0) * (row.rate || 0) | number:'1.2-2' }}</td>
                  <td class="col-act">
                    <button type="button" class="btn-remove" title="Remove row" (click)="removeByproduct(i)">
                      <svg width="14" height="14" viewBox="0 0 14 14" fill="none"><path d="M4 4l6 6M10 4l-6 6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>
                    </button>
                  </td>
                </tr>
              </tbody>
              <tfoot>
                <tr>
                  <td colspan="3" class="wp-foot-label-cell">Byproduct totals</td>
                  <td class="col-bags wp-foot-num">{{ inputBags(byproducts) }}</td>
                  <td class="col-kg wp-foot-num">{{ inputKg(byproducts) | number:'1.2-2' }}</td>
                  <td></td>
                  <td class="col-amount wp-foot-num">{{ totalByproductCost | number:'1.2-2' }}</td>
                  <td></td>
                </tr>
              </tfoot>
            </table>
          </div>
          <div class="wp-sub-empty" *ngIf="byproducts.length === 0">
            No byproducts. Click <strong>+ Add byproduct</strong> if recoverable lumps came out.
          </div>
        </div>

        <!-- Shortage sub-section (yellow tinted, written-off) -->
        <div class="wp-sub-section wp-sub-shortage">
          <div class="wp-sub-hd">
            <span class="wp-sub-title">Shortage <span class="wp-label-soft">(written-off loss)</span></span>
            <button type="button" class="pv-btn pv-btn-ghost wp-sub-btn"
                    [disabled]="!shortageUserEdited"
                    (click)="resetShortageOverride()">
              ↻ auto-suggest {{ getMaxShortageWeight() | number:'1.2-2' }} kg
            </button>
          </div>
          <div class="wp-shortage-grid">
            <div>
              <label class="prod-label">Expense Category</label>
              <app-searchable-select
                [options]="shortageCategoryOptions"
                [(ngModel)]="shortageCategoryId"
                placeholder="Pick expense category..."
                (ngModelChange)="onShortageCategoryChanged()">
              </app-searchable-select>
            </div>
            <div>
              <label class="prod-label">Production Loss Account</label>
              <app-searchable-select
                [options]="shortageExpenseOptions"
                [(ngModel)]="shortageAccountId"
                [disabled]="!shortageCategoryId"
                placeholder="Pick a Production Loss / Scrap expense account...">
              </app-searchable-select>
            </div>
            <div>
              <label class="prod-label">Kg</label>
              <input type="number" min="0" step="0.01" class="form-control prod-num-input"
                     [max]="getMaxShortageWeight()"
                     [(ngModel)]="shortageWeightKg"
                     (ngModelChange)="onShortageChanged()" />
            </div>
            <div>
              <label class="prod-label">Rate</label>
              <input type="number" min="0" step="0.01" class="form-control prod-num-input"
                     [(ngModel)]="shortageRate"
                     (ngModelChange)="onShortageRateChanged()"
                     placeholder="0.00" />
            </div>
            <div>
              <label class="prod-label">Amount</label>
              <div class="prod-amount-cell">Rs {{ shortageCost | number:'1.2-2' }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ===================== RIGHT: live balance panel ===================== -->
    <aside class="wp-right">
      <div class="wp-panel"
           [class.wp-panel-warn]="balanceState !== 'balanced' || !isCostBalanced">
        <div class="wp-panel-hd">
          <span class="wp-panel-dot"></span>
          <span class="wp-panel-title">Live balance</span>
          <span class="wp-status-pill"
                [class.wp-status-balanced]="balanceState === 'balanced' && isCostBalanced"
                [class.wp-status-warn]="balanceState === 'near'"
                [class.wp-status-off]="balanceState === 'off' || !isCostBalanced">
            <ng-container [ngSwitch]="balanceState">
              <ng-container *ngSwitchCase="'balanced'">{{ isCostBalanced ? 'balanced' : 'cost off' }}</ng-container>
              <ng-container *ngSwitchCase="'near'">near</ng-container>
              <ng-container *ngSwitchCase="'off'">off</ng-container>
            </ng-container>
          </span>
        </div>

        <div class="wp-panel-label">Mass (kg)</div>
        <div class="wp-rows">
          <div class="wp-row"><span class="wp-row-k">IN</span><span class="wp-row-v">{{ totalInputKg | number:'1.2-2' }}</span></div>
          <div class="wp-row"><span class="wp-row-k">OUT</span><span class="wp-row-v">{{ totalOutputKg | number:'1.2-2' }}</span></div>
          <div class="wp-row"><span class="wp-row-k">LUMPS</span><span class="wp-row-v">{{ totalByproductKg | number:'1.2-2' }}</span></div>
          <div class="wp-row"><span class="wp-row-k">SHORT</span><span class="wp-row-v">{{ shortageWeightKg | number:'1.2-2' }}</span></div>
          <div class="wp-row wp-row-divider"
               [class.wp-row-good]="balanceState === 'balanced'"
               [class.wp-row-warn]="balanceState === 'near'"
               [class.wp-row-bad]="balanceState === 'off'">
            <span class="wp-row-k">Δ</span>
            <span class="wp-row-v">{{ balanceDelta | number:'1.2-2' }}
              <ng-container *ngIf="balanceState === 'balanced'">✓</ng-container>
            </span>
          </div>
        </div>

        <div class="wp-panel-label">Cost (Rs)</div>
        <div class="wp-rows">
          <div class="wp-row"><span class="wp-row-k">Input</span><span class="wp-row-v">{{ totalInputCost | number:'1.2-2' }}</span></div>
          <div class="wp-row"><span class="wp-row-k">Output</span><span class="wp-row-v">{{ totalOutputCost | number:'1.2-2' }}</span></div>
          <div class="wp-row"><span class="wp-row-k">+ Lumps</span><span class="wp-row-v">{{ totalByproductCost | number:'1.2-2' }}</span></div>
          <div class="wp-row"><span class="wp-row-k">+ Short</span><span class="wp-row-v">{{ shortageCost | number:'1.2-2' }}</span></div>
          <div class="wp-row wp-row-divider"
               [class.wp-row-good]="isCostBalanced"
               [class.wp-row-bad]="!isCostBalanced">
            <span class="wp-row-k">Δ</span>
            <span class="wp-row-v">{{ costBalanceDelta | number:'1.2-2' }}
              <ng-container *ngIf="isCostBalanced">✓</ng-container>
            </span>
          </div>
        </div>

        <div class="wp-panel-label">Derived rate</div>
        <div class="wp-headline">{{ derivedRate | number:'1.2-2' }}
          <small>/ kg</small>
        </div>

        <div class="wp-save-reason" *ngIf="!canSave">{{ saveDisabledReason }}</div>
        <button type="button" class="wp-save-btn"
                [disabled]="!canSave"
                (click)="save()">
          {{ saving ? 'Saving…' : (isEditMode ? 'Update voucher' : 'Save voucher') }}
        </button>
        <button type="button" class="wp-discard-btn" (click)="discard()">Discard</button>
      </div>
    </aside>
  </div>
</div>

<div class="prod-loading" *ngIf="loading">Loading...</div>
```

- [ ] **Step 2: Verify the build compiles**

```
npx ng build --configuration development
```

Expected: success. (Layout will be partly broken until Task 6 CSS lands.)

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Claude/Desktop/ReGranBooks/ReGranBill
git add regranbill.client/src/app/pages/production-voucher/production-voucher.component.html
git commit -m "$(cat <<'EOF'
refactor(prod-voucher): rewrite template for two-column layout

Left column: header + formulation strip nested inside header,
Inputs (Category·Material merged column, rate hint cell),
Outputs, and a single 'Lumps & loss' card combining Byproducts
+ Shortage. Right column: live mass + cost balance panel with
Save / Discard.

Layout will be partially broken until the CSS task lands.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Add Production CSS for two-column layout + right panel + new column widths

**Files:**
- Modify: `regranbill.client/src/app/pages/production-voucher/production-voucher.component.css`

We need to:
1. Delete the now-unused old top sticky balance bar + bottom cost-balance card + bottom footer rules.
2. Add the same `wp-*` utility rules from Washing (CSS is component-scoped, so duplication is required).
3. Add Production-specific rules: `wp-cat-mat` (the two stacked selects in the merged column), `wp-cell-hint` (under rate input), `wp-formulation-strip` (the dashed-border strip), `wp-sub-*` and `wp-shortage-grid` (the merged Lumps & loss card).
4. Adjust Inputs table column widths since one column was removed.

- [ ] **Step 1: Delete obsolete rules**

Open `production-voucher.component.css` and remove the entire CSS rule blocks for:

- `.prod-balance` and all child/state selectors (`.prod-balance-cell`, `.prod-balance-label`, `.prod-balance-value`, `.prod-balance-sep`, `.prod-balance-rhs`, `.prod-balance-status`, `.prod-balance-tag`, `.prod-balance.balanced`, `.prod-balance.near`, `.prod-balance.off`, `.prod-balance-tag.balanced`, `.prod-balance-tag.near`, `.prod-balance-tag.off`)
- `.prod-cost-balance` (and `.prod-cost-balance-row`, `.prod-cost-balance-off`, `.prod-cost-cell`, `.prod-cost-label`, `.prod-cost-value`, `.prod-cost-delta-off`)
- `.prod-recipe-strip` (now replaced by `.wp-formulation-strip`)
- `.prod-shortage-row` and `.prod-shortage-hint` (now replaced by `.wp-shortage-grid` and panel reason text)
- `.prod-sec-empty` (replaced by `.wp-sub-empty`)
- `.prod-sec-totals` (replaced by `tfoot` rules and section-header summary)
- `.prod-footer`

Confirm with:
```
grep -nE "prod-balance|prod-cost-balance|prod-recipe-strip|prod-shortage-row|prod-shortage-hint|prod-sec-empty|prod-sec-totals|prod-footer" regranbill.client/src/app/pages/production-voucher/production-voucher.component.css
```
Expected: no matches.

- [ ] **Step 2: Update Inputs table column widths**

Find the existing column-width rules (around line 305 — search `.col-account`). The current Inputs grid had 4 account-class columns; the new one has fewer. Confirm the existing widths still make sense:

- `.col-num { width: 40px }` — keep
- `.col-account { min-width: 180px }` — keep (used by Outputs & Byproducts categories + Inputs lot)
- `.col-account-wide { min-width: 220px }` — keep (used by Outputs/Byproducts Raw Material AND new Inputs Category·Material merged column)
- `.col-lot-info` — **delete this rule** (the Lot Info column is gone — its sublabel now appears beneath the Lot select)
- `.col-bags { width: 80px }` — keep
- `.col-kg { width: 110px }` — keep
- `.col-rate { width: 100px }` — keep
- `.col-amount { width: 110px }` — keep
- `.col-act { width: 36px }` — keep

If any width rule is missing or wrong vs the list above, adjust to match. The merged `Category · Material` cell uses `.col-account-wide` and the inner CSS for stacking the two selects is added next.

- [ ] **Step 3: Append the new layout + panel + Production-specific rules**

Append the following block at the end of `production-voucher.component.css`:

```css
/* ────────────────────────────────────────────────────────────
   Two-column workspace (form left, sticky panel right)
   Mirrors the same rules used in washing-voucher (CSS is
   component-scoped in Angular so duplication is required).
   ──────────────────────────────────────────────────────────── */

.wp-page {
  padding: 0 0 32px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.wp-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 320px;
  gap: 16px;
  align-items: start;
}

.wp-left {
  display: flex;
  flex-direction: column;
  gap: 14px;
  min-width: 0;
}

.wp-right {
  position: relative;
}

@media (max-width: 1023px) {
  .wp-grid {
    grid-template-columns: 1fr;
  }
}

/* ── Right panel ─────────────────────────────────────────── */

.wp-panel {
  position: sticky;
  top: 18px;
  background: #0f172a;
  color: #fff;
  border-radius: 14px;
  padding: 18px;
  box-shadow: 0 4px 12px rgba(15, 23, 42, 0.18);
  display: flex;
  flex-direction: column;
}

.wp-panel-hd {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 14px;
}

.wp-panel-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #34d399;
}

.wp-panel.wp-panel-warn .wp-panel-dot {
  background: #fbbf24;
}

.wp-panel-title {
  font-size: 11px;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  font-weight: 700;
}

.wp-status-pill {
  margin-left: auto;
  font-size: 10px;
  padding: 3px 8px;
  border-radius: 99px;
  font-weight: 700;
  text-transform: lowercase;
  letter-spacing: 0.02em;
}

.wp-status-balanced { background: #065f46; color: #a7f3d0; }
.wp-status-warn     { background: #78350f; color: #fcd34d; }
.wp-status-off      { background: #7f1d1d; color: #fecaca; }

.wp-panel-label {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #94a3b8;
  margin-top: 14px;
  margin-bottom: 6px;
}

.wp-panel-label:first-of-type { margin-top: 0; }

.wp-rows {
  font-family: 'SF Mono', Menlo, monospace;
  font-size: 13px;
  line-height: 1.7;
}

.wp-row { display: flex; justify-content: space-between; }
.wp-row-k { color: #94a3b8; }
.wp-row-v { font-weight: 600; }
.wp-row-v small { font-size: 10px; color: #cbd5e1; margin-left: 2px; }

.wp-row-divider {
  border-top: 1px solid #334155;
  padding-top: 4px;
  margin-top: 4px;
  color: #fff;
}
.wp-row-divider .wp-row-v { font-weight: 700; }
.wp-row-good .wp-row-k, .wp-row-good .wp-row-v { color: #34d399; }
.wp-row-warn .wp-row-k, .wp-row-warn .wp-row-v { color: #fbbf24; }
.wp-row-bad  .wp-row-k, .wp-row-bad  .wp-row-v { color: #f87171; }

.wp-headline {
  font-family: 'SF Mono', Menlo, monospace;
  font-size: 24px;
  font-weight: 700;
  color: #fff;
  margin-top: 2px;
}
.wp-headline small { font-size: 11px; color: #94a3b8; margin-left: 2px; }

.wp-save-reason {
  margin-top: 18px;
  padding: 8px 10px;
  background: rgba(248, 113, 113, 0.12);
  border: 1px solid #f87171;
  border-radius: 8px;
  color: #fca5a5;
  font-size: 11.5px;
  font-weight: 600;
  text-align: center;
}

.wp-save-btn {
  margin-top: 8px;
  width: 100%;
  padding: 11px;
  background: #4f46e5;
  color: #fff;
  border: none;
  border-radius: 8px;
  font-weight: 700;
  font-size: 13px;
  cursor: pointer;
}
.wp-save-btn:hover:not(:disabled) { background: #4338ca; }
.wp-save-btn:disabled { background: #334155; color: #64748b; cursor: not-allowed; }

.wp-discard-btn {
  margin-top: 6px;
  width: 100%;
  padding: 9px;
  background: transparent;
  color: #94a3b8;
  border: 1px solid #334155;
  border-radius: 8px;
  font-weight: 600;
  font-size: 12px;
  cursor: pointer;
}
.wp-discard-btn:hover { background: #1e293b; color: #cbd5e1; }

/* ── Production-specific left-side rules ─────────────────── */

.wp-formulation-strip {
  margin-top: 14px;
  padding: 12px 14px;
  background: var(--slate-50);
  border: 1px dashed #cbd5e1;
  border-radius: 10px;
  display: grid;
  grid-template-columns: 1fr 140px auto;
  gap: 12px;
  align-items: end;
}

.wp-label-soft {
  color: var(--text-muted);
  font-weight: 500;
  text-transform: none;
  letter-spacing: 0;
}

.prod-sec-summary {
  margin-left: auto;
  font-size: 11px;
  color: #94a3b8;
  padding-right: 8px;
}

.wp-cat-mat {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.wp-cell-hint {
  display: block;
  font-size: 10px;
  color: var(--text-muted);
  margin-top: 3px;
  line-height: 1.3;
}

.wp-foot-label-cell {
  text-align: right;
  font-family: 'DM Sans', sans-serif;
  font-size: 11px;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  font-weight: 600;
  padding: 10px 12px;
  background: var(--slate-50);
}

tfoot .wp-foot-num {
  text-align: right;
  font-family: 'SF Mono', Menlo, monospace;
  font-weight: 700;
  background: var(--slate-50);
  padding: 10px 12px;
}

/* ── Lumps & loss card sub-sections ──────────────────────── */

.wp-sub-section {
  padding: 14px 18px;
  border-top: 1px solid var(--border);
}

.wp-sub-section:first-of-type {
  border-top: none;
}

.wp-sub-shortage {
  background: #fefce8;
}

.wp-sub-hd {
  display: flex;
  align-items: center;
  margin-bottom: 8px;
}

.wp-sub-title {
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--text-secondary);
  font-weight: 700;
}

.wp-sub-btn {
  margin-left: auto;
  font-size: 11px;
  padding: 4px 10px;
}

.wp-sub-empty {
  padding: 14px;
  text-align: center;
  color: var(--text-muted);
  font-size: 12px;
  background: #fafbfc;
  border-radius: 8px;
}

.wp-shortage-grid {
  display: grid;
  grid-template-columns: 1.4fr 1.6fr 90px 100px 110px;
  gap: 8px;
  align-items: end;
}

@media (max-width: 800px) {
  .wp-formulation-strip { grid-template-columns: 1fr; }
  .wp-shortage-grid { grid-template-columns: 1fr 1fr; }
}
```

- [ ] **Step 4: Verify build**

```
npx ng build --configuration development
```

Expected: success, no errors.

- [ ] **Step 5: Smoke-test in the browser**

With dev server running (`npm start`), open the production-voucher page. Confirm:
- Two columns at desktop width; right panel sticks while you scroll
- Formulation strip is the dashed-bg block inside the header card
- Inputs table shows Category · Material stacked in one wider cell; the "↩ from lot" hint appears under the Rate cell once you pick a lot
- Lumps & loss card shows Byproducts above Shortage with a divider; Shortage half is yellow-tinted
- Right panel shows mass IN/OUT/LUMPS/SHORT/Δ and cost equivalents; status pill at top reflects state
- Save button at bottom of panel; when balance is off, reason text appears above Save

- [ ] **Step 6: Commit**

```bash
cd /c/Users/Claude/Desktop/ReGranBooks/ReGranBill
git add regranbill.client/src/app/pages/production-voucher/production-voucher.component.css
git commit -m "$(cat <<'EOF'
style(prod-voucher): two-column grid + sticky right panel

- Removes old sticky balance bar, cost-balance card, formulation strip,
  shortage row, sec-totals, footer styles
- Adds wp-* utility rules (duplicated from washing because Angular
  CSS is component-scoped — no shared component to factor into yet)
- Adds Production-specific rules: merged Category·Material column,
  rate-source hint cell, formulation strip, Lumps & loss sub-sections

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: End-to-end smoke verification

**Files:** None modified.

Run the full set of scenarios from the spec's "Testing & verification" section against the dev server.

- [ ] **Step 1: Start the backend + frontend**

In one terminal:
```
cd ReGranBill.Server && dotnet run
```
In another:
```
cd regranbill.client && npm start
```

- [ ] **Step 2: Washing — new voucher happy path**

Navigate to `/washing-voucher`. Pick vendor → category → unwashed material → lot. Confirm:
- Lot rate populates in the Lot rate input
- "↩ from selected lot · editable" hint appears beneath the rate
- Right panel "within threshold" status, IN/OUT/WASTE all updating live as you enter weights
- Add 2 output lines, fill them
- Save succeeds, form resets, new voucher number fetched

- [ ] **Step 3: Washing — over-threshold path**

Set allowed wastage to 5% and produce wastage > 5%. Confirm:
- Right panel status pill switches to "over threshold" (yellow)
- "Over threshold" callout appears in the panel with allowed kg, excess kg, charged Rs
- Save still works (over-threshold isn't a hard block)

- [ ] **Step 4: Washing — edit voucher**

From `/rated-vouchers` open any washing voucher in edit mode. Confirm:
- Right panel reflects loaded state immediately
- "Update voucher" button text (instead of "Save voucher")
- Discard button opens confirm modal, then navigates back to a blank `/washing-voucher`

- [ ] **Step 5: Production — input rate hint visible**

Open `/production-voucher`. Pick category → material → lot in an input row. Confirm:
- Rate input pre-fills (existing behavior)
- "↩ from lot" hint visible underneath the Rate cell
- The lot-source detail (e.g., "PV-001 · 2026-05-16 · 320 kg available") visible under the Lot cell

- [ ] **Step 6: Production — mass balance off**

Enter inputs totalling 500 kg, outputs totalling 470 kg, no byproducts, shortage 20 kg → Δ = 10. Confirm:
- Right panel Δ row goes red (`.wp-row-bad`)
- Reason text "Mass is 10.00 kg off" appears in red box above Save
- Save button disabled

- [ ] **Step 7: Production — cost balance off**

Balance the mass but mismatch rates so output cost ≠ input cost. Confirm:
- Right panel Cost Δ row goes red
- Reason text "Cost is Rs N off" appears above Save
- Save button disabled

- [ ] **Step 8: Production — formulation apply**

Pick a formulation, enter batch kg, click Apply. Confirm input + output rows populate from the formula, right panel updates accordingly.

- [ ] **Step 9: Sticky panel behavior**

On both pages: scroll the left column past the bottom of the right panel's content. Confirm the panel stays visible at `top: 18px`. Resize the browser below 1024 px width — panel should wrap under the left column (no sticky).

- [ ] **Step 10: Final build + commit nothing**

```
cd regranbill.client && npx ng build --configuration development
```
Expected: success. No changes to commit in this task.

---

## Final state

After all 7 tasks:
- 6 commits on master branch
- All existing fields preserved
- Two pages restructured to two-column workspace
- Zero backend changes
- No new components, no shared CSS file (acceptable duplication of ~150 lines of wp-* rules)
- No tests added (no working test framework in the project)
