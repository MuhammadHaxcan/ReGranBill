const { chromium } = require('playwright');

const BASE = 'http://localhost:50559';
const SCREENSHOT_DIR = 'C:/Users/Claude/Desktop/ReGranBill/ScreenShots';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();

  const consoleMessages = [];
  page.on('console', msg => {
    const text = `[${msg.type()}] ${msg.text()}`;
    consoleMessages.push(text);
    // Print DC-related messages immediately
    if (msg.text().includes('[DC]') || msg.type() === 'error') {
      console.log('  CONSOLE:', text);
    }
  });
  page.on('pageerror', err => {
    console.log('  PAGE ERROR:', err.message);
  });

  try {
    // Login
    console.log('=== Login ===');
    await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
    await page.fill('input[placeholder="Enter username"]', 'admin');
    await page.fill('input[placeholder="Enter password"]', 'Admin123!');
    await page.click('button:has-text("Sign In")');
    await page.waitForURL('**/delivery-challan', { timeout: 10000 });
    console.log('  Logged in');

    // Wait and check DC page
    console.log('\n=== DC Page (waiting 8 seconds) ===');
    await page.waitForTimeout(8000);

    const isLoading = await page.locator('.loading-state').isVisible().catch(() => false);
    console.log('  Loading visible?', isLoading);

    const headerVisible = await page.locator('.invoice-header').isVisible().catch(() => false);
    console.log('  Invoice header visible?', headerVisible);

    await page.screenshot({ path: `${SCREENSHOT_DIR}/debug-dc-page.png`, fullPage: true });

    // Check component state via evaluate
    console.log('\n=== Checking Angular component state ===');
    const state = await page.evaluate(() => {
      const el = document.querySelector('app-delivery-challan');
      if (!el) return { error: 'Component not found in DOM' };
      // Try to access Angular's component instance
      const ng = window.ng;
      if (!ng) return { error: 'Angular devtools not available' };
      try {
        const component = ng.getComponent(el);
        if (!component) return { error: 'Component instance not found via ng.getComponent' };
        return {
          loading: component.loading,
          dcNumber: component.dcNumber,
          productsCount: component.products?.length,
          customersCount: component.customers?.length,
          transportersCount: component.transporters?.length,
          linesCount: component.lines?.length,
        };
      } catch (e) {
        return { error: `ng.getComponent failed: ${e.message}` };
      }
    });
    console.log('  Component state:', JSON.stringify(state, null, 2));

    // Print all console messages
    console.log('\n=== All Console Messages ===');
    consoleMessages.forEach(m => console.log('  ', m));

  } catch (err) {
    console.error('\nFAILED:', err.message);
    console.log('\n=== All Console Messages ===');
    consoleMessages.forEach(m => console.log('  ', m));
    await page.screenshot({ path: `${SCREENSHOT_DIR}/debug-error.png`, fullPage: true });
  } finally {
    await browser.close();
  }
})();
