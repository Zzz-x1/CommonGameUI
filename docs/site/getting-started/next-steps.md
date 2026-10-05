# 下一步

## 从菜单走向游戏客户端

| 需求 | 建议阅读 |
| --- | --- |
| 背包隐藏后保留筛选与滚动位置 | [窗口实例](../manual/panels.md) |
| 详情关闭恢复原背包 | [页面历史](../manual/stacks.md) |
| 设置所有分类一起保存于资源 | [内嵌 Tab](../manual/tabs.md) |
| 等待确认再购买 | [结果弹窗](../manual/modals-and-dialogs.md) |
| 页面恢复时刷新模型、停用时暂停装饰 | [生命周期](../manual/lifecycle.md) |
| 不同来源进入同一个页面采用不同动画 | [转场](../manual/transitions.md) |
| 原生时间轴与连续图片转场 | [时间轴](../manual/timeline-animation.md) |
| 进入大厅前准备背包资源 | [资源](../manual/resources.md) |

先将玩家数据放入独立模型，再把服务通过注册工厂注入 View。界面按钮调用模型方法，成功后刷新显示；奖励保存只做一次，动画完成或跳过不再提交业务。

项目外壳负责 Esc、退出、跨层返回和容器外按钮的操作范围。先实现明确的游戏规则，不增加全局导航队列或任意层级预设。

完整客户端见 [Samples](../samples/index.md)，接入新引擎见 [Integrations](../integrations/custom-engine.md)。
