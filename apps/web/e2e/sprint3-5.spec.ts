import { test, expect } from '@playwright/test'

test('GitHub evidence, requirement confirmation and deterministic matching compose end to end', async ({ page, request }) => {
  const email = `sprint3-5-${Date.now()}@example.test`
  await page.goto('/signup')
  await page.getByLabel('First name').fill('Ada')
  await page.getByLabel('Email', { exact: true }).fill(email)
  await page.getByLabel('Password', { exact: true }).fill('Browser-only.P4ssword!')
  await page.getByRole('button', { name: 'Create account' }).click()
  await expect(page.getByRole('heading', { name: 'Welcome, Ada.' })).toBeVisible()

  const seeded = await request.post('http://localhost:5002/fixtures/sprints-3-5', { data: { email } })
  expect(seeded.ok()).toBeTruthy()

  await page.goto('/onboarding/github')
  await expect(page.getByRole('heading', { name: 'Evidence from selected code snapshots.' })).toBeVisible()
  await expect(page.getByText('proofpath-fixture/evidence-api', { exact: true })).toBeVisible()
  await expect(page.getByText('Implemented an ASP.NET Core API with authenticated endpoints.')).toBeVisible()
  await expect(page.getByText(/01234567 · src\/Program.cs/)).toBeVisible()

  await page.goto('/jobs')
  await page.getByRole('button', { name: /Backend Engineer/ }).click()
  await expect(page.getByRole('heading', { name: 'Technical skills' })).toBeVisible()
  await page.getByLabel('I reviewed the source wording, alternatives, exclusions, and unresolved skills.').check()
  await page.getByRole('button', { name: 'Confirm requirement set' }).click()
  await expect(page.getByText('This requirement set is confirmed and immutable.')).toBeVisible()

  await page.getByRole('button', { name: 'Calculate match' }).click()
  await expect(page.getByRole('heading', { name: 'Candidate fit' })).toBeVisible()
  await expect(page.getByText('100%', { exact: true }).first()).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Requirement trace' })).toBeVisible()
  await page.locator('details.match-requirement summary').filter({ hasText: 'C# is required' }).click()
  await expect(page.getByText(/src\/Program.cs · Strong/)).toBeVisible()
  await expect(page.getByText(/1 immutable result stored/)).toBeVisible()
})
