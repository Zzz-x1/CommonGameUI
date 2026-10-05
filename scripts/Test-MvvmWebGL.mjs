import { createRequire } from 'node:module'
import assert from 'node:assert/strict'
const { chromium } = createRequire(new URL('../docs/site/package.json', import.meta.url))('playwright')
const browser = await chromium.launch({ headless: true, args: ['--enable-unsafe-swiftshader'] })
try {
  const page = await browser.newPage()
  const errors = []
  page.on('pageerror', error => errors.push(error.message))
  await page.goto(process.env.MVVM_WEBGL_URL || 'http://127.0.0.1:4385/')
  await page.waitForFunction(() => window.__mvvmResult, null, { timeout: 120000 })
  const result = await page.evaluate(() => window.__mvvmResult)
  assert.ok(result.passed >= 14, JSON.stringify(result))
  assert.deepEqual(errors, [])
  console.log(`WebGL IL2CPP high stripping: ${result.passed} checks passed.`)
} finally { await browser.close() }
