# TabContainerNode：内嵌分类切换

用于设置里的音量／画面、背包里的装备／材料分类。所有页预先放在同一资源中，运行时切换已存在的节点。Tab 不记录切换历史；返回上一个历史页面使用 [Stack](stacks.md)。

## 资源结构

```text
Settings                  ← 父业务资源
├─ CategoryButtons        ← 分类按钮
└─ Body                   ← TabContainerNode 挂载
   ├─ Audio               ← 已有页面控件
   └─ Graphics            ← 已有页面控件
```

Body 作为分类页挂载，按钮放在它之外。页面可以是基础控件，也可以挂业务 UINode 脚本；不要求每页都有 View 类。

## 用法

下例放在父窗口的初始化／分类操作中。settings 是已挂载的父节点；Godot 使用 Control，Unity 使用 RectTransform。

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

SwitchAsync("Graphics") 隐藏 Audio、显示 Graphics，两个实例都保留。HideAsync 收起当前分类并清空 CurrentName；再次 SwitchAsync 可以恢复保留的页面。

## 行为与边界

- Bind 不加载或克隆资源；分类页无需自定义 View。
- Switch 保留未选页，Hide 清空选择但保留全部实例。
- 绑定名称重复、切换名称缺失仍报错；切换当前项或收起未选中容器正常完成，不重播通知和动画。
- 分类按钮在 Body 外时，切换期间由项目同时禁用选择与关闭入口。
- Switch 不会加入历史；若需要“返回之前访问的分类”，该业务记录由项目提供，或将流程改用 Stack。

[容器选择](concepts.md#按流程选择容器) · [Godot 教程](../getting-started/godot.md) · [输入](input-and-focus.md)
