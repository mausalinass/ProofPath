import { test, expect } from '@playwright/test'

test('Collage scroll, floats, responsive pages and reduced motion', async ({ page }, testInfo) => {
  await page.goto('/')
  const path = page.locator('.progress-path')
  await expect(path).toHaveAttribute('data-progress', '0.000')
  await page.waitForTimeout(250)
  await expect(path).toHaveAttribute('data-progress', '0.000')
  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight))
  await expect(path).toHaveAttribute('data-progress', '1.000')
  await page.evaluate(() => window.scrollTo(0, 0))
  await expect(path).toHaveAttribute('data-progress', '0.000')
  await page.getByRole('button', { name: 'Pausar movimiento' }).click()
  await expect(page.locator('.pp-resume').first()).toHaveCSS('animation-play-state', 'paused')
  await page.getByRole('button', { name: 'Reanudar movimiento' }).click()
  await expect(page.locator('.pp-resume').first()).toHaveCSS('animation-play-state', 'running')
  for (const width of [1440, 768, 390, 320]) {
    await page.setViewportSize({ width, height: 1000 })
    for (const route of ['/', '/login', '/signup']) {
      await page.goto(route)
      await expect(page.locator('h1')).toBeVisible()
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy()
      if (width === 1440 || width === 390) await page.screenshot({ path: testInfo.outputPath(`${route.replaceAll('/', '') || 'landing'}-${width}.png`), fullPage: true })
    }
  }
  await page.goto('/')
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await expect(page.locator('.pp-resume').first()).toHaveCSS('animation-name', 'none')
  await page.keyboard.press('Tab')
  await expect(page.getByRole('link', { name: 'Saltar al contenido' })).toBeFocused()
})

test('real dossier, SPA navigation, privacy and matcher preserve sources', async ({ page, request }, testInfo) => {
  const email = `collage-${Date.now()}@example.test`
  await page.goto('/signup')
  await page.getByLabel('First name', { exact: true }).fill('Alex')
  await page.getByLabel('Email', { exact: true }).fill(email)
  await page.getByLabel('Password', { exact: true }).fill('Browser-only.P4ssword!')
  await page.getByLabel('Confirm password', { exact: true }).fill('Browser-only.P4ssword!')
  await page.getByLabel(/I have read and accept/).check()
  await page.getByRole('button', { name: 'Create account' }).click()
  await expect(page).toHaveURL(/\/sources$/)
  await page.screenshot({ path: testInfo.outputPath('sources.png'), fullPage: true })
  expect((await request.post('http://localhost:5002/fixtures/sprints-3-5', { data: { email } })).ok()).toBeTruthy()
  await page.goto('/home')
  await expect(page.getByText('Hola, Alex.')).toBeVisible()
  await expect(page.getByText('1 habilidades con señales activas')).toBeVisible()
  await page.locator('.evidence-row summary').filter({ hasText: 'C#' }).click()
  await expect(page.getByText(/Confianza de extracción: 100%/)).toBeVisible()
  // A sentinel proves the shell does not reload the document when navigating.
  await page.evaluate(() => { document.documentElement.dataset.spa = 'preserved' })
  await page.getByRole('link', { name: 'Privacidad', exact: true }).click()
  await expect(page).toHaveURL(/\/settings$/)
  await expect(page.getByText('Conectado · proofpath-fixture')).toBeVisible()
  await page.getByRole('button', { name: 'Revocar acceso' }).click()
  await expect(page.getByRole('button', { name: 'Confirmar revocación' })).toBeVisible()
  await page.getByRole('button', { name: 'Conservar conexión' }).click()
  await expect(page.getByText('Conectado · proofpath-fixture')).toBeVisible()
  await page.getByRole('button', { name: 'Delete my account' }).click()
  await expect(page.getByLabel('Current password')).toHaveAttribute('required', '')
  await page.getByRole('button', { name: 'Cancel', exact: true }).click()
  await expect(page.getByLabel('Current password')).toHaveCount(0)
  expect(await page.evaluate(() => document.documentElement.dataset.spa)).toBe('preserved')
  for (const width of [1440, 768, 390, 320]) {
    await page.setViewportSize({ width, height: 1000 })
    for (const route of ['/home', '/sources', '/jobs', '/settings']) {
      await page.goto(route)
      await expect(page.locator('h1')).toBeVisible()
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy()
      if (route === '/jobs') {
        await page.getByText('Add an opportunity', { exact: true }).click()
        await expect(page.locator('.snapshot-pair')).toBeVisible()
      }
      if (width === 1440 || width === 390) await page.screenshot({ path: testInfo.outputPath(`${route.slice(1)}-${width}.png`), fullPage: true })
    }
  }
})
