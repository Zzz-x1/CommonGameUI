# 内嵌分类页面

音量和画面分类一起放在 Settings 资源中，运行时绑定并切换显示。

## 用法

结构：Settings → Body → Audio／Graphics。Body 是挂载，两页使用已有控件。下例为 Godot；Unity 传 RectTransform。

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

## 要点

- Bind 不加载或克隆资源；分类页无需自定义 View。
- Switch 保留未选页，Hide 清空选择但保留全部实例。
- 名称重复、缺失或切换当前项会报错；项目按钮可忽略未改变的选择。
- 分类按钮在 Body 外时，切换期间由项目同时禁用选择与关闭入口。

[Godot 教程](../getting-started/godot.md) · [输入](input-and-focus.md)
