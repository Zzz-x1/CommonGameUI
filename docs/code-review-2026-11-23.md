# 代码审查报告（复审）

日期：2026-11-23。方式：静态源码与调用路径检查；未修改产品实现，未运行测试或引擎。以下问题为静态分析结论，复现步骤尚未执行。

## 范围与限制

本轮重点检查 Core 的资源获取、容器排队与弹窗接口，MVVM 的绑定、命令、集合项和异常清理，资源生成器，以及 Unity 资源加载和 Godot 生命周期绑定；对照了生成器测试。仓库初始工作区干净。

这不是全仓逐文件审查完成证明：Core 生命周期和容器的全部分支、IL Weaver、两端编辑器与绑定目标、Demo 全部业务和全部中英文文档尚未完成逐项核查。设计文档与版本计划首次读取发生输出截断，不能声称完整核对了所有契约。本报告只记录已有明确源码证据的问题，不以未发现问题表示通过。无构建、原生运行、导出或性能测试结果。

## 复审标准

用户于 2026-11-23 明确：不对任何用户使用导致的错误进行兜底。本次以遵守已公开契约的调用为前提；不将非法参数、错误配置、超出支持范围的类型或释放后继续使用等误用列为框架修复项。不建议增加自动纠正、容错分支、默认值、重试或扩展支持范围。合法调用中的资源管理、异步异常传播仍属于框架职责。

本次重新检查前次五项发现及其源码调用链，并补查生成器中英文用法限制、集合正常销毁流程与对应测试源码。保留 R1–R3，撤回 R4/R5。未执行测试，以下为静态复核；本次不是全仓审查完成声明。

## 保留的问题

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

合法使用场景：公开、顶层、非泛型的原生弹窗节点同时实现 IParameterizedNode<TArgs> 和 IDialogNode<TResult>，例如需要商品信息并返回购买确认的弹窗。中英文生成器指南明确宣称生成调用透传参数；这里的问题是生成器未提供传参签名，不是要求框架在调用方漏传参数时填入默认值。生成的 ShowXAsync 无法传入商品；如果参数校验拒绝 null，会在打开时失败。手写 Core API 可传参，因此属于生成 API 与核心能力不一致。

建议：Show 与 Open 使用同一参数推导；对需要参数的弹窗提供类型化参数。现有 GeneratorTests 只分别覆盖有参数普通节点与无参数弹窗，应补充两者组合的编译与参数转发验证。

## 撤回的发现

### R4：释放后使用集合项上下文

撤回原 P2 与增加生命周期检查、加载协调机制的修复建议。

原复现直接在已释放上下文上调用 LoadAsync，属于误用。重新追踪正常集合移除流程：CollectionTargetBase.RemoveCore 取消条目，ReleaseAsync 在 CollectionItems.cs:133–138 先等待 entry.Completion，再释放 Context；创建失败路径也在创建结束后释放。正常窗口关闭不会按原报告假设的顺序提前释放正在加载的上下文。

现有 CollectionItemTests.OwnerCloseWaitsForIgnoredLoadCancellationAndReleasesAcquisition 以及 CancelingInitialCollectionDrainsIgnoredLoadAndNeverDisplaysWindow 已覆盖相关合法路径（本次只阅读，未重新运行）。原报告未证明框架正常流程会触发该泄漏，不应为该误用追加防御实现。

### R5：同名嵌套节点生成冲突

撤回原 P2 与改变生成命名规则的修复建议。

中英文 docs/site/manual/source-generator.md 与 docs/site/en/manual/source-generator.md 第 7 行明确要求 public top-level 节点。原复现采用嵌套类型，超出已支持用法。不得以此要求生成器兼容嵌套节点、自动改名或新增诊断机制。

## 后续处理建议与验证状态

建议先修 R2 的异常传播完整性，再处理 R1 的历史任务积累和 R3 的类型化参数生成。R2 应传播全部原始失败，不恢复业务、不吞异常、不伪造成功；R1 应调整任务所有权，不添加容量上限后丢任务之类兜底；R3 只生成正确的参数入口，不接受错误参数或提供默认业务数据。

本次只修订审查报告和唯一设计文档中的审核原则，未修改框架、Demo、测试或 DLL。已做源码调用链、文档支持范围及差异检查；没有执行编译、测试或引擎验证。保留问题尚未修复和运行验收。前述全仓覆盖缺口仍然存在。

## R1 实施更新（2026-11-23）

已调整活动记录回收：帧观察、等待空闲结束或下一次命令启动清除已完成命令，成功观察任务及时移除；失败任务保持独立观察和清理传播。RuntimeTests 32 项通过，含新增长期存活页面记录积累验证。自关闭释放顺序保持不变。R2/R3 本次未修改。

## R2、R3 实施更新（2026-11-25）

- R2 已修复。MVVM 等待一批任务时会逐项收集失败。调用方接收到原始异常，其余异常可通过 `UIFailure.GetSecondaryExceptions` 读取。
- R3 已修复。带创建参数的结果弹窗会生成相应的类型化 `Show…Async`。生成器测试实际编译并运行购买确认调用，验证弹窗收到了传入的商品 ID。

本轮页面恢复、集合复用及验证结果见[设计决策](design-decisions.md#41-页面恢复绑定更新与异步错误2026-11-25)。
