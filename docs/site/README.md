# 文档维护 / Documentation maintenance

[中文入口](index.md) · [English](en/index.md)

## 本地构建 / Local build

Node.js 22+。从 docs/site 执行：

```sh
npm ci
npm run docs:dev
npm run docs:build
npm run docs:preview
```

构建先检查双语页面对应、代码一致性和内部文件链接，再生成站点。dist、cache 和 node_modules 不入 Git。中文首页使用 index.md，英文镜像 en；新增页面同时更新两套正文与 .vitepress/config.mts 导航。正文使用相对 .md 链接，在 GitHub 也能阅读。教程全文代码须与 docs/examples 内可编译文件一致。

Build checks bilingual page parity, code equality and internal file links before rendering. Do not commit dist/cache/node_modules. Chinese starts at index.md, English mirrors under en. Update both texts and config navigation together. Relative .md links also work in GitHub. Tutorial code must match compilable docs/examples files.

## 示例与浏览器验证 / Example and browser checks

从仓库根目录 / From the repository root:

```powershell
dotnet run --project docs/examples/core/CoreExamples.csproj
dotnet build docs/examples/godot/Tutorial.csproj
# Use your Godot .NET executable; keep both invocations headless.
godot --headless --path docs/examples/godot --editor --import --quit
godot --headless --path docs/examples/godot res://tests/Smoke.tscn
```

Godot 项目无玩家存档，自测不进入普通教程菜单；测试通过后直接退出。测试代码中的断言与异常处理仅用于验证。Unity TutorialHost 可用安装版本的引擎程序集与核心 DLL 离线编译；它需要 Unity API，不能作为普通 .NET 无依赖项目编译。

The Godot project has no player save. Smoke tests run separately and exit on completion; assertions/catches are test-only. Compile Unity TutorialHost offline with the installed engine assemblies and core DLL; it requires Unity APIs, not a plain standalone .NET project.

从 docs/site 执行 / From docs/site:

```sh
npx playwright install chromium
npm run docs:preview
# In a second terminal, while preview is running:
npm run docs:test:browser
```

DOCS_TEST_URL 可设置完整子路径预览 URL；DOCS_BROWSER_PATH 可显式指定已安装 Chromium。浏览器检查使用独立 headless 进程，不操作用户浏览器。截图位于忽略的 artifacts/docs-browser。

Set DOCS_TEST_URL to a full subpath preview URL or DOCS_BROWSER_PATH to an installed Chromium executable. Checks use an independent headless browser without controlling user windows. Screenshots are ignored under artifacts/docs-browser.

## GitHub Pages

仓库当前未配置远程；此配置不会自动推送或发布。用户将仓库发布到 GitHub 后，在 Settings → Pages → Source 选择 GitHub Actions，再在 Actions 的 Documentation 工作流手动 Run workflow，选择要发布的提交所在分支。PR 只构建检查，不部署；没有 push 自动部署。

No remote is configured. Nothing is pushed or deployed automatically. After publishing the repository yourself, choose GitHub Actions under Settings → Pages → Source. Manually run the Documentation workflow on the desired branch. Pull requests build only; pushes never trigger deployment.

configure-pages 返回 base_path，构建通过 DOCS_BASE 使用它，不写死仓库名；根域名和项目子路径均支持。需要仓库允许 Actions 和 Pages，环境 github-pages 的分支规则须允许选择的分支。发布成功的 URL 从 deploy-pages 输出取得，本地不伪造地址。

configure-pages supplies base_path through DOCS_BASE, supporting root domains and project subpaths without hardcoded repository names. Enable Actions/Pages and allow the chosen branch in the github-pages environment. Use deploy-pages output for the actual URL.

PowerShell 本地验证子路径：

```powershell
$env:DOCS_BASE = '/UI-Framework/'
npm run docs:build
npm run docs:preview
Remove-Item Env:DOCS_BASE
```

Node 只用于文档站点，不成为框架运行依赖。文档修改不改变框架 API；内部决策仍集中于 docs/design-decisions.md。Source truth remains framework code/tests, not historical design candidates. Node is a documentation dependency only.

参考 / References: [VitePress deploy](https://vitepress.dev/guide/deploy), [i18n](https://vitepress.dev/guide/i18n), [search](https://vitepress.dev/reference/default-theme-search), [Netick structure](https://netick.net/docs/2/).
