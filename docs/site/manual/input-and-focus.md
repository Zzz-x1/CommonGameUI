# 输入与焦点

Esc 先处理顶层弹窗，再关对比窗，再返回页面，最后确认退出。

## 用法

这是项目返回规则。容器负责自己的输入锁；挂载外的按钮和全局按键由外壳控制。

```csharp
async Task BackAsync()
{
    if (modals.Current is ConfirmView confirm) confirm.Complete(false);
    else if (modals.Current is SettingsFlowWindow flow && flow.Pages.History.Count > 1)
        await flow.Pages.PopAsync();
    else if (modals.Current is { } modal) await modals.CloseAsync(modal);
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

## 页面首选焦点与历史返回

在窗口根脚本的 Inspector 中，把首选按钮拖到 **Preferred Focus**。Unity 使用 uGUI Selectable，Godot 使用 Control；需要动态决定时，原生节点可覆写同名的 `GetPreferredFocus()`。指定的控件必须属于此窗口。

Stack 首次显示且没有其他窗口持有有效焦点时，选中首选控件。从装备行进入详情再返回，框架先尝试选中原来的装备行。若该行已移除或禁用，改选首选控件，再尝试窗口内首个可用控件。缓存重开按一次新打开处理。模态拥有输入时优先，显示普通 HUD 不会抢走其他页面的焦点。Tab 仅在焦点原属于切出的内容时把它移入新内容，分类按钮保留已有焦点。

## 要点

- 只在当前层 IsInteractive 时提交操作；初始化和转场期间等待。
- compareWindows 由项目维护，关闭后移除引用。确认弹窗用业务答案表达取消。
- 当前 Modal 包含多级菜单时，先返回其内部 Stack；首页再关闭 Modal。SettingsFlowWindow 见 [Modal + Stack](modals-and-dialogs.md#单窗口多级菜单modal--stack)。
- 输入门保留按钮业务 Disabled／interactable 状态；焦点只恢复到有效、可见、可用的控件。
- 键盘导航由引擎配置，完整输入与任务观察见教程。

[Godot 教程](../getting-started/godot.md) · [Unity 教程](../getting-started/unity.md)
