import { test, expect } from '@playwright/test'
// Minimal text PDF. All contents are synthetic; the test-only Worker host uses a fixture provider.
function pdf(): Buffer {
  const content = 'BT /F1 12 Tf 40 700 Td (Engineer at Acme. Built React applications.) Tj ET'
  const objects = ['<< /Type /Catalog /Pages 2 0 R >>', '<< /Type /Pages /Kids [3 0 R] /Count 1 >>', '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>', '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>', `<< /Length ${Buffer.byteLength(content)} >>\nstream\n${content}\nendstream`]
  let output = '%PDF-1.4\n'; const offsets = [0]
  objects.forEach((object, index) => { offsets.push(Buffer.byteLength(output)); output += `${index + 1} 0 obj\n${object}\nendobj\n` })
  const xref = Buffer.byteLength(output)
  output += `xref\n0 6\n0000000000 65535 f \n${offsets.slice(1).map(offset => `${String(offset).padStart(10, '0')} 00000 n \n`).join('')}trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF`
  return Buffer.from(output)
}
test('résumé upload, Worker extraction, correction, confirmation and account deletion', async ({ page }) => {
  await page.goto('/signup')
  await page.getByLabel('First name').fill('Ada')
  await page.getByLabel('Email', { exact: true }).fill(`resume-${Date.now()}@example.test`)
  await page.getByLabel('Password', { exact: true }).fill('Browser-only.P4ssword!')
  await page.getByRole('button', { name: 'Create account' }).click()
  await expect(page.getByRole('heading', { name: 'Welcome, Ada.' })).toBeVisible()
  await page.goto('/onboarding/resume')
  await page.getByLabel('Résumé file').setInputFiles({ name: 'resume.pdf', mimeType: 'application/pdf', buffer: pdf() })
  await page.getByRole('button', { name: 'Upload résumé', exact: true }).click()
  await expect(page.getByLabel('Name / role / degree')).toHaveValue('Engineer', { timeout: 30000 })
  await page.getByLabel('Name / role / degree').fill('Software engineer')
  await expect(page.getByRole('button', { name: 'Confirm résumé facts' })).toBeDisabled()
  await page.getByRole('button', { name: 'Save corrections' }).click()
  await expect(page.getByRole('checkbox')).toBeEnabled()
  await page.getByRole('checkbox').check()
  await page.getByRole('button', { name: 'Confirm résumé facts' }).click()
  await expect(page.getByText('This résumé supplies your active facts.')).toBeVisible()
  await page.reload()
  await expect(page.getByLabel('Name / role / degree')).toHaveValue('Software engineer')
  await expect(page.getByLabel('Name / role / degree')).toBeDisabled()
  await page.getByRole('link', { name: 'Settings', exact: true }).click()
  await page.getByRole('button', { name: 'Delete my account' }).click()
  await page.getByLabel('Current password').fill('Browser-only.P4ssword!')
  await page.getByRole('button', { name: 'Permanently delete account' }).click()
  await expect(page.getByRole('heading', { name: 'Welcome back.' })).toBeVisible()
})
