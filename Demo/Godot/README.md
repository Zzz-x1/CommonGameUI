# Godot Demo

本项目持续随框架能力更新。使用 Godot 4.7.2 .NET、.NET 10 SDK；框架核心仍为 netstandard2.1。

## 启动

在仓库根目录运行 `./scripts/Run-Demo.ps1`，或用 Godot .NET 编辑器导入此目录的 `project.godot`，构建 C# 后运行。

## 操作与预期

| 左侧入口 | 操作 | 预期 |
| --- | --- | --- |
| Embedded tabs | 修改滑块，切换 Audio 后返回 Graphics | 基础 Node 按字符串切换，状态保留，不新增场景实例 |
| Inventory stack | 切换 Materials，点击 Inspect，再返回 | 原背包实例及所选 Tab 保留 |
| Settings window | 打开设置，进入 Graphics，Return，Close | 一层模态外壳和遮罩；内部栈返回，关闭恢复输入/焦点 |
| Independent cards | 打开两张卡片，关闭其中一张 | 另一实例保持显示；可再次打开更多实例 |
| Nested dialogs | Open nested，确认/取消内层，再结束外层 | 仅栈顶参与交互；结果返回后恢复下层输入 |
| Cancel loading | 启动一个刻意延迟的加载并销毁其测试作用域 | 等待操作明确取消，无迟到实例；保留取消日志 |

资源在 `Scenes` 中可直接编辑。EmbeddedTabsDemo 场景故意让 Audio 在编辑器可见，运行时显式选择 Graphics，验证预览状态与运行时状态分离。页面无需定义专门 View 类。

Demo 初始化已使用 Godot 便利 API：`GodotUI.Create(this)` 构造上下文，`Root.CreateContainerAsync<T>` 一次创建并挂载项目容器，`AttachToAsync` 接入已有业务节点。内嵌 Tab 使用 `BindAsync(name, Control)`，业务引用通过 `GetControl<T>(path)` 获取；无需逐项编写显示句柄包装。显式 owned 动态挂载仍保留底层示例。

## 自动验证

```powershell
# 仓库根目录执行。
./scripts/Run-Demo.ps1 -Headless -SelfTest
./scripts/Run-Demo.ps1 -SelfTest
./scripts/Run-Demo.ps1 -Capture
```

自测包含 6 个场景：内嵌 Tab、栈和 Tab 组合、设置窗口、独立实例、嵌套结果、取消与根清理。所有通过时输出 `GODOT DEMO SELF-TEST PASSED (6 scenarios)` 并退出 0。取消用例预期保留 TaskCanceledException 日志，测试明确断言取消结果，不能当成技术故障被吞掉。

运行这 6 个场景前，还验证适配器便利 API：Root 不含默认项目节点、自定义加载器/日志保留、Control-only Tab 的基础节点和实例复用、类型化控件获取、借用挂载销毁时不释放 Control、已有节点身份不变、取消无残留、初始化失败原异常传播及清理。通过时输出 `GODOT ADAPTER SELF-TEST PASSED`。这组验证有意触发取消和初始化失败，原始日志必须保留。

故意失败独立运行，必须退出 1，打印原始异常及堆栈：

```powershell
./scripts/Run-Demo.ps1 -Headless -Failure tab
./scripts/Run-Demo.ps1 -Headless -Failure duplicate
./scripts/Run-Demo.ps1 -Headless -Failure load
./scripts/Run-Demo.ps1 -Headless -Failure lifecycle
./scripts/Run-Demo.ps1 -Headless -Failure transition
```

Godot 信号入口把任务保存在 Demo 的任务列表中，主帧观察完成/失败；失败打印并重新抛出原异常，结束当前运行。没有 async void 丢失任务异常的业务处理器。

## 验证记录：2026-10-02

- Windows 11，.NET SDK 10.0.400，Godot 4.7.2 .NET。
- 完整解决方案编译：0 警告、0 错误。
- 核心/生成器：23 项测试通过。
- Godot headless：6 个场景通过。
- 5 个故意失败用例均退出 1，保留原始异常。
- 原生 OpenGL 渲染：已生成并目视检查 Tab 页面、设置画质弹窗截图；没有用浏览器模拟 Demo。
- 自测是程序化场景验证；尚未完成所有窗口尺寸、手柄设备、导出平台的人工兼容性测试。

2026-10-02 根据用户选择④，将 Demo 改为 Kenney UI Pack - Adventure 卡通冒险风格，配以 Nunito 字体。使用米色纸张、木色边框、青绿色背景与红色标题旗帜；按钮、滑块、选项控件和模态窗口采用统一样式。

编辑 `Themes/Adventure.tres` 可调整样式，`project.godot` 已将其设为项目 Theme，可在编辑器预览。素材来源、CC0 与字体 OFL 原始许可在 `Assets` 中。不存在运行时在线素材依赖。

此次换肤验证：Demo 构建 0 警告、0 错误；Godot headless 的 6 场景自测通过（退出 0，预期取消日志保留）；原生 OpenGL 的 Tab 与画质弹窗截图已重新生成并目视检查。截图运行无资源缺失或渲染报错。此次未修改框架核心。
