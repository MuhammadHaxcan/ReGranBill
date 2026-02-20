const { chromium } = require('playwright');

const BASE = 'http://localhost:50559';
const SCREENSHOT_DIR = 'C:/Users/Claude/Desktop/ReGranBill/ScreenShots';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();

  const results = {};
  const jsErrors = [];
  const consoleMessages = [];

  // Capture ALL console messages
  page.on('console', msg => {
    const text = `[${msg.type()}] ${msg.text()}`;
    consoleMessages.push(text);
    if (msg.type() === 'error' || msg.type() === 'warning') {
      jsErrors.push(text);
    }
  });

  // Capture page errors (uncaught exceptions)
  page.on('pageerror', err => {
    jsErrors.push(`[pageerror] ${err.message}`);
  });

  // Track failed requests
  const failedRequests = [];
  page.on('requestfailed', req => {
    failedRequests.push(`${req.method()} ${req.url()} - ${req.failure()?.errorText}`);
  });

  // Log API response bodies
  page.on('response', async resp => {
    if (resp.url().includes('/api/')) {
      const method = resp.request().method();
      const status = resp.status();
      const url = resp.url().replace(BASE, '');
      try {
        const body = await resp.text();
        const preview = body.length > 300 ? body.substring(0, 300) + '...' : body;
        console.log(`  [API] ${status} ${method} ${url} => ${preview}`);
      } catch {}
    }
  });

  try {
    // ==================== 1. LOGIN ====================
    console.log('\n=== STEP 1: Login ===');
    await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
    await page.fill('input[placeholder="Enter username"]', 'admin');
    await page.fill('input[placeholder="Enter password"]', 'Admin123!');
    await page.click('button:has-text("Sign In")');
    await page.waitForURL('**/delivery-challan', { timeout: 10000 });
    console.log('  OK - logged in, redirected to delivery-challan');
    results.login = true;
    await page.screenshot({ path: `${SCREENSHOT_DIR}/01-after-login.png`, fullPage: true });

    // ==================== 2. DELIVERY CHALLAN ====================
    console.log('\n=== STEP 2: Check DC page ===');
    // Wait for API calls to complete
    await page.waitForTimeout(3000);

    // Check loading state
    const isLoading = await page.locator('.loading-state').isVisible().catch(() => false);
    console.log('  Loading visible?', isLoading);

    // Check if invoice-header is visible
    const headerVisible = await page.locator('.invoice-header').isVisible().catch(() => false);
    console.log('  Invoice header visible?', headerVisible);

    // Dump all console errors
    if (jsErrors.length) {
      console.log('\n  === JS ERRORS ===');
      jsErrors.forEach(e => console.log('  ', e));
    }

    // Dump failed requests
    if (failedRequests.length) {
      console.log('\n  === FAILED REQUESTS ===');
      failedRequests.forEach(r => console.log('  ', r));
    }

    await page.screenshot({ path: `${SCREENSHOT_DIR}/02-dc-page.png`, fullPage: true });

    // If still loading, try navigating fresh
    if (isLoading) {
      console.log('\n  Page still loading - trying fresh navigation...');
      await page.goto(`${BASE}/delivery-challan`, { waitUntil: 'networkidle' });
      await page.waitForTimeout(5000);

      const isLoading2 = await page.locator('.loading-state').isVisible().catch(() => false);
      console.log('  Loading visible after fresh nav?', isLoading2);

      const headerVisible2 = await page.locator('.invoice-header').isVisible().catch(() => false);
      console.log('  Invoice header visible after fresh nav?', headerVisible2);

      await page.screenshot({ path: `${SCREENSHOT_DIR}/03-dc-page-fresh.png`, fullPage: true });

      // Print any new errors
      if (jsErrors.length) {
        console.log('\n  === ALL JS ERRORS ===');
        jsErrors.forEach(e => console.log('  ', e));
      }

      // Get the full page HTML for debugging
      const bodyHtml = await page.locator('body').innerHTML().catch(() => 'BODY NOT FOUND');
      console.log('\n  Body HTML (first 1000 chars):');
      console.log(bodyHtml.substring(0, 1000));
    }

    const dcNumber = await page.locator('.invoice-number').textContent().catch(() => 'NOT FOUND');
    console.log('  DC Number:', dcNumber?.trim());
    results.dcNumber = dcNumber?.trim();

    // ==================== 3. CUSTOMER DROPDOWN ====================
    if (!isLoading || !(await page.locator('.loading-state').isVisible().catch(() => false))) {
      console.log('\n=== STEP 3: Customer dropdown ===');
      const custTrigger = page.locator('.form-group:has(.form-label:has-text("CUSTOMER")) .ss-trigger');
      await custTrigger.click();
      await page.waitForTimeout(800);
      const custOpts = await page.locator('.ss-option .ss-option-label').allTextContents();
      console.log('  Options:', custOpts);
      results.hasAkhtarPlastics = custOpts.some(o => o.includes('Akhtar'));
      results.hasBilalIndustries = custOpts.some(o => o.includes('Bilal'));
      results.hasDeltaPackaging = custOpts.some(o => o.includes('Delta'));
      await page.screenshot({ path: `${SCREENSHOT_DIR}/04-customer-dropdown.png`, fullPage: true });
      await page.keyboard.press('Escape');
      await page.waitForTimeout(300);

      // ==================== 4. PRODUCT DROPDOWN ====================
      console.log('\n=== STEP 4: Product dropdown ===');
      const prodTrigger = page.locator('.lines-table tbody tr').first().locator('.ss-trigger').first();
      await prodTrigger.click();
      await page.waitForTimeout(800);
      const prodOpts = await page.locator('.ss-option .ss-option-label').allTextContents();
      console.log('  Options:', prodOpts);
      results.hasHDPE = prodOpts.some(o => o.includes('HDPE'));
      results.hasPP125 = prodOpts.some(o => o.includes('PP-125'));
      results.hasLDPE = prodOpts.some(o => o.includes('LDPE'));
      await page.screenshot({ path: `${SCREENSHOT_DIR}/05-product-dropdown.png`, fullPage: true });
      await page.keyboard.press('Escape');
      await page.waitForTimeout(300);
    }

    // ==================== 5. METADATA PAGE ====================
    console.log('\n=== STEP 5: Metadata page ===');
    await page.click('a[routerLink="/metadata"]');
    await page.waitForURL('**/metadata');
    await page.waitForTimeout(3000);
    await page.screenshot({ path: `${SCREENSHOT_DIR}/06-metadata-page.png`, fullPage: true });

    const catItems = await page.locator('.list-item .list-name').allTextContents();
    console.log('  Categories in DOM:', catItems);
    results.seededCategories = catItems.length;

    // ==================== 6. ACCOUNTS TAB ====================
    console.log('\n=== STEP 6: Accounts tab ===');
    await page.click('button.tab:has-text("Accounts")');
    await page.waitForTimeout(2000);
    const acctRows = await page.locator('.acct-table tbody tr').count();
    console.log('  Accounts in DOM:', acctRows);
    results.seededAccounts = acctRows;
    await page.screenshot({ path: `${SCREENSHOT_DIR}/07-accounts-tab.png`, fullPage: true });

    // ==================== 7. CREATE CATEGORY ====================
    console.log('\n=== STEP 7: Create category ===');
    await page.click('button.tab:has-text("Categories")');
    await page.waitForTimeout(500);
    await page.click('button:has-text("+ Add Category")');
    await page.waitForTimeout(300);
    await page.fill('input[placeholder="Category name..."]', 'E2E Test Cat');
    await page.click('.inline-form button:has-text("Add")');
    await page.waitForTimeout(2000);
    const catsAfter = await page.locator('.list-item .list-name').allTextContents();
    console.log('  Categories after add:', catsAfter);
    results.categoryCreated = catsAfter.includes('E2E Test Cat');

    // ==================== 8. CREATE CUSTOMER ====================
    console.log('\n=== STEP 8: Create customer ===');
    await page.click('button.tab:has-text("Accounts")');
    await page.waitForTimeout(1000);
    await page.click('button:has-text("+ Add Account")');
    await page.waitForSelector('.modal', { state: 'visible', timeout: 3000 });
    await page.waitForTimeout(500);

    await page.fill('.modal input[placeholder="Enter account name..."]', 'E2E Customer Ltd');
    // Select category
    const catTrigger = page.locator('.modal app-searchable-select .ss-trigger').first();
    await catTrigger.click();
    await page.waitForTimeout(500);
    await page.locator('.ss-option').first().click();
    await page.waitForTimeout(500);
    // Select Party type
    await page.click('.type-btn:has-text("Party")');
    await page.waitForTimeout(500);
    await page.click('.role-btn:has-text("Customer")');
    await page.waitForTimeout(200);
    // Fill fields
    if (await page.locator('input[placeholder="Full name..."]').count() > 0) {
      await page.fill('input[placeholder="Full name..."]', 'Test Contact');
      await page.fill('input[placeholder*="0300"]', '0300-1111111');
      await page.fill('input[placeholder="City..."]', 'Test City');
    }
    await page.screenshot({ path: `${SCREENSHOT_DIR}/08-new-customer-form.png`, fullPage: true });
    await page.click('.modal button:has-text("Create Account")');
    await page.waitForTimeout(2000);

    // ==================== 9. VERIFY ON DC PAGE ====================
    console.log('\n=== STEP 9: Verify customer on DC page ===');
    await page.click('a[routerLink="/delivery-challan"]');
    await page.waitForURL('**/delivery-challan');
    await page.waitForTimeout(5000);

    const loadingNow = await page.locator('.loading-state').isVisible().catch(() => false);
    console.log('  DC page loading?', loadingNow);

    if (!loadingNow) {
      const custTrigger2 = page.locator('.form-group:has(.form-label:has-text("CUSTOMER")) .ss-trigger');
      await custTrigger2.click();
      await page.waitForTimeout(800);
      const custOpts2 = await page.locator('.ss-option .ss-option-label').allTextContents();
      console.log('  Customer options now:', custOpts2);
      results.hasE2ECustomer = custOpts2.some(o => o.includes('E2E Customer'));
      await page.screenshot({ path: `${SCREENSHOT_DIR}/09-dc-after-customer-add.png`, fullPage: true });
    }

    // ==================== SUMMARY ====================
    console.log('\n========================================');
    console.log('       E2E TEST RESULTS');
    console.log('========================================');
    const p = (v) => v ? 'PASS' : 'FAIL';
    console.log(`  Login .................. ${p(results.login)}`);
    console.log(`  DC number .............. ${p(results.dcNumber?.startsWith('DC-'))} (${results.dcNumber})`);
    console.log(`  Cust: Akhtar Plastics .. ${p(results.hasAkhtarPlastics)}`);
    console.log(`  Cust: Bilal Ind ........ ${p(results.hasBilalIndustries)}`);
    console.log(`  Cust: Delta (Both) ..... ${p(results.hasDeltaPackaging)}`);
    console.log(`  Prod: HDPE Blue Drum ... ${p(results.hasHDPE)}`);
    console.log(`  Prod: PP-125 Natural ... ${p(results.hasPP125)}`);
    console.log(`  Prod: LDPE Film Grade .. ${p(results.hasLDPE)}`);
    console.log(`  Seeded categories ...... ${p(results.seededCategories >= 6)} (${results.seededCategories})`);
    console.log(`  Seeded accounts ........ ${p(results.seededAccounts >= 14)} (${results.seededAccounts})`);
    console.log(`  Create category ........ ${p(results.categoryCreated)}`);
    console.log(`  Cust: E2E Customer ..... ${p(results.hasE2ECustomer)}`);
    console.log('========================================');

    // Print all console messages for debugging
    console.log('\n=== ALL CONSOLE MESSAGES ===');
    consoleMessages.forEach(m => console.log('  ', m));

  } catch (err) {
    console.error('\nTEST FAILED:', err.message);
    if (jsErrors.length) {
      console.log('\n  === JS ERRORS ===');
      jsErrors.forEach(e => console.log('  ', e));
    }
    console.log('\n=== ALL CONSOLE MESSAGES ===');
    consoleMessages.forEach(m => console.log('  ', m));
    await page.screenshot({ path: `${SCREENSHOT_DIR}/error-screenshot.png`, fullPage: true });
  } finally {
    await browser.close();
  }
})();
