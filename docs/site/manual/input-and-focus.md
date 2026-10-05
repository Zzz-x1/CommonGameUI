# 输入与焦点

Esc 先处理顶层弹窗，再关对比窗，再返回页面，最后确认退出。

## 用法

这是项目返回规则。容器负责自己的输入锁；挂载外的按钮和全局按键由外壳控制。

```csharp
async Task BackAsync()
{
    if (modals.Current is ConfirmView confirm) confirm.Complete(false);
    else if (modals.Current is UINode modal) await modals.CloseAsync(modal);
    else if (compareWindows.Count > 0)
    {
        var window = compareWindows[^1];
        await windows.CloseAsync(window);
        compareWindows.Remove(window);
    }
    else if (pages.History.Count > 1) await pages.PopAsync();
    else await AskQuitAsync();
}
// Call only after the project verifies that the current layer is interactive.
```

## 要点

- 只在当前层 IsInteractive 时提交操作；初始化和转场期间等待。
- compareWindows 由项目维护，关闭后移除引用。确认弹窗用业务答案表达取消。
- 输入门保留按钮业务 Disabled／interactable 状态；焦点只恢复到有效、可见、可用的控件。
- 键盘导航由引擎配置，完整输入与任务观察见教程。

[Godot 教程](../getting-started/godot.md) · [Unity 教程](../getting-started/unity.md)
