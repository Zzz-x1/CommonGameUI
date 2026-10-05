import { readdir, readFile, stat } from 'node:fs/promises'
import { dirname, resolve, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createRequire } from 'node:module'
import { pathToFileURL } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const site = resolve(root, 'docs/site')
const requireDocs = createRequire(resolve(site, 'package.json'))
const { default: GithubSlugger } = await import(pathToFileURL(requireDocs.resolve('github-slugger')).href)
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
const siteFiles = await markdownFiles(site)
const tracked = execFileSync('git', ['ls-files', '--', '*.md'], { cwd: root, encoding: 'utf8' }).trim().split(/\r?\n/).map(path => resolve(root, path))
const files = [...new Set([...siteFiles, ...tracked])]
const texts = new Map(await Promise.all(files.map(async path => [path, await readFile(path, 'utf8')])))
const code = text => [...text.matchAll(/^```(?:csharp|xml)\r?\n([\s\S]*?)^```/gm)].map(m => m[1].trim())
let pairs = 0
const stripFences = text => text.replace(/^```[^\n]*\n[\s\S]*?^```/gm, '')
const anchors = new Map()
for (const [path, text] of texts) {
  const slugger = new GithubSlugger()
  const prose = stripFences(text)
  const ids = new Set([...prose.matchAll(/^#{1,6}\s+(.+?)\s*#*\s*$/gm)].map(match => slugger.slug(match[1])))
  for (const match of prose.matchAll(/\bid="([^"]+)"/g)) ids.add(match[1])
  anchors.set(path, ids)
}
for (const [path, text] of texts) {
  const local = relative(site, path).replaceAll('\\', '/')
  const inSite = siteFiles.includes(path)
  if (inSite && local !== 'README.md' && !local.startsWith('en/')) {
    const english = texts.get(resolve(site, 'en', local))
    assert.ok(english, `Missing English page: ${local}`)
    assert.deepEqual(code(text), code(english), `Bilingual code differs: ${local}`)
    pairs++
  }
  if (inSite && local.startsWith('en/')) assert.ok(texts.has(resolve(site, local.slice(3))), `Missing Chinese page: ${local}`)
  const prose = stripFences(text)
  for (const match of prose.matchAll(/!?\[[^\]]*\]\(([^\s)]+)(?:\s+"[^"]*")?\)/g)) {
    const url = match[1]
    if (/^[a-z][a-z\d+.-]*:/i.test(url)) continue
    const [file, fragment] = url.split('#')
    const target = file ? resolve(dirname(path), decodeURIComponent(file)) : path
    const boundary = inSite ? site : root
    assert.ok(target.startsWith(boundary + '/') || target.startsWith(boundary + '\\'), `Link escapes documentation: ${local} → ${url}`)
    assert.ok((await stat(target)).isFile(), `Invalid link: ${local} → ${url}`)
    if (fragment) assert.ok(anchors.get(target)?.has(decodeURIComponent(fragment)), `Invalid anchor: ${relative(root, path)} → ${url}`)
  }
}
for (const engine of ['godot', 'unity']) {
  const source = (await readFile(resolve(root, `docs/examples/${engine}/TutorialHost.cs`), 'utf8')).trim()
  for (const prefix of ['', 'en/']) {
    assert.ok(code(texts.get(resolve(site, `${prefix}getting-started/${engine}.md`))).includes(source), `Tutorial drift: ${prefix}${engine}`)
  }
}
console.log(`Documentation checks passed: ${files.length} repository Markdown files, ${pairs} bilingual page pairs; tutorial code, local links and heading anchors verified.`)
