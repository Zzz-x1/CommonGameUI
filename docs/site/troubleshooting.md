# 常见问题

| 问题 | 检查与处理 |
| --- | --- |
| 挂载后不显示 | Attach 只初始化；用 Open／Push／Switch 显示。检查挂载尺寸。 |
| 按钮被挡住 | Godot 空挂载用 MouseFilter.Ignore；Unity 空挂载不放可命中 Graphic。 |
| 生命周期报重入 | 不在当前容器的准备／通知中请求自身或祖先；子容器初始化允许等待。普通连点请求会排队。 |
| 动画开始闪烁 | 首次等待前应用起点；等待布局时遮挡进入页，避免布局组件覆盖动画。 |
| 生成调用找不到 | 生成器作为 Analyzer 引用；注册导入 UIFramework.Generated，调用导入节点命名空间。 |
| 单实例重开参数报错 | 创建参数仅首次使用；重开省略参数，刷新调用业务方法。 |
| 取消没有立刻完成 | 底层加载不可取消时，需要等待其结束并清理；继续观察任务。 |
| Dialog 没返回 false | Cancel 按钮调用 Complete(false)；未回答就外部关闭是任务取消。 |

支持环境与验证状态见 [Godot](integrations/godot.md)／[Unity](integrations/unity.md)；其他引擎需实现[适配器](integrations/custom-engine.md)。

[异步与错误](manual/async-and-errors.md) · [API](api/index.md)
