# Washing & Production Voucher — Page Redesign

**Date:** 2026-05-16
**Status:** Design approved, ready for implementation planning
**Affects:** `regranbill.client/src/app/pages/washing-voucher/`, `regranbill.client/src/app/pages/production-voucher/`

---

## Context

The Washing Voucher and Production Voucher pages are the two most operationally complex pages in the app. Both involve weight/cost balancing, multi-line tables, and conditional derivations. Office-accountant users (the primary persona — desktop, comfortable with numbers, entering from paper slips) reported the current layouts are "not clear to operate."

Audit of the current pages surfaced concrete clarity problems:

- **Sticky balance bar at the top obscures the table** it's meant to summarize.
- **Wastage / shortage logic is fragmented** — the threshold input is in one section, the explanation card is at the bottom, the running calculation is in the sticky bar.
- **Rate auto-fill is inconsistent** — Washing auto-fills the rate from the selected lot; Production does not, even though both flows pick a lot the same way.
- **Cost-balance vs mass-balance error messages are generic** — when Save is disabled the accountant doesn't know which balance is off.
- **No persistent place for totals + Save action** — both live mid-page or at the bottom of long forms.

The user's hard constraint: **keep every existing field**. The redesign is purely about spatial layout, visual hierarchy, inline guidance, and behavioral consistency — not feature changes.

## Goals

1. Always-visible totals and Save action that never scroll off-screen.
2. The moment a balance / threshold goes off, the gap is visible without scrolling or clicking.
3. Consistent rate behavior across Washing and Production (lot rate auto-fills, editable, hint shown).
4. Fewer "where do I look?" moments — section grouping matches the order an accountant reads a paper slip.

## Non-Goals

- Removing or adding fields.
- Mobile-first redesign (the audit confirmed the breakpoint logic still works; desktop is primary).
- Touching downstream/print/edit-rates pages.
- Changing the backend API surface or DTOs.
- A guided/wizard flow (rejected during brainstorming — accountants want everything visible).

## Design

### Overall layout (both pages)

Two-column workspace at desktop widths (≥ 1024 px):

| | Left column (flexible) | Right column (320 px fixed) |
|---|---|---|
| **Content** | Editable form sections, stacked vertically | Persistent "Live balance" panel |
| **Sticky?** | No — natural scroll | `position: sticky; top: 18px` |
| **Holds the Save button?** | No (Save moves out of footer) | Yes — Save and Discard live at the bottom of the panel |

Below 1024 px the right column wraps under the left column and loses `sticky`; this preserves the current mobile behaviour without extra work, since mobile is not the primary use case.

The existing `.prod-*` design tokens (header card, section card with colored dot, monospace amounts, indigo primary) are reused — the redesign builds on the existing visual language rather than replacing it.

### Washing Voucher — left column structure

1. **Header card** — Voucher title + number + Date + Description (current fields, same layout).
2. **Source card** — All current source fields, but **regrouped into 3 rows**:
   - Row A: Source vendor · Input category
   - Row B: Unwashed material · Source lot
   - Row C: Input weight · Lot rate · **Allowed wastage %** ← moved here from the Outputs header
   The `Lot rate` field shows a small hint underneath: *"↩ from selected lot · editable"*. The `Allowed wastage %` field shows: *"excess charges vendor at lot rate"*.
3. **Washed outputs card** — Existing table (Category / Material / Weight / Amount / remove), with an explicit `<tfoot>` totals row and the derived washed rate in the section header (right-aligned). The current "Output preview" block is removed (redundant — the tfoot totals and the right panel both surface the same numbers).

### Washing Voucher — right panel content

Top-to-bottom, all monospace numbers:

- Status pill (`balanced` / `over threshold`)
- **Mass (kg)**: IN, OUT, WASTE (with %)
- **Excess box** (only renders when over threshold): allowed kg, excess kg, → charged Rs, on a warning-yellow background
- **Cost**: Input, Excess deduction (negative), Net cost
- **Washed rate** (headline, large): Rs / kg
- **Save voucher** (primary button, full width)
- **Discard** (ghost button, full width)

### Production Voucher — left column structure

1. **Header card** — Voucher title + number + Date + Lot number + Description (current fields), with a **Formulation strip embedded at the bottom** of the same card (dashed-border background, less visual weight than a separate section). Contains the formulation picker, batch kg, and Apply button.
2. **Inputs card** — Existing table. Two structural changes:
   - **Category and Material merge into one column** ("Category · Material" with the secondary value in muted color) — cuts horizontal scroll.
   - **Rate auto-fills from the selected lot** with a subtle highlight; user can edit. Matches Washing's behavior. (This is the only behaviour change in the spec.)
   - Explicit `<tfoot>` totals row (Bags / Kg / Cost).
   - Section header shows summary: *"N rows · X kg · Rs Y"*.
3. **Outputs card** — Existing table, with explicit tfoot totals and summary in section header.
4. **"Lumps & loss" card** — Merges Byproducts and Shortage into a single section card with two sub-blocks (separated by a thin divider, shortage sub gets a warm yellow tint to visually mark it as written-off). The "Auto-suggest" button moves to the sub-header and shows the suggested kg inline (e.g., *"↻ auto-suggest 10.00 kg"*) instead of being explained in a hint after the field.

### Production Voucher — right panel content

- Status pill (`balanced` / `near` / `off`)
- **Mass (kg)**: IN, OUT, LUMPS, SHORT, Δ (color-coded — green when within tolerance, yellow `near`, red `off`)
- **Cost (Rs)**: Input, Output, + shortage, Δ (color-coded by `isCostBalanced`)
- **Derived rate** (headline, large): Rs / kg
- **Save voucher** (primary button, full width — disabled with explicit reason text when balance is off, e.g. *"Mass is 1.2 kg off"* or *"Cost is Rs 250 off"*)
- **Discard** (ghost button, full width)

### Behavioural changes (the only ones)

1. **Production input rate auto-fills from the selected lot.** Implementation mirrors the existing washing-voucher `onSelectedLotChanged` pattern (washing-voucher.component.ts:228-241): selecting a lot writes the lot's rate into the rate input. If the user wants a different rate, they edit the rate field after the lot pick — same behavior as washing today.
2. **Save disabled state shows the reason.** Today: button is disabled with a generic tooltip. New: panel shows an inline reason line ("Mass is X kg off" or "Cost is Rs Y off") directly above the Save button.
3. **Washing gets a Discard button alongside Save.** Today Washing's footer is Save-only; the new right panel includes a Discard ghost button (full-width, beneath Save) matching Production for visual symmetry. Discard navigates away the same as the existing Production Discard.

No other behavior, no other endpoint, no other validation rule changes.

### What is explicitly NOT changing

- Field-level validation messages.
- The downstream-usage-banner component above the page (rendered unchanged).
- The cascade order for Source picks (vendor → category → account → lot).
- Mobile breakpoint behavior — the right column wraps below the left at < 1024 px, no special treatment.
- The .prod-* design token system itself.

## Component / file plan

### Files modified

- `regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.html` — rewritten for two-column layout.
- `regranbill.client/src/app/pages/washing-voucher/washing-voucher.component.css` — add two-column grid; restyle/remove sticky balance bar (now lives in panel); add panel styles.
- `regranbill.client/src/app/pages/production-voucher/production-voucher.component.html` — rewritten for two-column layout, merged Category/Material column, merged Byproducts+Shortage card.
- `regranbill.client/src/app/pages/production-voucher/production-voucher.component.css` — same panel + grid changes; restyle column widths after Category/Material merge.
- `regranbill.client/src/app/pages/production-voucher/production-voucher.component.ts` — add `onSelectedLotChanged` rate auto-fill for inputs (copy logic pattern from washing-voucher.component.ts:228-241); add computed property for "Save disabled reason" string.

### Files created

None. No new components — the right-side panel is just markup inside each existing page component (it doesn't justify a shared component until/unless a third page wants it).

### Files unchanged

- All backend code (services, controllers, DTOs, entities).
- All other voucher pages (delivery-challan, purchase-voucher, journal-voucher, cash-voucher, etc.).
- All shared components (searchable-select, downstream-usage-banner, confirm-modal, etc.).

## Testing & verification

End-to-end on the dev server:

1. **Washing — new voucher happy path**: open `/washing-voucher`, fill source vendor → category → unwashed material → lot. Confirm lot rate auto-populates in the rate input. Enter input weight, add 2 output lines. Confirm right panel shows IN/OUT/WASTE updating live; "Status" pill is `balanced` when wastage ≤ allowed %, switches to `over threshold` with the excess box appearing when exceeded. Save → success → form resets.
2. **Washing — over-threshold path**: set allowed wastage to 5%, enter inputs that produce 8% wastage. Confirm excess box (warning yellow) appears in panel showing allowed kg, excess kg, → charged amount. Save still works.
3. **Washing — edit voucher**: open an existing voucher in edit mode. Confirm right panel reflects loaded state immediately; Save button updates instead of creates.
4. **Production — input rate auto-fills**: pick a lot in an input row — rate field populates and shows the "↩ from lot" hint. Manually overwrite — change sticks; reload lot to different one — rate updates again.
5. **Production — mass balance off**: enter inputs totaling 500 kg, outputs totaling 470 kg, no byproducts, shortage 20 kg → Δ = 10. Panel Δ row goes red; Save shows reason text "Mass is 10.00 kg off"; Save button is disabled.
6. **Production — cost balance off**: balance mass but mismatch rates so output cost differs from input cost. Panel Cost Δ row goes red; reason text shows "Cost is Rs N off"; Save disabled.
7. **Production — formulation apply**: pick a formulation, enter batch kg, click Apply. Confirm input/output rows populate from the formulation; right panel updates accordingly.
8. **Build verification**: `dotnet build` and `ng build` both clean. No console warnings beyond pre-existing.

## Risks & mitigations

- **Risk:** Right panel takes 320 px from main content; line-item tables that already scroll horizontally will scroll more.
  **Mitigation:** The Category/Material column merge in Production cuts ~140 px of horizontal scroll, net win. Washing tables are narrower and unaffected.

- **Risk:** Production rate auto-fill might surprise users who currently type rate first before picking a lot.
  **Mitigation:** Match Washing's existing behavior exactly — lot pick sets rate, user can edit after. This is the long-established convention on the Washing page so users are already familiar with it; the change makes Production *consistent* rather than introducing novel behavior.

- **Risk:** Sticky panel on short pages (e.g., empty new voucher) looks like a giant empty box.
  **Mitigation:** Panel content is dense enough at baseline (status, mass section, cost section, rate, save buttons) that even an empty voucher shows ~280 px of content — never feels empty.

- **Risk:** Behavior change (input rate auto-fill on Production) is invisible from this spec to anyone not reading it.
  **Mitigation:** Implementation plan must include a test step confirming both fresh-rate and edit-then-relock-rate scenarios. Note in commit message.
