# 文档维护 / Documentation maintenance

[中文](index.md) · [English](en/index.md)

## 本地预览 / Local preview

Node.js 22+，在 docs/site 执行 / Run in docs/site:

```sh
npm ci
npm run docs:dev
```

构建与预览发布结果 / Build and preview production output:

```sh
npm run docs:build
npm run docs:preview
```

## 编辑规则 / Editing

- 中文页面与 en 镜像同时更新；导航在 .vitepress/config.mts。
- 使用相对 .md 链接；教程源码与 docs/examples 保持一致。
- dist、cache、node_modules 和截图不入 Git。

Update Chinese/en pages together and maintain config navigation. Use relative .md links, keep tutorial code in sync with docs/examples, and exclude build/cache outputs.

## 验证 / Checks

从仓库根执行 / From the repository root:

```powershell
dotnet run --project docs/examples/core/CoreExamples.csproj
dotnet build docs/examples/godot/Tutorial.csproj
godot --headless --path docs/examples/godot --editor --import --quit
godot --headless --path docs/examples/godot res://tests/Smoke.tscn
```

将 godot 替换为自己的 .NET 引擎路径。Unity 示例用安装版本的引擎程序集离线编译。

Use your Godot .NET executable. Compile Unity examples offline against installed engine assemblies.

浏览器检查：保持 preview 运行，在 docs/site 的另一个终端执行 / With preview running, use another terminal in docs/site:

```sh
npx playwright install chromium
npm run docs:test:browser
```

DOCS_TEST_URL 指定子路径预览地址，DOCS_BROWSER_PATH 指定已有 Chromium；检查以 headless 运行，截图输出到 artifacts/docs-browser。

Set DOCS_TEST_URL for a subpath or DOCS_BROWSER_PATH for an existing Chromium. Checks are headless; screenshots go to artifacts/docs-browser.

## GitHub Pages 发布 / Publish

1. 将仓库发布到 GitHub，启用 Actions。
2. Settings → Pages → Source 选择 GitHub Actions。
3. 手动运行 Documentation 工作流，选择发布分支。
4. 在部署结果中取得站点 URL。

Enable Actions, set Pages Source to GitHub Actions, run Documentation manually on the desired branch, and use its deployment URL.

PR 只构建；手动运行才部署。base 由 configure-pages 提供，github-pages 环境须允许目标分支。

PRs build only; manual runs deploy. configure-pages supplies base; the github-pages environment must allow the branch.

[VitePress deployment](https://vitepress.dev/guide/deploy) · [Internationalization](https://vitepress.dev/guide/i18n)
