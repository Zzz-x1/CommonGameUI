# 代码审查报告

日期：2026-11-23。方式：静态源码与调用路径检查；未修改产品实现，未运行测试或引擎。以下问题为静态分析结论，复现步骤尚未执行。

## 范围与限制

本轮重点检查 Core 的资源获取、容器排队与弹窗接口，MVVM 的绑定、命令、集合项和异常清理，资源生成器，以及 Unity 资源加载和 Godot 生命周期绑定；对照了生成器测试。仓库初始工作区干净。

这不是全仓逐文件审查完成证明：Core 生命周期和容器的全部分支、IL Weaver、两端编辑器与绑定目标、Demo 全部业务和全部中英文文档尚未完成逐项核查。设计文档与版本计划首次读取发生输出截断，不能声称完整核对了所有契约。本报告只记录已有明确源码证据的问题，不以未发现问题表示通过。无构建、原生运行、导出或性能测试结果。

## 发现

### R1 · P2：长期存活页面永久保留已完成命令和集合任务

位置：`src/UIFramework.Mvvm/BindingScope.cs:122–128`、`src/UIFramework.Mvvm/MvvmRuntime.cs:150`。

每次命令执行同时加入 runtime 与 scope 的 `_commands`；正常完成不移除，runtime 仅在 scope 移除时清理，scope 仅在最终释放时清理。Scope.Observe 同样持续添加任务，直至页面销毁。帧观察器清除 runtime 的 `_tasks` 并不清除上述集合。

触发：长期打开的背包/商店页面反复执行命令、刷新集合，或 KeepAlive 页面反复打开。已完成对象数量随操作次数增长；命令还持有 scope、父命令和完成任务，关闭时扫描全部历史，形成持续内存和清理成本增长。

建议：区分正在执行的工作与尚未观察的失败；完成并观察后释放追踪记录，保留自关闭命令所需的当前依赖直到释放完成。验证同一页面重复执行与集合更新后，追踪规模不随历史次数增长，并覆盖命令关闭自身场景。

### R2 · P2：等待多任务失败时仅保留一个异常

位置：`src/UIFramework.Mvvm/BindingScope.cs:151–161`、`src/UIFramework.Mvvm/MvvmRuntime.cs:39`。

这些路径 await Task.WhenAll 后只把 catch 到的单个异常加入 Failure。多个任务同时失败时，其他原异常保存在聚合任务中，但代码没有读取该任务的全部异常，也未遍历各任务，因而返回给调用方的失败及 UIFailure 附加异常集合不完整。

触发：同一 scope 使用 ObserveMvvmTask 注册两个独立失败任务，再等待 scope 关闭；或 runtime 同一批观察任务失败后调用 WhenIdleAsync。普通外部观察任务不一定自行记录日志。

建议：等待全部结束后逐一收集失败任务的原异常，首个作为主异常，其余作为附加异常，注意重复任务和取消。CollectionTargetBase.CloseAsync 已有逐任务观察的参考模式。验证两个不同异常实例都能从调用结果取回。

### R3 · P2：生成的 Show 弹窗调用丢失业务参数入口

位置：`src/UIFramework.Generators/UIResourceGenerator.cs:67–74`；对照 `src/UIFramework.Core/Containers.cs:837`。

生成器为 Push/Open 计算 argumentDeclaration 和 argumentCall，但为 IDialogNode<TResult> 生成 Show 方法时忽略它们，固定不接受 args，并向 ShowDialogAsync 传 null。

触发：一个弹窗同时实现 IParameterizedNode<TArgs> 和 IDialogNode<TResult>，例如需要商品信息并返回购买确认的弹窗。生成的 ShowXAsync 无法传入商品；如果参数校验拒绝 null，会在打开时失败。手写 Core API 可传参，因此属于生成 API 与核心能力不一致。

建议：Show 与 Open 使用同一参数推导；对需要参数的弹窗提供类型化参数。现有 GeneratorTests 只分别覆盖有参数普通节点与无参数弹窗，应补充两者组合的编译与参数转发验证。

### R4 · P2：集合项上下文释放后仍可加载资源，产生无人释放的句柄

位置：`src/UIFramework.Mvvm/CollectionItems.cs:33–44`。

LoadAsync 入口只检查 `_resource`/`_element`，不检查 `_dispose`；先加载并 Instantiate，随后 AttachAsync 才拒绝已释放上下文。空上下文 DisposeAsync 后再 LoadAsync 会成功获取资源并创建显示对象，然后抛异常；此时 DisposeAsync 返回先前已经完成的任务，不会再次清理新资源。加载尚未完成时调用 DisposeAsync 也存在同类问题。

建议：加载开始前校验生命周期并标记正在创建，明确处置与进行中加载的协调关系；释放等待加载结束并清理最终所有权，避免只追加一个入口检查却遗漏跨 await 的竞态。验证先释放再加载、加载中释放均不遗留资源或显示对象。

### R5 · P2：同命名空间中同名嵌套节点会生成重复方法

位置：`src/UIFramework.Generators/UIResourceGenerator.cs:39–45`。

生成调用按 ContainingNamespace 分组，方法后缀只使用 Symbol.Name。两个公开嵌套类型 A.SettingsView 与 B.SettingsView 若都带 UIResource，生成的 OpenSettingsViewAsync 等方法参数完全相同，仅返回类型不同，C# 无法按返回类型重载，导致使用生成器的项目编译失败。

建议：方法命名包含必要的包含类型路径，或调整生成调用的归属，保持确定且不冲突的名称；不需要新增 Analyzer。现有测试仅覆盖不同 namespace 的同名类型，应补同 namespace 的嵌套类型用例。

## 修复顺序

优先处理 R4 的资源所有权与 R2 的异常完整性，再处理 R1 的长期追踪对象积累；R3/R5 可作为生成器一次独立修复。以上均为建议，尚未实施或验收，不代表新增设计已获确认。
