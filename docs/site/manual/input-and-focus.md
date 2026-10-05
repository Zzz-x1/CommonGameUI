# 输入与焦点

## 使用场景

Esc 优先顶层弹窗，再关闭对比窗，再返回页面，最后在大厅确认退出。分类切换时同时禁用资源外的选择与关闭按钮。

## 资源与职责

容器锁自己管理的显示范围；Modal 操作额外临时阻挡 Context 输入并提交模态所有权。项目外壳承担跨容器按钮、全局按键和实际返回优先级。

## 使用代码

```csharp
async Task BackAsync()
{
    if (modals.Current is ConfirmView confirm) confirm.Complete(false);
    else if (modals.Current is UINode modal) await modals.CloseAsync(modal);
    else if (compareWindows.Count > 0) await windows.CloseAsync(compareWindows[^1]);
    else if (pages.History.Count > 1) await pages.PopAsync();
    else await AskQuitAsync();
}
// Call only after the project verifies that the current layer is interactive.
```

## 行为与限制

这是项目返回政策示例，compareWindows 由项目维护，关闭后须移除引用。确认弹窗按业务取消策略回答，不把技术失败转换为答案。IsInteractive 包括输入锁；初始化、转场和不可操作层不接受按键。适配器恢复鼠标／焦点配置，不覆盖按钮业务 Disabled／interactable 条件。焦点恢复只使用仍有效、可见且可用的目标；原生按键导航由引擎配置。没有全局输入队列、导航策略或自动 Esc 绑定。完整输入门与任务观察见教程。

## 相关章节

[Godot 教程](../getting-started/godot.md) · [Unity 教程](../getting-started/unity.md)
