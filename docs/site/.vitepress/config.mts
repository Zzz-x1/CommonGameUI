import { defineConfig } from 'vitepress'

const groups = [
  ['Getting Started', 'getting-started', [
    ['index', '概览与安装', 'Overview and installation'],
    ['godot', 'Godot 入门', 'Godot tutorial'],
    ['unity', 'Unity 入门', 'Unity tutorial'],
    ['next-steps', '下一步', 'Next steps']]],
  ['Manual', 'manual', [
    ['concepts', '概念与职责', 'Concepts and responsibilities'],
    ['hierarchy', '层级与挂载', 'Hierarchy and mounts'],
    ['nodes-and-parameters', '节点、参数与依赖', 'Nodes, parameters and dependencies'],
    ['panels', 'Panel：独立窗口', 'Panel: independent windows'],
    ['stacks', 'Stack：窗口历史', 'Stack: window history'],
    ['tabs', 'Tab：内嵌分类', 'Tab: embedded categories'],
    ['modals-and-dialogs', 'Modal：模态与结果', 'Modal: modals and results'],
    ['lifecycle', '生命周期', 'Lifecycle'],
    ['transitions', '转场与条件分支', 'Transitions and conditions'],
    ['timeline-animation', '时间轴与自定义动画', 'Timelines and custom animation'],
    ['resources', '资源管理', 'Resources'],
    ['input-and-focus', '输入与焦点', 'Input and focus'],
    ['async-and-errors', '异步、取消与错误', 'Async, cancellation and errors'],
    ['source-generator', 'Source Generator', 'Source Generator'],
    ['mvvm', 'MVVM 入门', 'Getting started with MVVM'],
    ['mvvm-editor', 'MVVM 编辑器', 'MVVM editor']]],
  ['Integrations', 'integrations', [
    ['godot', 'Godot 适配', 'Godot integration'],
    ['unity', 'Unity 适配', 'Unity integration'],
    ['custom-engine', '自定义引擎', 'Custom engines']]],
  ['Samples', 'samples', [
    ['index', '游戏客户端 Demo', 'Game client demos'],
    ['game-ui-recipes', '游戏 UI 场景', 'Game UI recipes']]],
  ['API Reference', 'api', [
    ['index', 'API 索引', 'API index'],
    ['core', 'Core', 'Core'],
    ['godot', 'Godot', 'Godot'],
    ['unity', 'Unity', 'Unity'],
    ['mvvm', 'MVVM', 'MVVM']]]
] as const

function sidebar(en: boolean) {
  const prefix = en ? '/en/' : '/'
  return [
    ...groups.map(([text, folder, entries]) => ({
      text, collapsed: false,
      items: entries.map(([file, zh, english]) => ({
        text: en ? english : zh, link: `${prefix}${folder}/${file === 'index' ? '' : file}`
      }))
    })),
    { text: en ? 'Troubleshooting' : '常见问题', link: `${prefix}troubleshooting` }
  ]
}

export default defineConfig({
  title: 'UIFramework',
  description: 'Renderer-independent C# UI management / 渲染无关的 C# UI 管理框架',
  base: process.env.DOCS_BASE || '/',
  cleanUrls: false,
  locales: {
    root: {
      label: '简体中文', lang: 'zh-CN',
      themeConfig: {
        nav: [{ text: '入门', link: '/getting-started/' }, { text: 'Manual', link: '/manual/concepts' }, { text: 'API', link: '/api/' }],
        sidebar: sidebar(false),
        outline: { label: '本页目录', level: [2, 3] },
        docFooter: { prev: '上一篇', next: '下一篇' },
        sidebarMenuLabel: '目录', returnToTopLabel: '返回顶部', langMenuLabel: '语言',
        darkModeSwitchLabel: '主题'
      }
    },
    en: {
      label: 'English', lang: 'en',
      themeConfig: {
        nav: [{ text: 'Getting Started', link: '/en/getting-started/' }, { text: 'Manual', link: '/en/manual/concepts' }, { text: 'API', link: '/en/api/' }],
        sidebar: sidebar(true), outline: { level: [2, 3] }
      }
    }
  },
  themeConfig: {
    search: { provider: 'local', options: {
      locales: { root: { translations: {
        button: { buttonText: '搜索', buttonAriaLabel: '搜索文档' },
        modal: { noResultsText: '没有找到结果', resetButtonTitle: '清除搜索',
          footer: { selectText: '选择', navigateText: '切换', closeText: '关闭' } }
      } } }
    } }
  }
})
