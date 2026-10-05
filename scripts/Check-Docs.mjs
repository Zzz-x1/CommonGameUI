import { readdir, readFile, stat } from 'node:fs/promises'
import { dirname, resolve, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import assert from 'node:assert/strict'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const site = resolve(root, 'docs/site')
async function markdownFiles(directory) {
  const results = []
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    if (entry.name.startsWith('.') || entry.name === 'node_modules') continue
    const path = resolve(directory, entry.name)
    if (entry.isDirectory()) results.push(...await markdownFiles(path))
    else if (entry.name.endsWith('.md')) results.push(path)
  }
  return results
}
const files = await markdownFiles(site)
const texts = new Map(await Promise.all(files.map(async path => [path, await readFile(path, 'utf8')])))
const code = text => [...text.matchAll(/^```(?:csharp|xml)\r?\n([\s\S]*?)^```/gm)].map(m => m[1].trim())
let pairs = 0
for (const [path, text] of texts) {
  const local = relative(site, path).replaceAll('\\', '/')
  if (local !== 'README.md' && !local.startsWith('en/')) {
    const english = texts.get(resolve(site, 'en', local))
    assert.ok(english, `Missing English page: ${local}`)
    assert.deepEqual(code(text), code(english), `Bilingual code differs: ${local}`)
    pairs++
  }
  if (local.startsWith('en/')) assert.ok(texts.has(resolve(site, local.slice(3))), `Missing Chinese page: ${local}`)
  const prose = text.replace(/^```[^\n]*\n[\s\S]*?^```/gm, '')
  for (const match of prose.matchAll(/!?\[[^\]]*\]\(([^\s)]+)(?:\s+"[^"]*")?\)/g)) {
    const url = match[1]
    if (/^(?:https?:|mailto:|#)/.test(url)) continue
    const target = resolve(dirname(path), decodeURIComponent(url.split('#')[0]))
    assert.ok(target.startsWith(site + '/') || target.startsWith(site + '\\'), `Link escapes site: ${local} → ${url}`)
    assert.ok((await stat(target)).isFile(), `Invalid link: ${local} → ${url}`)
  }
}
for (const engine of ['godot', 'unity']) {
  const source = (await readFile(resolve(root, `docs/examples/${engine}/TutorialHost.cs`), 'utf8')).trim()
  for (const prefix of ['', 'en/']) {
    assert.ok(code(texts.get(resolve(site, `${prefix}getting-started/${engine}.md`))).includes(source), `Tutorial drift: ${prefix}${engine}`)
  }
}
console.log(`Documentation checks passed: ${pairs} bilingual page pairs; tutorial code and local links verified.`)
