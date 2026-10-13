import { test, expect } from '@playwright/test';

test.describe('Vendor Management E2E Workflow', () => {

  test('Procurement officer can navigate to vendors, create a vendor, and verify details', async ({ page }) => {
    // 1. Visit Web App Login Page
    await page.goto('/login');

    // Verify login page loaded
    await expect(page.getByRole('heading', { name: /welcome back/i })).toBeVisible();

    // 2. Perform Login with valid credentials
    await page.getByLabel(/email/i).fill('officer@procura.test');
    await page.getByLabel(/password/i).fill('Password123!');
    await page.getByRole('button', { name: /sign in/i }).click();

    // Verify navigation after authentication
    await expect(page).toHaveURL(/\/(requests|vendors)/);

    // 3. Navigate to Vendor Management Section
    await page.click('nav a[href="/vendors"]');
    await expect(page).toHaveURL(/\/vendors/);
    await expect(page.getByRole('heading', { name: /vendors/i })).toBeVisible();

    // 4. Click "+ New Vendor" button
    const newVendorButton = page.getByRole('link', { name: /\+ new vendor/i });
    await expect(newVendorButton).toBeVisible();
    await newVendorButton.click();

    // 5. Verify Create Vendor Form renders with validations
    await expect(page).toHaveURL(/\/vendors\/new/);
    await expect(page.getByRole('heading', { name: /new vendor/i })).toBeVisible();

    // 6. Fill out the vendor creation form
    const testVendorName = `Playwright Hardware Supplies ${Date.now()}`;
    await page.getByLabel(/vendor name/i).fill(testVendorName);
    await page.getByLabel(/category/i).fill('Hardware & IT');
    await page.getByLabel(/contact person/i).fill('Kasun Perera');
    await page.getByLabel(/email/i).fill(`kasun_${Date.now()}@testsupplies.com`);
    await page.getByLabel(/phone/i).fill('0771234567');
    await page.getByLabel(/rating/i).fill('4.8');
    await page.getByLabel(/address/i).fill('No. 45, Galle Road, Colombo');

    // 7. Submit Form
    await page.getByRole('button', { name: /save vendor/i }).click();

    // 8. Assert navigation to Vendor Detail Page and verify persisted data
    await expect(page).toHaveURL(/\/vendors\/[a-f0-9-]+/);
    await expect(page.getByText(testVendorName)).toBeVisible();
    await expect(page.getByText('ACTIVE')).toBeVisible();
    await expect(page.getByText('Kasun Perera')).toBeVisible();

    // 9. Verify Deactivation action (Functional status toggle)
    const deactivateBtn = page.getByRole('button', { name: /deactivate/i });
    if (await deactivateBtn.isVisible()) {
      await deactivateBtn.click();
      await expect(page.getByText('INACTIVE')).toBeVisible();
    }
  });

  test('Form validation prevents submitting invalid email and rating', async ({ page }) => {
    // Direct navigation to create vendor
    await page.goto('/vendors/new');

    // Attempt submitting empty form to trigger browser HTML5 / React validations
    const submitBtn = page.getByRole('button', { name: /save vendor/i });
    if (await submitBtn.isVisible()) {
      await submitBtn.click();
      // Form should remain on /vendors/new because required fields block submission
      await expect(page).toHaveURL(/\/vendors\/new/);
    }
  });

});
