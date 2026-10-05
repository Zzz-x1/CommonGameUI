# 内嵌分类页面

## 使用场景

设置的音量／画面页面在美术设计时都放在同一个资源中，运行时只切换显示；分类页可以没有业务 View。

## 资源与职责

Settings → Body → Audio / Graphics。Body 是 Tab 挂载，两页借用已有控件。绑定不加载、复制或克隆资源。需要逻辑时传自定义 Node，否则适配器创建基础 UINode。

## 使用代码

```csharp
var tabs = await settings.CreateContainerAsync<TabContainerNode>("Categories", bodyControl);
await tabs.BindAsync("Audio", audioControl);
await tabs.BindAsync("Graphics", graphicsControl);
await tabs.SwitchAsync("Audio", playAnimation: false);
await tabs.SwitchAsync("Graphics");
await tabs.HideAsync();
await tabs.SwitchAsync("Audio");
string? category = tabs.CurrentName;
```

## 行为与限制

此处为 Godot Control 版本，Unity 传 RectTransform。未选项隐藏但仍存在，值与业务实例保留；Hide 清空当前选择且不销毁页面。名称在当前容器内解析，重复或缺失报错；重复切换当前项是非法核心操作，项目按钮可在提交前判断是否相同。`SwitchAsync<T>` 仅用于恰好一个匹配类型的绑定项，基础 Node 页优先使用名称。Tab 的锁覆盖挂载，分类按钮若在挂载外，项目须在切换期间同时禁用它们和相关关闭入口。

## 相关章节

[Godot 教程](../getting-started/godot.md) · [输入](input-and-focus.md)
