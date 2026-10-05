# 常见问题与限制

## 为什么节点没有显示？

普通 UINode 挂载只初始化，默认隐藏；用对应容器 Open／Push／Switch 提交显示。Root 是结构节点，不自动创建 Canvas 或 Control。检查真实挂载已进入引擎树、布局尺寸以及项目层级。

## 为什么按钮被挡住或无法点击？

Godot 空挂载 MouseFilter=Ignore；Unity 空挂载无可命中 Graphic。模态只让顶层交互，转场期间 IsInteractive=false。Tab 锁不包含挂载外的按钮，项目应明确控制它们，不用额外 try/catch 隐藏重入错误。

## 为什么动画开始闪烁？

首次等待前必须采样起点，不能先显示编辑预览终态再等布局。复杂动画等待布局时先遮挡进入页，时间轴开始后恢复；恢复轨道属性需要完整覆盖。Godot Slide／Scale 使用内容包装层，避免布局 Container 重写动画位置。

## 为什么 Register 或生成调用编译失败？

手写工厂用于非无参构造。生成器须引用为 Analyzer，注册导入 UIFramework.Generated，类型化调用导入节点业务命名空间。两个命名空间同名节点同时导入，限定 GeneratedUICalls 或用泛型 API。Unity 尚无生成器接入保证。

## 为什么同样窗口重开参数报错？

Panel 默认单实例。已有实例重开不能重新传创建参数；使用业务刷新方法，或 Close 后新建。多实例明确设置 Multiple，并保留具体实例引用。

## 为什么取消没有立刻结束？

底层引擎加载请求不可直接取消，框架等待其结束再清理晚到资源。任务必须继续被观察；不用同步阻塞或忽略取消异常。明确业务 Cancel 应通过 Dialog Complete(false)，未回答外部关闭是任务取消。

## 支持所有引擎和平台吗？

Core 渲染无关，但引擎需要适配。Godot 当前使用 4.7.2 .NET；Unity 最新变化只离线编译，Demo 时间轴资产原生验证仍待完成。没有验证移动端、IL2CPP 或所有引擎版本。

当前没有控件／布局系统、动态资源 Tab 默认工作流、自动返回策略、全局输入队列、池化预算、运行时重挂载、绑定框架、编辑器配置或 Analyzer 诊断。Source Generator 不等于自定义 Analyzer。

[异步错误](manual/async-and-errors.md) · [API](api/index.md)
