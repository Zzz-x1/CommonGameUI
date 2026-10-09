# UI 管理框架设计文档

更新日期：2026-10-08（Asia/Shanghai）。当前包含 V5、动画与资源能力、Godot 客户端、Unity 适配、框架审核及双语用户文档；最新用户文档交付见第 16 节，引擎生命周期绑定见第 17 节。历史版本验收保留其当时状态。

## 目录

1. [约束与范围](#1-约束与范围)
2. [概念与职责](#2-概念与职责)
3. [API 场景与归属](#3-api-场景与归属)
4. [待定设计与调研](#4-待定设计与调研)
5. [版本开发文档入口](#5-版本开发文档入口)
6. [当前实际契约](#6-当前实际契约)
7. [V2 生命周期契约](#7-v2-生命周期契约)
8. [V3 功能覆盖方向](#8-v3-功能覆盖方向)
9. [V4 候选范围与 USN 对照](#9-v4-候选范围与-usn-对照)
10. [V5 实现与后续候选](#10-v5-实现与后续候选)
11. [动画能力](#11-对比-usn-补齐动画能力2026-10-02)
12. [资源迭代与 USN 覆盖](#12-下一版候选与当前-usn-覆盖结论2026-10-02)
13. [Godot 客户端](#13-星庭远征完整重建客户端-demo2026-10-03已确认)
14. [Unity 适配与 Demo](#14-unity-适配与原生-demo2026-10-03)
15. [框架审核](#15-框架代码审核)
16. [中英文用户文档](#16-中英文用户文档2026-10-05)
17. [引擎生命周期绑定](#17-引擎生命周期绑定2026-10-05)
24. [关闭后实例缓存](#24-关闭后实例缓存2026-10-07已确认)
25. [Modal 显示与遮罩首帧](#25-modal-显示与遮罩首帧2026-10-07)
26. [Context 默认遮罩](#26-context-默认遮罩2026-10-07已确认)

## 阅读约定

- **已确认**：用户明确提出或确认的要求，后续工作必须遵守。
- **建议**：设计候选，不代表已获用户确认。
- **待定**：尚不能作为实现依据的具体契约或语义。

第 1–5 节保留设计阶段的概念与候选；第 6 节起按版本记录实现与验收，历史记录不代表最新 API。当前用法见[用户文档](site/index.md)，实际签名以源码为准。新决定更新本文并同步相关指南。

## 1. 约束与范围

### 1. 目标（已确认）

基于 C# 的渲染无关 UI 管理框架，可集成到使用 C# 的游戏引擎。开箱即用、简单易上手、API 简洁，支持快速原型、复杂项目与高扩展性。

新引擎需要相应适配器，不能承诺未经适配即可运行。

长期 Demo（已确认）：Godot 项目作为本工程持续维护的 Demo，也是实际验证/测试载体。Demo 随框架已实现功能更新迭代，不是一次性验证项目，维护范围不限于 V1。初版已创建于 `Demo/Godot`，采用 Godot 4.7.2 .NET。V1 交付安排维护在 V1 开发文档。

Demo 美术方向（已确认，2026-10-03）：按用户批准的重建计划，当前 Demo 为原创中文奇幻二次元客户端「星庭远征」，采用浅色面板、金色装饰、六名角色立绘与幻想背景，替代此前 Kenney Adventure 木框方案。素材及字体来源集中于 Demo 的 Assets/SOURCES.md，样式集中于 Themes/Client.tres，不进入渲染无关核心契约。此前版本章节中的旧场景、素材与验收属于历史记录，当前实施见第 13 节。

版本管理（已确认，2026-10-02）：工程初始化本地 Git 仓库，后续修改完成并执行适用验证后自动提交相关代码、Demo 与文档。提交前检查变更范围和暂存内容；构建产物、工具、缓存及截图输出不入库。阶段性提交必须如实记录未完成/未验证状态。当前仅管理本地仓库，远程推送或改写历史需另有用户授权。

设计演进规范（已确认，2026-10-02）：发现功能设计存在缺陷或不合理时，优先提出解决方案并重构，不以围绕原设计局部修补为默认路线。所有改动不考虑历史版本兼容，可以直接调整或删除旧类型、API 和行为；不引入兼容层、旧入口转发、双轨钩子或旧行为开关。重构须同步迁移工程内调用方、生成器、适配器、Demo、测试和文档，保持新设计完整可验证。此规范不取消原始异常传播、资源清理及用户已确认的其他要求。

代码简洁规范（已确认，2026-10-02）：始终保持代码逻辑简洁清晰，后续所有开发按此要求执行。用明确的类型、资源结构、生命周期和直接的业务流程实现功能，以适用测试保证正确性；不堆叠重复检查、保底分支、无实际用途的抽象或诊断代码。正常 Demo 不捕获和手工处理运行异常，不包装异常、不自定义错误退出或降级；同步错误直接抛出，异步任务必须被观察并让原始异常直接交给宿主报错。测试断言与预期异常验证集中在测试代码，不混入业务。框架必要的日志与失败清理仍按原始异常契约执行；这不是取消异步观察或吞掉错误。

场景驱动与避免过度设计（已确认，2026-10-02）：所有功能设计必须从游戏开发中实际可能使用的场景出发，禁止凭空编造需求、为展示 API 倒推业务场景，或为理论完整性与假想扩展增加功能。设计顺序为：具体游戏业务流程与预期行为 → 现有能力能否满足及缺口 → 框架还是项目应承担职责 → 最小必要 API → 对应业务 Demo 与适用验证。场景可以来自用户需求、本工程实际业务流程或可核对的成熟项目实践，不要求已经上线，但推测不能表述为事实。现有能力或简单项目代码足以表达的需求，不另建框架机制；涉及资源、生命周期、输入和异常一致性的真实公共缺口，则由框架负责。

成熟方案用于核对处理方式和取舍，不把其功能清单直接变成本工程需求。没有充分场景支撑的功能暂缓，不先增加通用策略系统、扩展接口或版本承诺。“支持复杂项目”不等于提前实现所有可能操作，“完整栈流程”也不等于理论栈操作全集。待定建议不自动纳入实现范围；所有新旧候选都适用本规范。

### 2. 模型与 API（已确认）

- 框架以层级节点 Node 建模，业务类可以使用 InventoryView 等名称。
- Container 是 Node，通过继承扩展不同能力的节点类型。
- 不强制业务 UI 继承 Page、Sheet、Modal 三种基类。
- 具体容器直接提供操作，不暴露 Navigation / Selection 中间对象。
- SwitchAsync 只属于 Tab 容器；通用基类不能集中暴露所有操作。
- 通过类型契约排除不支持的操作，不制造 Push / Switch 混合语义。
- 不提供 HUD、Content、Overlay 等默认节点、层级预设或固定枚举。
- 项目自行定义节点名称与结构，后续可以通过编辑器配置。
- 框架默认包含结构性 Root，不创建项目业务节点；当前通过 UIContext.Root 获取。

### 3. 工具范围

**已确认**：可引入 Source Generator 减少编码；当前不引入 Analyzer、专门诊断分析系统或编译期诊断功能，待框架完善后再考虑。

**建议**：生成器优先自动注册及类型化调用，后续考虑引用、绑定和订阅生成。生成代码调用核心统一逻辑，保留手写入口；通过类型契约或显式声明识别节点，不依赖 View / Node 后缀。

诊断延期不取消运行时异常、基础日志及正确性测试。编辑器配置格式和实现尚未确定。

异步范围（已确认，2026-10-02）：不滥用异步方法。纯构造、注册、查询、配置和即时状态修改采用同步 API；需要等待资源、生命周期、转场、弹窗结果或清理时才使用异步契约。方法仅转发下层任务时直接返回 Task，不额外使用 async/await，不用 Task.Run 包装即时操作，不用 Wait/Result/GetAwaiter().GetResult 将异步操作伪装成同步 API（Demo 的帧入口观察已完成任务的结果除外）。

### 4. 异常与日志（硬性要求）

用户原话：

> 遇到问题直接抛出原始异常和打印日志，禁止做任何保底跳过，隐藏错误

适用于核心、适配器及生成代码：

- 记录失败上下文并传播原始异常。
- 捕获后使用 `throw;`，保留原始堆栈；禁止 `throw ex;` 或统一异常包装。
- 禁止吞异常、静默跳过、默认值保底、占位替代、自动降级或伪造成功。
- 禁止隐式自动重试，业务需要时由业务显式实现。
- 异步失败必须可被调用方观察，不能遗失内部任务异常。
- 必要清理仍执行；清理失败必须暴露，不能覆盖或隐藏原始失败。
- 非法状态直接抛明确异常。
- 日志包含节点类型、实例标识、操作与生命周期阶段，避免重复记录同一异常。

用户取消可以是业务结果，技术失败不能转换为用户取消。调用取消应明确传播取消状态。

主操作与清理同时失败使用 UIFailure.GetSecondaryExceptions 暴露附加原异常，详见第 7 节。

### 5. 范围建议

核心管理节点、层级、生命周期及容器流程；控件、布局、绘制及命中测试由后端负责，具体引擎类型不进入核心契约。

优先管理逻辑跨引擎复用。统一界面文件跨引擎显示需要统一控件、布局、字体、样式与资源格式，尚未确认纳入范围。MVVM、绑定、DI 建议可选。

## 2. 概念与职责

已确认方向与具体实现分开描述。类型名称、成员、状态及运行时规则尚未定稿。

### 1. 类型关系与运行时结构

Container 是 Node，能力通过派生类型扩展。候选继承关系：

```text
UINode
├── InventoryView（业务类示例）
└── ContainerNode
    ├── StackContainerNode
    └── TabContainerNode
```

继承决定类型能力，运行时父子关系决定实例归属。InventoryView 可以包含 TabContainerNode 子节点，不必继承 TabContainerNode。

### 2. Node（节点）

**定义（已确认）**：框架管理 UI 的基础概念，参与层级结构，可以派生出提供不同能力的类型。

**职责（建议）**：实例身份、父子关系、公共生命周期、节点状态、自身数据及资源所有权。

**边界（建议）**：普通 Node 不提供所有导航操作，不负责 GPU 绘制。可以承载实际界面，也可以仅组织子节点；不要求每个按钮或文本都成为管理节点。

父节点退出对子节点的影响、重挂载和状态传播规则待定。

### 3. View（业务命名）

**定义（已确认）**：业务类的命名方式，如 InventoryView、SettingsView；在框架内部仍是 Node，不是另一套基础模型。

**职责（建议）**：业务数据展示、交互和生命周期逻辑，组合业务需要的子节点或容器。

**边界**：View 后缀不决定能力，不作为生成器识别依据；不必另设 UIView 核心基类。

### 4. ContainerNode（容器节点）

**定义（已确认）**：一种 Node，通过派生容器类型提供不同子节点管理能力。

**职责（建议）**：容器共享的子节点管理、操作协调及挂载接入；具体类型管理自己的流程与状态。

**边界（已确认）**：不能集中暴露所有操作，不暴露 Navigation / Selection 中间对象。公共成员及是否需要独立 ContainerNode 基类待定。

### 5. StackContainerNode（栈式容器）

**定义**：提供栈式进入与返回能力的容器，名称为候选。

**职责（建议）**：直接提供 PushAsync 和出栈操作，维护历史与当前项，协调进入、退出、转场和恢复。

**边界**：不提供 Tab 的 SwitchAsync。历史顺序与 Children 归属分别记录，不能以全部子节点顺序替代历史。

出栈名称、历史项保存形式、实例保留与多层返回规则待定。

### 6. TabContainerNode（标签容器）

**定义（已确认方向）**：具有 Tab 切换能力的容器；SwitchAsync 仅属于此类型。

**主要工作流（已确认偏好）**：类似 UMG WidgetSwitcher 的使用方式，优先使用父 View 资源中内嵌的页面。父资源实例化后绑定既有页面对象，切换时复用，不能再次加载或克隆页面。

**项类型与名称（已确认）**：Tab 页面可以只是基础 Node，不要求具体业务 View 实现。TabContainer 必须提供 `SwitchAsync(string name)`，按当前容器内的项名称定位并切换；泛型调用可以保留为可选便利方式，不能成为唯一入口。

名称规则建议：使用容器局部唯一的项名称，不是资源路径或全局节点查找路径；显式绑定名称，也可由项目配置指定。名称比较规则尚待确定；找不到名称、重复注册或归属冲突直接抛异常并打印日志，不回退到首项或其他项。同类型多个基础 Node 以名称区分，泛型调用不能隐式挑选第一个。

**职责（建议）**：维护内嵌切换项及当前项，直接提供 SwitchAsync，协调旧项退出与新项进入，不建立栈式历史。运行时通常只显示一个当前项；有转场时两项是否暂时同时显示仍需约定。

**边界**：不通过运行时模式变成栈式容器。主要能力是切换已绑定项，不承担通用资源加载器角色。独立资源动态 Tab 是后续扩展候选，不作为 V1 默认路径。页面声明、初始项、重复切换和状态保留规则待定。

### 7. Hierarchy（逻辑层级）

**定义**：运行时节点实例的父子结构，UIHierarchy 为候选管理名称。

**职责（建议）**：维护归属、查询与组合，并约束单父归属及无循环结构。

**边界**：不等于类型继承、历史或显示顺序，不必镜像引擎控件树。

### 8. Root / Context（根与上下文）

**Root 定义（已确认）**：框架默认包含的结构性根节点，用户可通过 API 获取，作为项目节点层级的起点。候选获取形式为 `ui.Root`，具体名称尚未定稿。

**Context 定义（建议）**：节点层级所需的运行环境。默认存在 Root 不表示已经确认全局静态单例、多上下文或额外 Root 创建能力。

**职责（建议）**：实例注册、后端服务、作用域与统一退出入口。

**边界（已确认）**：不能自动创建 HUD、Content、Overlay 等项目节点。根和上下文是否合并、Root 所支持的公共操作、初始化/销毁时机及具体名称待定；不能因 Root 存在而在其上暴露所有容器操作。

### 9. 后端显示对象与 Adapter

**显示对象**：后端真实创建的显示实例，例如 GameObject、VisualElement、Godot Control、GObject；这些不进入核心依赖。

**Adapter 职责（建议）**：把管理请求转为后端创建、挂载、显隐、排序、交互、焦点及销毁操作，接入宿主线程调度。

**边界**：后端负责控件、布局、绘制和命中测试；适配器不重新定义导航语义。接口和后端能力差异处理待定。

初始化便利 API（已确认方向，2026-10-02）：引擎适配层封装重复的服务构造、显示句柄包装与挂载初始化，减少接入代码；保留底层 API 供自定义。封装不向纯核心引入引擎类型，不自动创建项目节点，不改变容器能力或异常传播规则。Godot 首版便利 API 的具体实现见第 6 节。

### 10. Mount Target（挂载目标）与显示顺序

**挂载目标（建议）**：显示对象实际放置的位置。是否允许与逻辑父节点分离尚待确认。

**显示顺序**：真实绘制的前后关系，由适配器执行项目排序配置。

**边界**：归属决定管理关系，挂载决定显示位置，排序决定遮挡。不能用导航历史隐式替代这三个概念。

### 11. 生命周期、可见与激活

**生命周期（建议）**：实例从创建到销毁的管理过程。

**可见**：是否参与显示。**激活（建议）**：是否参与当前交互。被覆盖不自动等于销毁。

**职责边界（建议）**：容器协调时序，Node 承载公共生命周期，Adapter 应用后端状态。状态表、回调、关闭与销毁区别、父状态传播待定。

### 12. 模态、焦点与转场

| 概念 | 定义 | 建议职责与边界 |
| --- | --- | --- |
| 模态 | 阻断指定范围其他 UI 交互的显示行为 | 不天然等于全局最高层或栈式历史 |
| 焦点 | 键盘/手柄等输入的当前目标 | 核心协调交接与恢复，适配器实施后端操作 |
| 转场 | 进入、退出或切换的可等待显示过程 | 容器协调完成状态，后端执行显示变化 |

弹窗结果、模态能力的类型归属、焦点范围、转场接口待定；当前不新增已确认的 ModalContainer 类型。

### 13. 创建与资源概念

| 概念 | 定义与建议职责 | 边界 |
| --- | --- | --- |
| 注册描述 | 节点类型与创建配置的关联 | 不等于活跃实例 |
| 工厂 | 创建节点及接入依赖 | 不假设无参构造；DI 可选 |
| 资源提供方 | 加载、持有、释放后端资源 | 资源与节点不是同一对象，所有权待定 |
| 实例引用/句柄 | 定位具体运行实例 | 多实例不能仅以类型定位；是否另设句柄类型待定 |
| 缓存 | 为复用显式保留实例或资源 | 不是失败兜底，默认策略待定 |

### 14. Source Generator 与项目配置

**Source Generator**：编译时减少重复编码的工具，不是运行时管理器。生成代码复用核心规则，范围见 [约束与范围](#1-约束与范围)。

**项目配置**：项目定义的节点名称、类型、结构及显示/资源配置。后续编辑器可以生产此配置，但不改变类型能力约束；格式及加载流程待定。

## 3. API 场景与归属

以下是调用形态草案，不是现有实现；签名、初始化和返回值待定。已确认：具体容器直接提供操作，SwitchAsync 只属于 Tab，不出现 Navigation / Selection 中间对象。

### 1. 操作归属

| 类型 | 候选 API | 状态 |
| --- | --- | --- |
| Node | 公共生命周期成员 | 范围待定 |
| ContainerNode | 共享子节点管理成员 | 不集中暴露全部操作 |
| StackContainerNode | PushAsync、PopAsync | 直接栈操作方向已确认，出栈名称待定 |
| TabContainerNode | SwitchAsync | 已确认只属于 Tab 类型 |
| 独立显示/弹窗能力类型 | OpenAsync、ShowDialogAsync 等 | 类型归属待定，不放入万能基类 |

### 2. 栈式导航

项目提供强类型的栈容器引用：

```csharp
await stack.PushAsync<InventoryView>(inventoryArgs);
await stack.PushAsync<ItemDetailsView>(itemArgs);
await stack.PopAsync(); // 返回背包，出栈命名待定。
```

stack 不提供 SwitchAsync。历史和实例复用的精确规则待定。

### 3. Tab 切换

项目提供强类型的 Tab 容器引用：

```csharp
await tabs.SwitchAsync<EquipmentView>();
await tabs.SwitchAsync<MaterialsView>();
```

如果页面没有具体业务 View 类，可以直接按名称切换：

```csharp
await tabs.SwitchAsync("Graphics");
await tabs.SwitchAsync("Audio");
```

Graphics / Audio 是项目绑定的容器内项名称，相应页面可以只是基础 Node。无需为调用切换而定义空的业务 View 类。

tabs 不提供 PushAsync。项声明和初始项规则待定。

### 4. 节点组合

```text
项目定义的栈容器
└── InventoryView
    └── 项目定义的 Tab 容器
        ├── EquipmentView
        └── MaterialsView
```

外部栈负责背包进入与返回，内部 Tab 负责标签切换。能力分别属于不同对象，不存在同一对象上的 Push / Switch 混合语义。父节点退出对子树的影响需要统一生命周期规则。

### 5. 业务命名

```csharp
public partial class InventoryView : UINode<InventoryArgs>
{
}
```

仅示意 View 与 Node 的关系；是否提供泛型 Node 待定。生成器按契约识别身份，不按名称后缀。

### 6. 暂不定稿的 API

独立多实例显示、弹窗结果、关闭实例、节点查找、挂载覆盖及初始化注册，在能力归属明确后再设计。不得重新引入通用容器同时公开所有操作的旧草案。

## 4. 待定设计与调研

### 1. 待定契约

| 主题 | 需要确定的问题 |
| --- | --- |
| 类型体系 | 命名、公共成员、继承扩展规则 |
| 层级 | 单父归属、循环检测、重挂载、查询与身份 |
| 生命周期 | 打开/关闭/销毁区别、回调与提交顺序、父子处理 |
| 状态 | 可见、激活、被覆盖及父状态传播 |
| Stack | 历史形式、Push/Pop 时序、出栈命名、多层返回、重复进入 |
| Tab | 项声明、创建、重复切换、数据更新与保留 |
| 其他容器 | 独立多实例显示、弹窗与结果能力的类型归属 |
| 异步 | 并发排序、跨容器并行、重入、取消与作用域退出 |
| 资源 | 所有权、预加载、销毁、缓存与预算 |
| 后端 | 挂载、排序、线程调度、适配契约 |
| 输入与转场 | 阻断范围、返回、焦点恢复、可等待转场 |
| 失败传播 | 主操作和清理同时失败的完整暴露协议 |
| 工具链 | C#/.NET 目标、引擎生成器兼容性 |
| 编辑器 | 项目节点配置、强类型引用、加载格式 |

### 2. 候选功能与验证

基础功能候选：注册与工厂、实例管理、生命周期、栈导航、Tab 切换、模态结果、排序、输入、异步取消、转场、资源、显式缓存、作用域退出、订阅清理、基础日志、无引擎测试。这不意味着全部进入同一个基类。

建议验证：重复请求、加载中关闭、加载/转场失败、嵌套模态焦点恢复、缓存旧订阅、外部线程调度、后端意外销毁、父节点退出、清理同时失败。失败必须可观察，不能以保底跳过通过测试。

### 3. 建议实施顺序

1. 明确节点、容器类型、生命周期及失败契约。
2. 内核、基础日志、假后端及行为测试。
3. 自动注册与类型化 API 生成器。
4. 完整适配器、模板及运行示例；节点配置属于示例项目。
5. 第二个不同架构后端，验证核心独立性。
6. 按需扩展缓存、绑定、输入、编辑器与生成能力。
7. 框架完善后再考虑诊断工具和 Analyzer。

建议验收目标：快速运行栈导航和 Tab 示例；直接容器调用；业务命名自由；不修改核心即可接新后端；核心流程与失败路径可在普通 .NET 环境验证。

### 4. 调研参考

调研日期：2026-10-02。依据官方文档或项目仓库，未做集成实测。版本可能变化，实现前重新核验；参考方案不是既定依赖。

| 方案 | 借鉴点 | 来源 |
| --- | --- | --- |
| UnityScreenNavigator | 容器、历史、转场与异步生命周期 | [仓库](https://github.com/Haruma-K/UnityScreenNavigator) |
| QFramework UIKit | 简洁调用、类型化数据及代码生成工作流 | [文档](https://qf.readthedocs.io/en/latest/QFramework_v1.0_Guide/03_toolkits/06_uikit.html) |
| CommonUI | 激活、输入、焦点、显示栈和转场 | [激活控件](https://dev.epicgames.com/documentation/unreal-engine/API/Plugins/CommonUI/UCommonActivatableWidget)、[栈](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/CommonUI/UCommonActivatableWidgetStack)、[转场](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/CommonUI/UCommonAnimatedSwitcher)、[输入](https://dev.epicgames.com/documentation/en-us/unreal-engine/commonui-input-technical-guide-for-unreal-engine) |
| FairyGUI | 跨引擎工作流、组件、转场、虚拟列表；候选后端 | [仓库](https://github.com/fairygui/FairyGUI-unity) |
| NoesisGUI | Framework/Integration API 边界 | [文档](https://www.noesisengine.com/docs/Gui.Core.WPFComparison.html) |
| UI Toolkit | 后端已有绑定与焦点能力 | [绑定](https://docs.unity.com/en-us/engine/6000.5/manual/uitoolkits/uielements/uie-data-binding) |
| Avalonia | 绑定、模板与开发体验，诊断后续参考 | [文档](https://docs.avaloniaui.net/) |
| Dear ImGui | 核心、平台及渲染后端分离 | [后端文档](https://github.com/ocornut/imgui/blob/master/docs/BACKENDS.md) |

UnityScreenNavigator 没有固定 HUD/Content/Overlay 全局层：容器位于 Unity 显示树中，可嵌套；容器之间依赖 Unity 排序，容器内部管理自己的历史或堆叠。转场可指定 Rendering Order，最新 Modal 在前。Page/Sheet/Modal 是行为分类，不是固定全局渲染层。

CommonUI 同样提供栈式返回和动画切换，不能仅凭显示栈推断持久化路由历史或销毁页面重建协议。

优先借鉴 UnityScreenNavigator 的操作时序、CommonUI 的输入与激活、QFramework 的开发体验；本框架使用 Node 及派生容器，不复制三类业务视图体系。

### 5. 内嵌 Tab 资源工作流（建议）

已确认偏好：Tab 更倾向直接使用父 View 资源中内嵌的页面，类似 UMG WidgetSwitcher 的使用体验，设计时通过显隐预览。内嵌绑定是主要路径；独立资源动态创建仅保留为后续扩展候选，不是 V1 默认路径。不是所有节点都要求独立资源。

内嵌方式建议流程：父资源实例化后，根据显式声明/引用绑定已有子页面，创建或关联其逻辑 Node，注册到 TabContainer，然后按明确默认项统一设置显示和交互状态。SwitchAsync 复用绑定实例，不再次加载或克隆。编辑器预览显隐不隐式决定运行时当前项。

页面绑定/初始化只执行一次；每次切换执行进入/退出，默认保留数据状态。绑定时不通过后端引擎 Enter/Ready 自动判定框架激活，也不扫描任意控件作为页面。引用缺失、重复身份或归属冲突直接记录并抛错。

资源所有权建议：父资源拥有内嵌显示对象，框架管理子 Node 的状态和订阅清理。最终显示树销毁由后端按父资源统一执行，避免对子资源重复卸载/释放。单独移除内嵌页面的能力和生命周期精确顺序待定。

建议的候选绑定为 `tabs.Bind("Equipment", existingNode)` 后调用已确认的 `tabs.SwitchAsync("Equipment")`，或为有业务类型的项提供泛型便利入口。Bind 名称、是否异步及输入类型尚未定稿。existingNode 可以是基础 Node；后端具体显示对象不进入纯核心，适配层负责绑定或构造显示句柄。

UnityScreenNavigator 对照：所核对的 SheetContainer 源码公开 Register 接收资源键，LoadSheet 调用 Instantiate；没有直接接收已有 Sheet 实例的注册入口。PreloadedAssetLoader 提供已持有的资源引用，不能据此当成现有实例接管机制。来源：[SheetContainer](https://raw.githubusercontent.com/Haruma-K/UnityScreenNavigator/master/Assets/UnityScreenNavigator/Runtime/Core/Sheet/SheetContainer.cs)、[PreloadedAssetLoader](https://raw.githubusercontent.com/Haruma-K/UnityScreenNavigator/master/Assets/UnityScreenNavigator/Runtime/Foundation/AssetLoader/PreloadedAssetLoader.cs)。

## 5. 版本开发文档入口

V1 范围建议、默认规则、开发阶段及验收标准统一维护在 [V1 开发文档](v1-development.md)。本设计文档维护通用概念与已确认约束，不重复保存版本开发清单。

用户已指定 V2 主要补全生命周期相关能力，开发范围、阶段、迁移与验收集中维护在 [V2 开发文档](v2-development.md)。V2 已完成，具体生命周期契约见第 7 节，容器与适配器公共入口见第 6 节。

## 6. 当前实际契约

本节记录实现采用的契约，不表示把所有历史草案改为用户已确认要求。前文尚未定稿的示例用于解释概念；使用代码以本节和工程 README 为准。

| 对象 | 已实现 API 与责任 |
| --- | --- |
| UIContext | 构造时创建唯一实例 Root；提供 Register<T>、工厂注册和 DisposeAsync；没有全局静态实例 |
| UINode / UINode<TArgs> | Id、Name、Parent、Children、Context、Element、State、Lifetime；AttachAsync、GetChild、DisposeAsync；可选类型化参数 |
| ContainerNode | 容器共享异步操作控制，不公开 Push / Switch / Open |
| PanelContainerNode | OpenAsync<T>(args = null, playAnimation = true, cancellationToken = default)、TryGet<T>、HideAsync、ShowAsync、CloseAsync；注册模式决定容器内 Single/Multiple，隐藏保留，关闭销毁 |
| StackContainerNode | PushAsync<T>(args = null, playAnimation = true, cancellationToken = default)、PopAsync、PopToAsync、Current、History；覆盖保留，返回恢复原实例，移除项销毁 |
| TabContainerNode | BindAsync(name, node, embeddedElement, token)、SwitchAsync(name, playAnimation = true, cancellationToken = default)、SwitchAsync<T>、Items、CurrentName；仅绑定既有项 |
| ModalContainerNode | OpenAsync<T>、CloseAsync(node)、CloseToAsync(target)、ShowDialogAsync<T,TResult>；单步关闭或一次返回下层实例，持有输入并恢复焦点；ConfigureBackdrop、RequestDismissAsync、CurrentBackdrop 管理遮罩 |
| DialogNode<TResult> | 可选结果契约，Complete(result)；普通模态窗口仍可为一般 UINode，不要求继承此类 |
| GodotUIElement | owned 动态场景与 borrowed 内嵌 Control 句柄；显隐、输入、排序及按所有权销毁 |
| Source Generator | UIResource 特性生成模块注册、容器直接调用、类型化参数及弹窗结果便利 API |

所有实际转场操作均支持默认 true 的 playAnimation，准确签名和选择配方契约见 10.6。

NodeState 为 Detached、Initializing、Ready、Disposing、Disposed；可见和交互是独立的局部状态，经祖先状态及输入拥有者计算有效状态。生命周期已重构为第 7 节中的前置/完成、状态通知及处理器契约，旧 OnEnterAsync/OnExitAsync 已删除。容器内部执行进入/退出，业务使用相应容器 API，不公开万能节点导航入口。

名称采用 StringComparer.Ordinal；Tab 项容器内唯一，未知名、重复名、重复切换当前项、泛型匹配多项直接抛错。Node 子节点允许同类型多实例，GetChild(name) 必须唯一，否则抛错，关闭使用具体实例。

同容器结构操作 FIFO 排队；生命周期中请求当前操作容器或祖先仍直接抛错。销毁取消进行中的操作，等待其协作结束，然后按逆序销毁子树。生命周期内销毁当前操作所属节点或祖先直接报错以避免等待自身。回调和自定义资源加载器必须配合 CancellationToken。

故障后执行一致性清理并重新抛出原异常，不把清理当成业务成功或降级。额外清理异常保留在主异常 Data 的 UIFramework.SecondaryExceptions 中，提供 UIFailure.GetSecondaryExceptions 获取，单独记录日志；主异常不包装，使用 throw 或 ExceptionDispatchInfo 保留原堆栈。

Godot owned 场景通过 QueueFree 延迟到引擎帧末物理释放；逻辑清理任务完成表示已结束管理并请求释放。立即重新绑定同一借用挂载时须先等待引擎处理已排队释放的子场景，不能把仍在树中的待释放控件当作新的内嵌资源。

Godot 调用必须在场景线程；GodotUIHost.VerifyAccess 校验，初版不自动跨线程调度。场景加载采用同步 ResourceLoader.Load；异步接口表达可等待生命周期和取消协议，不宣称已经实现后台资源加载。隐藏页面不会自动停掉业务任务，业务订阅和任务由生命周期回调及 Lifetime 管理。

Root 无具体引擎显示对象，不自动创建项目容器。Demo 自行在 Root 下附加菜单、场景、卡片及模态节点。资源文件、样式和控件是后端能力，未引入统一跨引擎资源格式。

Godot 初始化便利 API（2026-10-02 已实现）：

| API | 职责 |
| --- | --- |
| GodotUI.Create(owner, loader?, logger?) | 从已进入场景树的 Godot Node 构造宿主、默认场景加载器及日志服务，返回 UIContext；可显式替换加载器/日志，不注册业务类型、不自动创建项目容器 |
| parent.CreateContainerAsync<T>(name, mount, token) | 创建指定 ContainerNode 派生类型并挂载到已有 Control；支持自定义派生容器，名称与层级由项目指定 |
| node.AttachToAsync(parent, name, control, token) | 将已有逻辑节点绑定到已有 Control，直接返回核心 AttachAsync 的初始化任务，不另建异步状态机或返回已有节点，适合自定义构造/依赖注入 |
| tabs.BindAsync(name, pageControl, token) | 自动创建基础 UINode，绑定既有内嵌 Control；不加载或克隆 |
| tabs.BindAsync(name, node, pageControl, token) | 为内嵌页面绑定项目提供的逻辑节点 |
| node.GetControl() / GetControl<T>(path) | 获取节点显示 Control 或指定路径的类型化子控件，避免业务重复转换显示句柄 |

上述扩展方法只在 Godot 适配器中提供。Create 应从场景线程调用；显示 Control 必须仍有效并在场景树中，核心继续验证节点状态、归属、并发与取消。AttachToAsync、CreateContainerAsync、Control 版 BindAsync 使用 borrowed 所有权，逻辑节点销毁不会释放父场景中的 Control。项目自行创建且需由框架释放的显示根仍显式使用 `new GodotUIElement(control, owned: true)` 配合底层 AttachAsync。挂载失败仍执行核心清理并原样抛出；重复项、缺失控件或类型不匹配均报错，不隐式选择其他控件。创建的 UIContext 必须由调用方 DisposeAsync，便利 API 不隐藏任务观察或自动销毁流程。

AttachToAsync / BindAsync / CreateContainerAsync 保留可等待契约，是因为挂载会执行可异步的 OnInitializeAsync，失败时还要等待清理；并非仅包装 Control 就必须异步。CreateContainerAsync 需要等初始化成功后才能返回新容器；AttachToAsync 与 Control 版 BindAsync 只直接转发核心任务。当前不新增仅适用于部分节点的同步 Attach/Bind 旁路，以免跳过生命周期或遇到异步初始化时隐式阻塞。

### 生命周期完整性审查（2026-10-02）

V1 审查发现前后阶段、原因、稳定激活和处理器组合缺失，且完成时序先于容器提交。V2 已重构这些职责，以下保留调研来源。

官方源码对照：[IPageLifecycleEvent](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/IPageLifecycleEvent.cs)、[IModalLifecycleEvent](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Modal/IModalLifecycleEvent.cs) 区分 Will/Did 与 Push/Pop；[ISheetLifecycleEvent](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Sheet/ISheetLifecycleEvent.cs) 提供 WillEnter/DidEnter、WillExit/DidExit。Will、Initialize、Cleanup 使用协程或可选 Task，Did 为同步 void。[Page](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/Page.cs) 还支持 AddLifecycleEvent/RemoveLifecycleEvent 和处理器优先级。此调研核对源码，未集成运行 UnityScreenNavigator。


## 7. V2 生命周期契约

本节记录 V2 实现选择，属于实际契约；用户已确认的约束优先。删除 V1 覆写点，不提供兼容层。未增加 OnInitialized/OnDisposed：初始化任务和销毁任务本身表达完成，暂时没有独立通知场景。

### 7.1 阶段与职责

| Node 覆写 | 处理器方法 | 职责 |
| --- | --- | --- |
| OnInitializeAsync(token) | InitializeAsync(node, token) | 每实例一次；取得引用、订阅和初始化子节点 |
| OnEnteringAsync(context, token) | EnteringAsync(context, token) | 每次显示前准备，之后执行进入转场 |
| OnEntered(context) | Entered(context) | 容器提交后的同步通知 |
| OnExitingAsync(context, token) | ExitingAsync(context, token) | 正常退出显示前准备，之后执行退出转场 |
| OnExited(context) | Exited(context) | 显示退出提交后的同步通知，不代表已销毁 |
| OnActivated(context) | Activated(context) | 有效业务激活恢复 |
| OnDeactivated(context) | Deactivated(context) | 有效业务激活失去 |
| OnVisibilityChanged(context) | VisibilityChanged(context) | 有效显隐改变 |
| OnDisposeAsync() | DisposeAsync(node) | 最终清理；包括部分初始化失败，不模拟正常退出 |

仅准备、转场、初始化与清理可等待。同步阶段没有 Async 镜像，普通同步处理器返回 Task.CompletedTask 即可。

### 7.2 上下文与状态

`LifecycleContext` 不可变，含 Node、Container（可空）、Reason、Source/Target（可空）和 IsFirstEnter。原因枚举为 Initialization、Open、Show、Hide、Push、Covered、Switch、Return、Close、Modal、Ancestor、Disposal；Show/Hide 在 V3 加入，不包含项目层级名称。IsFirstEnter 在本次上下文创建时取值；节点提交显示后 HasEntered 为 true，完成通知失败也不重置。首次 Tab 显示与再次显示均为 Switch，用 IsFirstEnter 区分。

`StateChangeContext` 含 Operation、Node、Previous/Current。直接参与操作的源/目标保留各自原因；后代因祖先变化使用 Ancestor；模态造成其他节点激活变化使用 Modal。初始化和销毁可无 Source/Target。

- IsVisible 是本地显示意图与祖先可见的合成结果，转场可有临时显示。
- IsActive 是已提交可见、本地激活意图、祖先激活和模态输入范围的合成结果，尚未提交的转场不改变业务激活。
- IsInteractive 进一步包含本节点和祖先的临时输入锁，表示后端实际输入资格。
- 临时输入锁不会发 Activated/Deactivated；稳定状态变化在提交点发布。转场期间显隐查询可能反映临时显示状态，Current/History 仍是此前提交状态。

状态通知先提交整棵树的有效状态快照，然后发通知：失去按后序（兄弟按挂载逆序），每节点先 Deactivated 后 VisibilityChanged(false)；获得按前序（兄弟按挂载顺序），每节点先 VisibilityChanged(true) 后 Activated。只通知实际变化。父隐藏不会重跑子节点初始化、Entering/Entered 或动画；未选中 Tab 不重复失活。结构性容器初始化完成可激活，Root 从构造时就可见/激活。

父 View 初始化中可以选中内嵌 Tab，其局部进入已提交，但有效可见/激活仍受尚未显示的父 View 限制；父 View 显示时才传播有效状态通知。

### 7.3 容器提交与失败边界

统一顺序：操作守卫 → 捕获模态原焦点（如适用） → 临时输入锁 → 前置回调与转场 → 检查取消 → 提交 Current/History/显隐与模态输入所有者/焦点 → 解除临时锁 → 状态变化通知 → 源 Exited → 目标 Entered → 解除守卫。通知中能查询提交状态和可交互性，但不能修改层级或重入操作。

Panel 独立打开/关闭，V3 增加单实例与隐藏恢复；Stack Push 将旧项退出并保留，新项入历史，Pop 移除栈顶并以 Return 恢复旧实例，PopTo 一次返回更早实例；Tab 切换既有项；Modal 覆盖旧项只导致失活，不重放旧项退出/进入。正常显示项 Close/Pop 先发 Exited，再销毁目标，外层任务等清理完成；隐藏窗口 Close 仅清理，不重复退出。

提交前失败或取消恢复框架状态并清理新实例，不发本次成功完成或临时显隐通知，不重执行业务准备；业务前置回调的外部副作用由业务负责。源实例先恢复，再清理失败新实例，避免清理发布临时隐藏。V4 的转场由 IUITransition.Create 创建独立 IUITransitionRun，执行对象以同步 Restore() 归还所修改的表现状态；不重播动画、不转成成功或换用默认转场。恢复异常作为附加原异常暴露，旧 V2 转场接口已删除。准备与并行动画的实际时序见 9.5。

提交后同步通知失败保持已提交状态，操作任务以原异常失败；不回滚、重试或重播。常规通知链停止，后续完成阶段不执行；异常日志与测试明确阶段。关闭流程仍清理已关闭节点。后端提交/恢复失败同样可观察，不能保证故障后端实际显示成功。

强制 Dispose：取消本节点操作并等待协作结束 → 隐藏/失活通知 → 逆序销毁子节点 → Node 清理 → 已开始初始化的处理器逆序清理 → 显示资源释放 → 从父移除 → Disposed。不调用 Exiting/Exited，不播放退出动画。直接销毁当前模态会归还输入所有者和焦点并通知恢复；销毁 Root 不会恢复已隐藏子树的激活。

强制清理中状态通知或清理失败继续必要步骤；首个原异常向上传播，其他原异常写入 UIFailure.GetSecondaryExceptions 并记录日志。每节点/处理器清理至多一次。根/父销毁仍需调用方观察之前启动的操作任务，不能遗失取消或原始失败。

### 7.4 独立处理器、顺序与重入

`INodeLifecycle` 是纯核心接口，`NodeLifecycle` 提供空默认实现。Node 继续可继承覆写。每阶段 Node 覆写优先，处理器按注册顺序；清理为 Node 优先、已开始初始化的处理器逆序。

```csharp
var page = new UINode();
page.AddLifecycle(new InventoryPreviewBehavior()); // 必须在 Attach/Bind 前
await tabs.BindAsync("Equipment", page, equipmentDisplay);
await tabs.SwitchAsync("Equipment");
```

AddLifecycle 同步，仅 Detached 可调用；重复实例注册、空处理器报错。初始化前已注册的处理器在 Node 初始化成功后依次初始化，开始执行就加入清理所有权，部分失败仍清理。Node 自身初始化失败时不启动尚未执行的处理器。

RemoveLifecycle 同步，禁止节点操作、初始化、派发和销毁中修改；缺失处理器报错。移除只停止后续通知，已初始化处理器的资源仍由节点最终清理，移除不异步释放、不补播过去阶段。处理器不应跨节点共享拥有资源的实例。

同步通知允许读取状态，禁止上下文内操作、注册、挂载、销毁或修改处理器；同容器前置阶段重入也直接失败。异步准备/清理中禁止销毁当前操作节点或祖先，避免等待自身。业务在生命周期结束后由外部事件显式发起下一操作，无自动队列。

### 7.5 Demo 的项目职责

Demo 是虚构冒险游戏 WAYFARER 的纯 UI 原型：营地主菜单、偏好、背包/材料、物品详情与收藏、独立藏品比较、设置/画质和嵌套确认。不含角色移动、战斗或其他 Gameplay。页面文本使用游戏业务语言；生命周期日志输出到宿主，计时表示本地物品预览更新，不作为专门诊断面板。GameUiState 保存项目 UI 会话偏好和收藏，独立于页面实例；本版不接入真实渲染设置或磁盘存档。

内嵌页面使用基础 Node；背包更新仅在选中页面有效激活时执行，详情覆盖、父隐藏及模态阻挡均暂停，恢复后继续。仅用于打印/计数的 PageActivity 已在 Demo 简化中移除，见 11.8。素材继续使用已本地化的 Adventure/Nunito 及原许可；本版没有新增外部素材。
## 8. V3 功能覆盖方向

### 8.1 版本方向（用户已确认）

V3 优先补齐功能特性，以覆盖绝大多数常见游戏 UI 管理需求为目标。此前提出的“业务接入与交互闭环”、控件绑定生成和减少编码，不作为 V3 的主要目标；易用性仍是实现约束，工具增强服从功能覆盖。

用户进一步指定：优先窗口与实例管理、完整栈流程；先参考其他成熟方案的处理方式，再决定具体设计。下面的调研结论和 API 建议不代表实现授权或已确认契约。

覆盖对象是 UI 管理：页面、窗口、弹窗、提示、输入、显示顺序、资源和作用域。列表虚拟化、布局、绘制、文本编辑、拖拽控件和 Gameplay 本身仍由引擎或项目实现，不能把管理框架的功能覆盖表述为完整控件库。

### 8.2 功能范围建议（尚未确认或实现）

本节为早期候选汇总；2026-10-02 重新审计后的可选清单、建议和延期项以 **8.8** 为准，不将本表的 P0/P1 直接转为 V3 范围。

| 优先级 | 功能组 | 建议范围 | 游戏场景 |
| --- | --- | --- | --- |
| P0 | 实例与显示管理 | 明确单实例/多实例打开策略；实例查询；显示、隐藏、再次显示；刷新参数/数据入口；批量关闭。重复请求的处理必须由显式策略定义，不隐式挑选实例或忽略错误 | 背包、地图、任务、商店反复打开；同时比较多个物品窗口 |
| P0 | 栈流程扩展 | 返回指定历史实例、返回根页、清空/重置历史；定义中间项的退出与销毁时序；Replace 暂缓，见 8.6 | 主菜单→存档→角色创建；商店多级界面；结算后回主菜单 |
| P0 | 显示排序与模态范围 | 项目定义容器显示顺序、窗口置前；明确多个容器的模态优先关系；全局与局部输入阻挡；遮罩点击关闭等项目可配置行为 | 聊天与背包共存；暂停菜单；加载界面覆盖其他窗口；局部选择弹窗 |
| P0 | 返回与焦点 | 顶层返回请求分发、业务拦截/消费、明确处理顺序；初始焦点、页面焦点记忆与恢复；适配键盘/手柄返回入口 | Esc 逐层返回；未保存设置确认；返回背包后继续选中原物品 |
| P0 | 资源与实例复用 | 根据真实加载需求评估可等待的加载、预加载、取消与释放；区分资源缓存和实例保留；隐藏保留由 Hide/Show 表达；关闭后复用采用第 24 节 KeepAlive；自动实例池暂缓 | 首次打开大型商店；高频背包；退出游戏会话后释放 |
| P0 | 项目作用域 | Root 下由项目组织菜单/游戏会话等子树；统一关闭和释放一个作用域、取消其未完成操作、限制跨作用域残留；保持全局项目 UI 的生命周期 | 主菜单进入游戏会话；返回登录界面；切换存档；全局提示持续存在 |
| P1 | 短暂与附着 UI | 在已有 Node/容器能力上验证限时提示、显式提示排队/替换规则、Tooltip 随目标失效关闭；位置和屏幕边缘约束由适配器处理 | 道具获得提示、错误提示、奖励通知、鼠标/手柄物品 Tooltip |
| P1 | 内嵌 Tab 完整管理 | 项查询、可用状态与绑定移除；当前项不可用时要求项目明确选择目标；内嵌资源继续为默认路径，不据此引入动态资源 Tab | 根据角色权限显示设置页；不同商店页签；页面内容改变后维护选择 |

以上功能需要先检查现有模型能否完整承载，存在职责缺陷时重构，不机械地在通用 Container 上添加全部操作。单实例策略不能替代多实例身份；实例复用不能成为失败兜底；资源缓存不等于实例池。多个容器的显示顺序、输入优先级和逻辑归属需要分别定义。

本表是需求调研候选，不是版本承诺；P0/P1 为此前建议排序，不代表所有项目需求已经核实。按场景驱动规范重新审视，本轮只优先确定窗口/实例与栈流程。排序、输入、作用域、资源和提示等其他组先检查现有实现与项目代码，明确缺口后再定范围。

### 8.3 验收方向建议

Demo 根据最终选中的能力扩展实际业务流程，优先复用现有背包、详情、设置和比较窗口。不预先承诺增加登录、地图、任务、商店、Tooltip、奖励与结算等整套界面；只有被选中的能力确需对应场景时才扩展。不含 Gameplay；每个场景必须对应框架能力和核心测试，不能只增加界面外观。

先研究并确定窗口/实例管理与完整栈流程，其他功能组后续安排。正式 V3 开发清单及 API 契约尚未制定。Source Generator 增强、编辑器、MVVM、Analyzer 与第二引擎适配器不作为本轮功能覆盖主线。

### 8.4 窗口、实例与栈流程调研（2026-10-02）

本次核对官方公开源码和 API 文档，未安装运行这些方案。GitHub 来源为核对当日 master；Epic 页面为当前在线 API 文档，不能据此宣称其他历史版本行为完全一致。

| 方案 | 核对到的处理方式 | 本工程可借鉴的部分 |
| --- | --- | --- |
| UnityScreenNavigator PageContainer | Push 创建新实例，以 pageId 维护有序历史；被覆盖的历史页保留；支持按数量或目标 pageId 返回，移除页最终清理、销毁并释放资源 | 历史保存实例、目标使用实例身份、批量返回只呈现当前源和最终目标的转场 |
| UnityScreenNavigator SheetContainer | Register 加载并初始化实例；Show/Hide 切换已注册项，Hide 不注销；UnregisterAll 才释放 | 实例存续与显示分离；但 Sheet 是互斥显示，不等于多个独立窗口共存 |
| FairyGUI Window | Show/Hide/BringToFront 操作具体窗口实例；隐藏不销毁，Dispose 独立；初始化与每次显示/隐藏回调分开 | 高频窗口隐藏后再显示，保留同一实例；并未自动保证同类型单实例 |
| QFramework UIKit | OpenPanel 默认 Single，也支持 Multiple；Single 找到已有实例后重新传入数据并显示；提供显示、隐藏、关闭及批量操作 | 单实例入口、重复打开时的数据更新需求、多实例必须有明确身份 |
| Unreal CommonUI | Stack 仅显示并激活栈顶；栈顶失活会移除并恢复前项；AddWidget 可创建或从 inactive pool 取实例 | 栈顶和历史的状态区别、实例池与业务历史分开定义；不直接复制其“失活即出栈”规则 |

来源：[USN PageContainer](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/PageContainer.cs)、[SheetContainer](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Sheet/SheetContainer.cs)、[FairyGUI Window](https://github.com/fairygui/FairyGUI-unity/blob/master/Assets/Scripts/UI/Window.cs)、[QFramework UIManager](https://github.com/liangxiegame/QFramework/blob/master/QFramework.Unity2018%2B/Assets/QFramework/Toolkits/UIKit/Scripts/UIManager.cs)、[CommonUI Stack](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/CommonUI/UCommonActivatableWidgetStack)、[CommonUI AddWidget](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/CommonUI/UCommonActivatableWidgetContaine-/AddWidget)。

需要避免的语义误读：

- USN 的 `Push(..., stack: false)` 标记新页在下一次 Push 时不保留，不是立即替换旧栈顶。例如先有 A，再推入不保留的 B，此时仍可返回 A；随后推 C 才移除 B，成为 A→C。[PagePushContext](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/PagePushContext.cs) 判断的是退出页此前的保留标记。真正 Replace 应独立定义。
- QFramework 的 [UIPanelStack](https://github.com/liangxiegame/QFramework/blob/master/QFramework.Unity2018%2B/Assets/QFramework/Toolkits/UIKit/Scripts/UIPanelStack.cs) 保存 PanelInfo；Push 关闭并移出管理表，Pop 根据资源、类型和数据重新 Open。不能把这种重开历史当作原实例恢复；本工程 V2 已采用实例历史。
- CommonUI 的池化是物理实例复用，不代表同类型业务单实例，也不保证保留上一轮业务状态。其 [AddWidgetInstance](https://dev.epicgames.com/documentation/unreal-engine/API/Plugins/CommonUI/UCommonActivatableWidgetContaine-/AddWidgetInstance?lang=en-US) 不参与池化，所有权仍归原创建者。本轮不据此引入自动实例池。
- USN Pop 启动后台清理协程，转场句柄不等待全部释放；其批量返回对被移除的隐藏项也执行退出回调。本工程要求清理失败可观察，且 V2 覆盖时已完成显示退出，不能直接照搬这两个时序。
- QFramework 部分 Show/Hide 对找不到实例不执行操作、部分查询采用首项或末项；本工程不得将它们作为缺失实例和多实例歧义的处理方式。

### 8.5 结合当前模型的建议（尚未确认或实现）

本节保留初步 API 讨论，具体名称和策略仍未定稿；本轮是否需要实现以 8.8 的场景审计及用户后续选择为准。

#### 窗口与实例

建议扩展现有独立窗口容器的职责，仍以普通 Node 为内容，不要求业务继承新的 Window/View 基类。是否把 PanelContainerNode 改名为 WindowContainerNode 待定。

将实例存续和显示状态分开：隐藏的实例仍归原容器管理，保持逻辑 Parent、子节点和已初始化状态；再次显示不重新初始化。Hide 执行显示退出和失活，Show 执行显示进入；Close 执行必要的退出并最终销毁。关闭已隐藏实例直接清理，不重复退出。容器销毁负责包括隐藏项在内的全部实例。

API 候选按场景区分，不增加 Navigation/Selection 中间对象：

```csharp
var inventory = await windows.OpenAsync<InventoryView>(); // 候选：取得或创建单实例并显示
await windows.HideAsync(inventory);                       // 保留实例与页面状态
await windows.ShowAsync(inventory);                       // 显示同一实例
await windows.CloseAsync(inventory);                      // 最终销毁

var left = await windows.OpenNewAsync<ItemDetailsView>(leftItem);
var right = await windows.OpenNewAsync<ItemDetailsView>(rightItem);
await windows.CloseAsync(left);                           // 操作具体实例，不按类型猜测
```

倾向显式入口区分取得/创建单实例与创建新实例，避免频繁传策略枚举；也可以采用注册时声明实例策略，最终选择待定，不同时引入互相矛盾的两套规则。单实例身份建议限定在容器内，不强制全局唯一；按类型、注册资源还是业务键识别仍需确定。同类型存在多个实例时，查询须返回集合或要求 Id/实例，唯一查询遇到歧义报错。构造、配置、查询保持同步。

重复 Open 的行为必须明确：隐藏实例可重新显示；已显示实例不能重新初始化或伪造一次退出/进入。携带新数据时如何刷新、是否置前，以及同一容器并发请求如何处理仍待定；不隐式丢参数、不静默跳过、不自动合并失败任务。刷新职责与初始化、显示生命周期分别定义，不直接照搬 UIKit 每次 Open 的回调时序。

建议提供集合查询和 CloseAllAsync；关闭完成等待所有目标清理。批量处理失败继续必要清理，传播首个原异常并暴露其他原异常，复用既有失败契约。实例保留由 Hide 表达，暂不同时加入 Close 保留策略和自动池化，避免多个 API 表达同一种行为。

#### 完整栈流程

建议保持 V2 的实例历史：Push 创建新页；覆盖页留在历史中；返回恢复指定原实例；移除项最终销毁。栈上的单实例复用与独立窗口复用不能直接混用，一个节点不能重复占据多个历史位置。

| API 候选 | 示例（左侧为栈底） | 语义建议 |
| --- | --- | --- |
| PushAsync<T> | [A,B] → [A,B,C] | 创建并推入新实例 |
| PopAsync / PopAsync(count) | [A,B,C,D] → [A,B] | 移除指定数量，恢复最后保留项 |
| PopToAsync(instance) | [A,B,C,D] → [A,B] | 返回具体 B 实例，销毁其上全部项 |
| PopToRootAsync | [A,B,C] → [A] | 返回最早历史页；这里的根页不是 UIContext.Root |
| ClearAsync | [A,B,C] → [] | 退出当前页、清理全部历史，保留可再次使用的容器 |
| ResetAsync<T> | [A,B,C] → [D] | 准备新页并一次提交新历史，不拆成先 Clear 后 Push |

返回目标建议使用实例或 Id；相同类型可多次入栈，不能按类型默认找到最近/最早项。目标缺失、归属不符直接报错；空栈 Pop、目标已是当前项正常完成（第 27 节）。

批量返回只让当前可见源页退出、最终目标进入；已被覆盖并完成 Exited 的中间页不补播进入/退出或转场，仅按栈顶向下清理。Clear 不恢复中间页；候选 Reset 先准备目标，提交前失败保持原历史并清理新实例；提交后清理失败不回滚已提交历史，任务仍失败。所有操作均等待其负责的清理，不启动无法由调用方观察的后台释放。

新增操作需要补充相应生命周期原因与 Source/Target 语义；上面是方向建议，不能把现有 Reason 枚举硬套为已实现支持。多页清理顺序、通知失败后的必要清理、取消边界要在正式开发清单中给出验收用例。

#### 本轮建议验证场景

场景须随用户选择的能力确定：窗口候选为背包隐藏后重开保留当前内嵌 Tab 与选中物品、重复打开不增加单实例、同类型两个详情窗口分别关闭；如选批量关闭，再验证隐藏窗口清理。栈候选为背包→详情→进一步查看后直接回到原背包；不默认要求 Reset 或新的角色创建流程。对选定能力的新页准备失败、返回目标缺失、多实例歧义和清理失败补核心测试。Demo 展示业务流程，不能以 API 按钮面板替代。

### 8.6 其他方案如何实现 Replace（调研补充，非已确认设计）

这里的 Replace 严格指 `[A,B,C] → [A,B,D]`：B 不暂时恢复，C 不留在返回历史中；新页准备失败时尽量保持 C。以下区分公开 API 可组合的流程与需要扩展框架的方案；没有运行验证这些组合。

- **UnityScreenNavigator**：核对的 PageContainer 没有直接 Replace 入口。顺序等待 `Pop(false)` 再 `Push("D", true)` 可以得到目标历史，但 Pop 仍会恢复 B 并调用其生命周期，关动画不能取消这些回调；Push 失败时 C 已移除。这是两个操作，不是一次替换。若 C 当初以 `stack: false` 推入，下一次推 D 会移除 C，可达成相同历史结果；已正常入栈的 C 不能通过给 D 设置 `stack: false` 追溯改变。任意栈顶的完整替换需要扩展源码，在一次操作中加载 D、转场 C→D、替换历史并清理 C，不能从外部修改只读历史或直接 Destroy C 绕过管理器。
- **CommonUI**：核对的容器公开 GetActiveWidget、AddWidget、RemoveWidget，没有独立 Replace 契约。基于这些 API 的组合建议是保存旧顶 C，添加 D，待 D 成为显示项且转场结束后移除已隐藏 C；结果可为 `[A,B,D]`。这是 API 层面的推导，具体版本的转场完成、重入和释放时序需集成验证，不能宣称自带事务替换。先让 C Deactivate 会自动出栈并恢复 B，不满足“不激活中间页”的要求；不可移除的 RootContent 也不能套用该组合。
- **QFramework UIKit**：其 UIPanelStack 保存已关闭页面的 PanelInfo，而不是当前显示项组成的完整实例栈。若当前页 C 尚未压入该历史，关闭 C 后直接打开 D、不要保存 C，便可保持此前返回记录。若已把 C 保存为历史，需扩展历史管理移除该记录。关闭再打开并非原子操作，D 创建失败无法自动保留 C；实例状态恢复也不是该栈的保证。
- **FairyGUI**：Window 是窗口显示能力，不是内置页面历史。项目可自行准备 D、隐藏/销毁 C 并显示 D；如果还需要 `[A,B,D]` 的返回关系，历史、转场协调和失败恢复均由项目的页面管理器负责，不能仅把窗口切换称为栈 Replace。

来源沿用 8.4 的官方源码，并核对 [CommonUI ContainerBase](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/CommonUI/UCommonActivatableWidgetContaine-) 的公开操作。上述记录说明技术上怎样组合或扩展，不作为本工程需要实现 Replace 的依据。

**需求审视（2026-10-02）**：用户指出多数被调研框架没有直接提供 Replace，质疑其有意义的使用场景。当前调研样本不能证明所有框架均不提供，也不能证明这种操作不存在场景；但本工程尚未找到足以将它列为基础 API 的具体业务需求。修订建议：从本轮优先范围与 Demo 验收中移除 Replace，保留调研记录，暂缓设计与实现。该修订是建议调整，不把用户的疑问记为永久禁止该 API 的确认决定。

已有场景优先按职责处理：设置→画质→返回设置使用 Push/Pop；同一详情面板切换物品更新数据或切换内嵌内容；登录完成或结算回主菜单评估整个流程的关闭/重置，而不是为了栈顶替换构造业务。后续若真实项目需要保留更早历史、移除当前项并直接进入新项，再验证是否有必要增加 Replace。Reset、批量返回等其他候选也应按真实业务需求和组合成本评估，不因“完整栈流程”而机械补全所有栈操作。

### 8.7 按实际游戏场景审视当前候选（建议，非开发清单）

| 场景与预期行为 | 现有能力与缺口 | 建议 |
| --- | --- | --- |
| 背包关闭显示后再打开，保留页签和选中项；重复点击入口不增加背包实例 | Panel 当前每次 Open 创建、Close 销毁，缺少容器管理的隐藏保留与单实例入口 | 优先设计窗口实例身份和 Hide/Show；不据此引入自动池化 |
| 同时比较两个物品详情，分别关闭 | 当前 Panel 已能创建多实例、按实例关闭 | 保留现有能力；是否新增 OpenNew 名称取决于单实例入口选择，不视为新的业务能力 |
| 设置→画质→返回设置，恢复原设置页 | 现有 Stack Push/Pop 已能表达 | 不新增 API；继续验证现有流程 |
| 多级商店页面直接返回原商品列表，保留列表状态 | 单次 Pop 已有，重复 Pop 会恢复中间页；缺少一次返回指定历史实例的流程 | 优先评估目标返回，跳过中间显示但完成必要清理 |
| 返回已在历史中的主菜单 | 可由返回指定实例表达 | 返回根页是否保留独立便利方法按调用易用性评估，不重复建立机制 |
| 离开一个 UI 流程，释放其页面 | 现有父节点 DisposeAsync 已能释放子树；保留容器并清空历史的需求需单独核实 | 区分作用域销毁与栈清空，不能直接推导需要两个新系统 |
| 结束流程后打开新的入口页 | 项目可销毁旧流程并打开新流程；尚未明确必须在同一栈内一次重置的需求 | Reset 保持待定，不作为已确认的本轮功能；先核实资源准备和失败保持要求 |

先验证已有业务场景，再为明确缺口安排实现和 Demo。Replace 继续暂缓；其余候选也不能仅靠命名、抽象栈示例或外部框架有同名功能获得必要性。

### 8.8 V3 重新审计与用户选择清单（2026-10-02，已选择 1、2、3）

依据当前 Containers、UINode、UIContext、Godot 适配器、Demo 业务代码及已有测试审查。本次是代码与文档审计，未运行新测试、未实现候选功能；V2 验证记录不算候选能力的验收。用户随后选择 **1、2、3**，范围已经确认；4—9 不纳入本轮。具体实现方案与 API 为 8.9 的建议，不能将范围确认等同于逐项 API 契约确认。

#### 建议优先选择

| 编号 | 功能 | 实际游戏场景与现有缺口 | 最小范围建议 | 取舍与工作量 |
| --- | --- | --- | --- | --- |
| 1 | 单实例窗口打开与识别 | 多次点击背包/任务入口应使用同一窗口；Panel 当前每次 Open 均创建新实例。项目虽能自己保存引用，但各入口须维护存续和归属 | 明确容器内单实例身份、重复打开行为；保留多实例路径；提供支撑该流程的同步查询，不建立全局索引/新句柄系统 | 建议纳入；中等。不能默认挑选同类型多实例中的某一个；重复打开的参数行为须定稿 |
| 2 | 窗口隐藏与再次显示 | 暂时关闭背包显示，再打开时保留原页签、滚动与选中项；当前 Panel Close 销毁，公共容器缺少 Hide/Show | 隐藏保留原 Node/子树，恢复同一实例；关闭仍销毁；隐藏/显示接入 V2 生命周期和失败恢复；容器释放包括隐藏项 | 建议纳入；中等。不增加自动实例池、Close 保留策略或默认缓存淘汰 |
| 3 | 一次返回指定历史实例 | 背包进入详情、再查看进一步信息后直接回到原背包；当前只能逐次 Pop，会恢复中间页 | 按实例/Id 返回；仅源与目标转场；移除项逆序清理；保留目标及更早历史 | 建议纳入；中等。先完成目标返回，不必同时增加按类型猜测、Pop(count)、PopToRoot 等多个入口 |

1 和 2 可以分别选择：单实例并不意味着关闭后永远保留，隐藏保留也不自动保证只有一个实例。窗口基本多实例创建/精确关闭已经存在，不能算新增交付。

#### 可选，按本轮需求取舍

| 编号 | 功能 | 实际游戏场景与现状 | 最小范围建议 | 审计建议与工作量 |
| --- | --- | --- | --- | --- |
| 4 | 清空栈但保留容器 | 离开一个多级菜单，清除本次页面；下一次仍使用场景中同一挂载容器。现有 Pop 可清空，但多次执行会恢复中间页；Dispose 可释放整个容器 | 一次清理全部历史，当前页正常退出，隐藏项直接清理；容器可再次 Push | 可纳入；中等。如果项目本来会销毁整个流程，现有 Dispose 即可，不需要 Clear |
| 5 | 批量关闭独立窗口 | 退出背包/商店交互时关闭所属详情与比较窗口，保留窗口容器 | 对容器当前管理实例执行正常关闭，包含隐藏项；等待清理并暴露全部失败 | 可选；中等。项目顺序关闭已能表达；父容器销毁已有。仅凭少写循环不足以建立更大关闭策略系统 |
| 6 | 窗口置前的直接入口 | 点击被另一张比较窗口遮挡的详情，将其移到前面 | 在容器上对所属实例进行同步置前；Godot 使用既有 MoveToFront | 可选；小。Element.BringToFront 已存在；项目可接点击事件。收益是调用便利，不是新增排序能力，不引入层级预设 |
| 7 | 页面进入焦点与栈返回焦点 | 键盘/手柄从背包物品按钮进入详情，返回时继续选中原按钮 | 明确项目指定初始焦点与历史页焦点恢复，复用 IUIHost；适配器负责真实控件 | 可选；中等。现有焦点交接主要用于 Modal，Stack/Tab 未实现同等恢复；不扩展为完整跨引擎控件导航系统 |
| 8 | Esc/返回键的 UI 处理 | 画质页按 Esc 回设置，设置首页按 Esc 关闭，确认框先处理最上层返回 | 引擎适配器接输入；项目明确处理目标，顶层弹窗优先；必要时提供最小处理契约 | 建议后续或本轮可选；中等。Demo 当前主要按钮调用；项目代码可以处理。要先决定自动分发是否必要，不引入路由器/通用命令系统 |
| 9 | Godot 实际异步资源加载 | 首次打开较大 UI 时让既有界面继续响应，取消时释放准备资源 | 保留核心加载器协议，改进 Godot 资源加载；场景实例化/挂载仍遵守引擎线程要求 | 建议后续；中等至较大。核心已支持等待加载器，Godot 目前同步加载；Demo 尚无加载卡顿的测量证据，不额外加入预加载/缓存预算系统 |

工作量为相对评估，包括正常流程、失败/取消和 Demo，不是工期承诺。7 如与 3 同选，目标页恢复焦点统一验收；4/5 如与 2 同选，必须验证隐藏实例清理；6 只在同一挂载范围内置前，跨容器显示层级仍由项目决定。

#### 已有能力，不计为 V3 新功能

- 多实例创建与按对象关闭：Panel OpenAsync/CloseAsync，Compare relics 已演示。
- 历史页实例保留、覆盖暂停与单步返回：Stack PushAsync/PopAsync，背包与详情已演示。
- 内嵌 Tab、基础 Node、字符串切换、项查询：Tab BindAsync/SwitchAsync/Items/Current；已绑定实例切换保留。
- 单个模态容器内嵌套弹窗、输入阻挡、结果与焦点恢复：Modal/ShowDialogAsync，设置和嵌套确认已演示。
- 父节点整树释放、操作取消和子资源清理：DisposeAsync；不因此再增加通用 Scope 系统。
- 基础身份与层级查询：Id、Name、Parent、Children、GetChild；集合过滤可由项目代码完成，1 的查询只补必要入口。
- 一般业务数据更新：业务 View 可提供普通同步方法或订阅模型；不要求统一刷新接口或重新初始化。

#### 本轮建议不纳入

| 候选 | 暂缓理由 |
| --- | --- |
| Replace、Reset、任意删除/重排历史、跨会话历史 | 没有当前业务必须依赖这些独立机制的证据；关闭流程、返回目标及项目代码应先评估 |
| 自动实例池、通用资源缓存/预热/淘汰系统 | 高频窗口先由隐藏保留解决；实际资源性能需求与释放边界尚未验证 |
| 跨容器排序策略、多容器模态调度、局部模态 | 本轮 Demo 使用一个全局 Modal 容器和项目挂载顺序；不能把现有单 InputOwner 当成多容器仲裁支持。出现相应项目需求再设计，不宣称已覆盖。HUD/Overlay 等默认层级预设仍按已确认要求禁止内置 |
| 新作用域类型或管理系统 | 已有节点子树与销毁能力；暂无必须新增独立类型的缺口 |
| Tab 可用状态系统、自动选择、动态资源 Tab | 查询与按名切换已具备；项目可决定显示哪些标签；新运行时增删需求未确定，不自动选替代项 |
| 通用 Tooltip/Toast/提示队列 | 此类 UI 有游戏用途，但显示与计时可由现有 Node、容器及引擎控件完成；本工程尚无明确公共缺口 |
| 统一 Refresh API、业务键单例、持久化窗口状态 | 普通业务方法和模型先满足数据展示；不因单实例打开一次性引入所有数据策略 |
| 新 Source Generator 功能、编辑器、MVVM、第二适配器、Analyzer | 服从已确认功能主线，诊断分析仍延期；不以工具数量替代功能缺口 |

#### 用户选择与交付要求

用户已选择 **1、2、3**：单实例窗口管理、窗口隐藏与恢复、返回指定历史实例。4—9 不纳入 V3；若后续调整，由用户另行指定。下节提出具体实现与 API 建议。

每项选中能力同时更新核心、适配器/生成调用（受影响时）、现有游戏 Demo、操作说明和适用测试；日志与原异常、生命周期提交边界、并发/取消和清理规则为既有要求，不作为另一个需选择的功能包。当前范围确认和方案制定不等于实现完成；具体 API 建议仍须与已确认范围区分，不新建未由用户指定的 V3 开发文档。

### 8.9 V3 窗口与栈 API 实际契约（已实现）

用户已授权开始实现 V3，本节方案已落实到核心、生成器与 Demo，取代 8.5 的 Open/OpenNew 初步建议。以下描述实际契约。保留 PanelContainerNode 名称，不新增 WindowNode/View 基类；窗口仍是普通 UINode，单实例只约束某个 Panel 容器，不是全局单例。不把窗口操作加入 Stack/Tab/Modal。

#### A. 注册决定窗口实例模式

已增加 `WindowInstanceMode.Single/Multiple`，作为节点注册描述的一个配置项；默认 Single。配置仅影响 PanelContainerNode.OpenAsync，Stack Push 和 Modal Open 继续创建新实例。同一注册类型在不同窗口容器中各有自己的单实例。

```csharp
// 手写注册；构造、注册均同步。
ui.Register<InventoryView>("res://Scenes/Inventory/Inventory.tscn");
ui.Register<ItemDetailsView>("res://Scenes/Inventory/Details.tscn",
    windowMode: WindowInstanceMode.Multiple);

// 使用已有生成注册工作流时，属性将同一配置传给 Register。
[UIResource("res://Scenes/Inventory/Card.tscn", WindowMode = WindowInstanceMode.Multiple)]
public class CardView : UINode<CardArgs> { /* 业务实现 */ }
```

统一 OpenAsync，不让同一类型通过 Open/OpenNew 两个入口混用实例语义。生成器仅迁移现有注册和调用模板，不新增 Analyzer、业务生命周期生成或新配置系统。现有比较窗口迁移为显式 Multiple；初始化和业务资源由项目决定。

#### B. 窗口 API 与参数

```csharp
// 公开签名；泛型方法约束 where T : UINode。
Task<T> OpenAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default);
bool TryGet<T>(out T? node);
Task HideAsync(UINode node, bool playAnimation = true, CancellationToken cancellationToken = default);
Task ShowAsync(UINode node, bool playAnimation = true, CancellationToken cancellationToken = default);
Task CloseAsync(UINode node, bool playAnimation = true, CancellationToken cancellationToken = default);
```

| 操作 | 实际行为 |
| --- | --- |
| Open Single，不存在 | 根据注册创建并初始化，准备进入，提交后返回新实例 |
| Open Single，已有隐藏实例 | 显示原实例，重新进入但不初始化、不替换子树 |
| Open Single，已有显示实例 | 验证访问、状态、取消和操作守卫后返回原实例；没有新的显示变化，不重播生命周期/动画、不自动置前 |
| Open Multiple | 每次创建新实例，业务保存返回对象以便操作 |
| TryGet<T> | 在当前容器中查找已提交、仍存续的注册类型 T 实例，含隐藏项；零个返回 false，一个返回 true，多个报歧义错误，不默认选一个 |
| Hide | 本地显示中的所属窗口退出显示、失活，保留实例和资源 |
| Show | 本地隐藏的所属窗口准备进入并恢复显示，不重新初始化 |
| Close | 显示项退出后销毁；隐藏项直接销毁，不重复 Exiting/Exited；均等待必要清理 |

TryGet 是明确的存在性查询，false 表示正常的“不存在”，不是异常兜底。多实例集合仍可用 Children，具体对象已有 Id，不新增句柄或全局查询系统。查询不能返回正在初始化/准备进入、尚未提交的新实例，也不能返回已从管理表移除但正在清理的实例。

参数明确作为 **创建参数**，不兼任刷新：非 null args 只用于新建，已有单实例时再次提供直接报错，不能忽略、自动比较、覆盖 Parameters 或隐式重跑业务方法。省略 args 或传 null 可重开已创建的参数化单实例，但首次创建缺少必需参数仍报错。V5 将核心 Open 统一为上述签名，生成器继续提供具体参数类型的创建入口及省略业务参数的重开入口。后续数据更新由业务 View 的普通方法/模型订阅负责；需要可等待更新时由业务明确提供异步方法。

```csharp
var inventory = await windows.OpenAsync<InventoryView>();
var same = await windows.OpenAsync<InventoryView>(); // 同一对象，不是第二个窗口
await windows.HideAsync(inventory);
var restored = await windows.OpenAsync<InventoryView>(); // 自动显示原对象
await windows.HideAsync(restored);
await windows.ShowAsync(restored); // 已持有引用时直接恢复
await windows.CloseAsync(restored); // 释放；下次 Open 创建新对象

var left = await windows.OpenAsync<CardView>(leftArgs);   // Card 注册为 Multiple
var right = await windows.OpenAsync<CardView>(rightArgs);
await windows.CloseAsync(left); // 不影响 right
```

Hide 已隐藏、Show 已显示正常完成；操作其他容器节点或已销毁目标仍报错（正常关闭后的重复 Close 除外）。判断使用窗口本地已提交显示状态，不用有效 IsVisible 推断：父隐藏不意味着窗口已执行 Hide；窗口本地隐藏时，即使父恢复也不得自动显示。Open 已显示单实例的“取得现有对象”属于明确成功语义，不是忽略非法状态；并发结构请求按提交顺序排队，同一次打开的重复 Close 共享执行结果（第 27 节）。

#### C. 窗口内部实现与生命周期

注册类型作为容器局部身份；注册描述与创建流程需保留类型键及 WindowMode，不按实例 GetType、资源路径、名称或类型继承猜测。维护仅包含已提交窗口的内部表，记录创建注册键和本地显示状态；Single 类型附加唯一索引。Children 继续表示逻辑归属，表表示由窗口操作管理的已提交实例，不维护另一棵层级树。

新建在进入准备成功后发布实例记录；提交后通知失败时记录保留，使后续查询/关闭仍可找到已提交实例。Hide/Show 修改显示记录及本地节点状态，不改变 Parent、Id、Lifetime 或已初始化处理器；Close 在提交时移除记录，然后等待清理。直接强制 Dispose 也经 OnChildRemoved 清除索引，容器 Dispose 继续处理全部子节点，包括隐藏项。

新增生命周期原因为 Show/Hide；首次 Open 使用 Open，已有隐藏实例经 Open 恢复使用 Show。Hide：Exiting(Hide)→退出转场→提交隐藏→状态通知→Exited(Hide)。Show：Entering(Show)→进入转场→提交显示→状态通知→Entered(Show)。首次进入通过 IsFirstEnter 判别。隐藏项 Close 不重复显示退出，只执行最终销毁协议。

全部复用 V2 前置准备、提交、状态通知和失败边界：提交前取消/失败恢复本地状态和转场表现，不重新创建替代实例；提交后通知失败保持已提交状态、原异常向上传播。Close 已提交后通知失败仍清理目标，不留下已移出管理表的资源。不直接调用 Element.SetVisible 绕过核心激活/可见传播。

#### D. 栈返回 API 与实现

```csharp
Task PopToAsync(UINode target, CancellationToken cancellationToken = default);

var inventory = await stack.PushAsync<InventoryView>();
await stack.PushAsync<ItemDetailsView>(itemArgs);
await stack.PushAsync<ItemPreviewView>(previewArgs);
await stack.PopToAsync(inventory); // 恢复原背包，清理详情和预览
```

仅增加实例目标入口，不同时提供 Id/类型/名称/数量/根页重载。历史已有实例引用与 Id，项目如持有 Id 可先在 History 显式解析。目标必须属于当前栈、Ready 且仍在历史；当前目标正常完成，缺失、外部和已销毁目标仍报错。

实施步骤：

1. 在同一操作守卫内验证目标，捕获源、目标及从栈顶到目标之上的移除项列表。
2. 当前源执行 Exiting(Close)，目标执行 Entering(Return)，只对它们播放转场；隐藏中间项已在 Covered 时退出，不重复显示生命周期。
3. 检查取消后一次提交历史截断和源/目标本地状态，Current 为 target；发布 V2 状态变化与源 Exited、目标 Entered。目标及更早历史保留原实例与业务状态。
4. 按原栈顶向下销毁全部移除项；方法等待清理完成。即使完成通知或某项清理失败，也继续必要清理；首个原异常传播，其余原异常通过 UIFailure 暴露。

提交前失败恢复源显示、目标隐藏及原历史；提交后失败不回滚截断历史。提交后清理不因调用方取消而遗弃；转场成功的状态已生效，但清理失败仍使任务失败。实际显示资源的最终物理释放继续遵守 Godot QueueFree 的帧末语义。

重构现有 PopAsync 与 PopToAsync 共用内部返回实现，不将 PopTo 包装成循环 Pop。保留单步 Pop 最后一项可返回空栈的既有行为；这是内部共享时需支持的边界，不额外暴露 Clear API。生命周期内重入继续禁止，不引入并行动画或操作队列。

#### E. 实施与验收方案

实施顺序：注册描述及生成模板迁移→窗口已提交实例表与查询→单实例 Open→Hide/Show/Close→共享栈返回流程与 PopTo→业务 Demo 与回归。所有阶段遵守无兼容层要求，变更旧 Open 默认语义时同步迁移所有调用。

Demo 复用现有界面：使用可隐藏恢复的单实例背包窗口外壳，内部 Stack 管理背包页及多级查看页，背包页继续包含内嵌 Tab；隐藏外壳保留完整子树，PopTo 返回内部原背包页。窗口实例与栈页是不同节点，不把同一个对象同时挂到两个容器，不向 Panel 添加栈操作。比较藏品继续 Multiple。分别验证窗口恢复与栈返回时的页签/选中项状态，不增加 Gameplay、API 按钮展台或新的全套菜单。

核心验收包括：同容器重复打开为同对象，不同容器各自单实例；隐藏后恢复不加载/初始化；关闭隐藏项不重复退出；父隐藏与本地 Hide 的区别；传参重开严格报错；多实例查询歧义；查询不发布未提交实例；直接销毁移除索引；PopTo 不进入中间页、保留目标及选中状态、按序释放全部移除项；前置失败/取消保留原状态，提交后通知/多项清理失败不丢资源和原异常。同一场景对应 Godot 实际验证；未运行部分如实记录。

注册配置、Open 的重开参数限制、TryGet 的精确注册类型语义、Show/Hide 严格状态及 PopTo 的目标边界均已实现并验证。未添加兼容层、OpenNew、Clear、Reset 或 Replace。TryGet 的 true 分支通过 NotNullWhen 注解表达非空结果。

### 8.10 V3 交付与验证记录（2026-10-02）

已完成用户选择的 1、2、3：注册控制 Single/Multiple；窗口已提交实例查询、重复打开、隐藏恢复与关闭；实例目标 PopTo，以及共享的单步返回实现。旧 Panel 默认每次新建已迁移为默认 Single；比较窗口和原多实例测试明确配置 Multiple。生成器支持属性 WindowMode 和参数化窗口的无参重开调用，核心、Demo 与编译测试同步迁移，无兼容入口。

Demo 的背包窗口外壳、内嵌背包 Tab、详情、物品手记组成实际游戏 UI 流程：重复打开不重建，收起再打开恢复当前历史及页面选择，从手记直接回到原背包。窗口释放和再次创建通过自测验证；普通界面不增加技术清理按钮。仍不含 Gameplay，不新增 4—9 或此前暂缓的机制。

素材：用户再次明确可自行从网络补齐所需素材；本版新增资源复用已本地化的 Adventure/Nunito，未下载新素材，原来源/许可见 Demo/Assets/SOURCES.md。以后实际新增时继续记录可分发许可、来源与必要署名。

验证环境沿用 Windows 11、.NET SDK 10.0.400、Godot 4.7.2 .NET：

- 解决方案与 Demo 构建 0 警告、0 错误；核心/生成器 **59 项测试通过**，包含 18 项 V3 窗口/栈专项测试，以及更新的生成结果编译测试。
- Godot **headless 与原生 OpenGL 自测均通过 9 场景**，包括重复打开、实际控件不克隆、隐藏子树暂停/恢复、PopTo 跳过中间页及隐藏项关闭后重建。
- **12 个独立故障用例均退出 1** 并暴露异常日志：原 8 项加 window-parameters、window-state、stack-target、return-cleanup。测试夹具中的预期取消与原始失败仍打印日志，不伪造运行成功。
- 原生背包与物品手记截图已目视检查；修正新增外壳后页签标题重叠和检查按钮越界，紧凑布局保持原素材风格。截图和故障日志位于被忽略的 artifacts，不纳入 Git。

未验证其他引擎、导出平台、所有分辨率和实体手柄。Godot 加载器仍同步加载，QueueFree 物理释放仍在帧末；隐藏实例持有资源直到显式关闭或父节点销毁，不宣称自动内存回收或实例池。本记录仅覆盖当前代码和上述场景，不把功能覆盖描述为已经满足所有游戏项目。

## 9. V4 候选范围与 USN 对照

### 9.1 依据与状态（2026-10-02）

用户要求制定 V4，并明确可以参考 UnityScreenNavigator 补齐功能特性。用户已选择 **1、2：完整转场能力、模态遮罩管理**；3—6 不纳入 V4。用户随后已授权开始实现；具体 API 与执行契约现已落实，见 9.5、9.6；不把 V3 未选项自动纳入 V4，不创建未经用户指定的独立 V4 开发文档。

本轮核对 USN 官方 README 与当前工程代码，未运行 Unity 集成。参考：[转场](https://github.com/Haruma-K/UnityScreenNavigator#screen-transition-animation)、[生命周期](https://github.com/Haruma-K/UnityScreenNavigator#lifecycle-events)、[资源加载](https://github.com/Haruma-K/UnityScreenNavigator#loading-screen-resources)、[其他功能](https://github.com/Haruma-K/UnityScreenNavigator#other-features)。README 是功能依据，不视为每项内部失败语义的源码验证；实施选中项前还须核对相关源码。

| USN 能力 | 本工程现状 | V4 判断 |
| --- | --- | --- |
| Push/Pop、多页返回 | Stack 已有 Push/Pop/实例目标 PopTo | 已覆盖主要场景，不追加数量/Id/类型重载 |
| Sheet 切换与状态保留 | Tab 绑定内嵌实例，Panel 隐藏恢复 | 已有对应能力，不复制 Sheet 基类或独立资源默认工作流 |
| 前置/完成生命周期及外部处理器 | V2 已实现，V3 增加 Show/Hide | 不重复建设；容器操作观察与节点生命周期需区分 |
| 通用、单界面及伙伴界面相关转场、绘制顺序 | 每 Node 可设置 IUITransition；仅收到 element、entering，Godot 只有 Fade | 优先补齐原因/源目标上下文与易用动画配置 |
| 遮罩资源覆盖、点击关闭及多弹窗遮罩策略 | Demo 各弹窗自带 ColorRect；核心无遮罩管理 | 优先统一遮罩的归属、排序、嵌套与业务关闭请求 |
| 可替换资源加载及预加载/释放 | 已实现可替换 loader、Godot 同步资源加载及独立资源持有凭据 | 实际契约见 10.4；区分资源持有和节点实例保留 |
| 不加入返回历史的临时页 | 当前每次 Push 都保留历史 | 仅在确有临时页面流程时考虑，不借此恢复 Replace |
| 容器操作前后回调 | 节点有上下文和处理器，无独立容器操作观察入口 | 可选；需先证明节点处理器/项目协调代码不足 |

USN 还公开转场期间交互控制及动画进度等选项。本工程已有输入锁、操作守卫与失败恢复；不为了选项数量新增全局配置矩阵、进度诊断系统或允许生命周期重入。

### 9.2 已确认 V4 范围：1、2（方案说明，实际契约见 9.5、9.6）

#### 1. 转场能力与配置

实际场景：背包→详情与详情→背包使用相反的滑动方向；设置窗口淡入，确认弹窗缩放出现；项目统一设置默认动画，个别界面覆盖。当前 PlayAsync 只有 entering，无法从其参数得知 Push/Return/Switch 或伙伴节点，业务需为动画额外保存这些信息。

建议重构 IUITransition，以不可变转场上下文传入节点、容器、源/目标、生命周期原因和进入/退出阶段；优先复用现有上下文信息，不建立第二套历史。配置使用同步 API，执行和取消仍可等待。Godot 适配层提供可直接使用的淡入淡出、滑动、缩放及引擎动画接入；核心只定义流程和契约，不实现 Tween、坐标或引擎动画资源。

默认配置与节点覆盖应有明确优先级。默认 Instant 是主动配置，不是失败后的降级。V4 不做伙伴名称正则匹配、动画组合语言或通用 Timeline 编辑器。绘制先后按具体转场需要处理，不内置项目层级枚举；Godot Container 的布局约束需在滑动实现中考虑，不能直接移动受布局控制的根而宣称通用。

实际采用 `node.Transition = ...` 直接配置；`IUITransition.Create(TransitionContext)` 创建独立执行对象，执行对象的 PlayAsync/Restore 替换旧契约。提交前失败/取消恢复动画实际修改的透明度、位置、缩放及临时排序；恢复失败保留原异常。V4 并行播放源/目标动画，不开放同容器操作并发或操作排队。

Demo：详情前进/返回、内嵌 Tab 切换、设置与确认弹窗分别体现适合业务的动画。验收原因和源目标正确，PopTo 不动画播放中间页，取消/失败恢复原表现，输入解锁与生命周期提交时序一致。

#### 2. 模态遮罩管理

实际场景：设置窗口打开半透明遮罩，再打开确认弹窗；项目希望嵌套确认期间背景暗度固定，也可以明确选择逐层加深；点击设置外部可关闭，确认框外部是否取消由业务决定。当前 SettingsWindow 和 Confirm 各带遮罩，嵌套默认叠加且颜色/点击接入分散。

建议由 ModalContainer 管理独立遮罩显示对象，适配器负责创建、挂载、排序、点击和动画；弹窗内容资源不再重复自带全屏遮罩。单个容器支持明确配置的共享遮罩或每层遮罩，先覆盖这两个实际行为，不复制 USN 所有策略开关。共享遮罩位于当前弹窗下方，打开/关闭/直接销毁/失败恢复均保持正确的所有权和排序。

遮罩点击只是当前弹窗的关闭请求，项目决定执行 CloseAsync、Dialog.Complete(false) 或不允许关闭；不能统一把 Dialog 强制关闭而令等待者收到技术取消。未开启点击关闭时正常阻挡输入，并非吞异常。没有默认 HUD/Overlay 节点；遮罩是容器管理的显示资源，不是项目层级预设。渲染无关核心不包含 ColorRect、颜色和纹理。

API 方向：容器同步配置遮罩提供方及共享/逐层方式；业务声明外部点击行为，异步请求必须由调用方或适配入口观察失败。具体名称及是否需要纯核心遮罩接口待定，不能为缩短 Demo 初始化把 Godot 类型塞入核心。

本项要明确单模态容器的嵌套输入协议。多个模态容器的仲裁是当前另一个缺口，不能宣称本项自动支持；若选择同时解决，需单列真实使用场景和范围。Demo 验证设置→确认→恢复设置、遮罩不重复变暗、点击结果语义、输入阻挡、焦点恢复，以及遮罩加载/转场/释放失败的原异常和必要清理。

### 9.3 V4 后仍延期的候选

**3. 后台资源加载（当前未实现）。** 资源预加载和显式持有已在 V4 实现；Godot 默认 loader 当前在场景线程同步调用 `ResourceLoader.Load<PackedScene>`，`LoadAsync` 返回已完成的 Task。Godot 4.7.2 .NET 的线程加载路径在 Demo 退出时产生 RefCounted 泄漏警告，因此本次改回同步加载。同步加载期间不能中断；若此时收到取消，会在加载返回后观察并传播取消。只有出现可复现的加载卡顿，并能在目标 Godot 版本验证无泄漏后，才重新评估后台加载；实例化和场景树操作仍留在场景线程。不默认增加 LRU、内存预算或实例池。

**4. 容器操作观察。** 项目侧面包屑/标题需要一次观察源与目标时可评估；目前节点处理器已有 Source/Target，能完成的场景直接使用现有机制。若确有公共缺口，只增加提交前/后明确观察契约，不把观察者变成隐式导航控制器或错误包装器。

**5. 临时页不保留历史。** 适用于加载/连接等完成后不应返回的独立页面；当前 Demo 没有该流程，且许多加载表现可以作为临时覆盖完成。先判断覆盖是否足够；只有页面确实属于同一栈时，才考虑 USN 式 Push 的历史保留选项。移除时点及清理规则必须明确，不等同 Replace，不作为本轮默认范围。

**6. 返回请求和页面焦点。** 从当前 Demo 可直接验证：Esc 关闭顶层确认或退回设置/背包详情，返回后继续选中原物品。当前只有 Modal 捕获/恢复焦点，Stack/Tab/Panel 未协调页面焦点。属于本工程自身缺口；本轮核对的 USN 文档没有提供其对应通用返回/焦点契约的依据，不能说是照搬 USN 功能。建议单独迭代或由用户选择纳入，不与动画/遮罩顺带捆绑。

### 9.4 范围边界与交付要求

用户已确认 V4 只包含 1、2。预加载与显式资源持有随后已实现；Godot 后台资源加载仍延期。容器操作观察、临时页历史选项、返回请求与页面焦点均延期；工具增强、编辑器、Analyzer、Replace、自动实例池、全局路由和项目默认层级继续不纳入。用户随后已授权实施，核心、适配器、Demo 与测试已同步迁移，交付见 9.6。

建议实施顺序：确定转场上下文、配置优先级和表现恢复协议 → 重构核心转场契约与 Godot 动画适配 → 确定遮罩提供、共享/逐层、点击请求及所有权协议 → 实现模态遮罩管理 → 迁移 Demo 弹窗资源并补齐业务流程与故障验证。整体遵守无历史兼容要求，不保留旧转场接口或资源自带遮罩的双轨默认行为。

选中项实施前确定最小 API 与失败边界；实现同步更新适配器、受影响生成调用、游戏 Demo、操作说明和测试。验收包括实际业务流程及取消/故障路径；多容器仲裁、实体手柄、性能和导出平台未验证时如实标注，不把一个 Godot Demo 等同所有引擎适配完成。

### 9.5 V4 转场使用与执行契约（已实现）

用户要求从使用者角度明确第 1 项的实现与调用方式。建议保持 PushAsync/PopAsync/PopToAsync/SwitchAsync/OpenAsync 等业务入口不变，动画通过同步配置自动参与这些操作，调用方 await 同一个操作即可等待准备、动画、提交及必要清理，不另调用 Play。

#### 配置与常用调用

实际 API（GodotTransitions 属于 Godot 适配器，非纯核心）：

```csharp
ui.DefaultTransition = GodotTransitions.Fade(duration: 0.15);
inventoryStack.ChildTransition = GodotTransitions.SlideHorizontal(duration: 0.20);
settingsBody.ChildTransition = GodotTransitions.Fade(duration: 0.12);
modals.ChildTransition = GodotTransitions.ScaleFade(duration: 0.16, fromScale: 0.94);

await inventoryStack.PushAsync<ItemDetailsView>(itemArgs);
await inventoryStack.PopAsync();
await settingsBody.PushAsync<GraphicsView>();
await tabs.SwitchAsync("Materials");
```

优先级为节点显式 Transition → 直接管理容器的 ChildTransition → UIContext.DefaultTransition；Context 默认 Instant。Node/Container 未配置表示明确继承配置，不是异常后的兜底；显式 Instant 禁用本节点动画。ChildTransition 仅针对该容器直接管理项，不穿透到后代容器，不改变容器自身进入/退出；父窗口动画带动资源子树，但不重播所有子节点生命周期与动画。DefaultTransition 是项目级动画配置，不创建默认项目节点。

单界面覆盖放在构造或初始化中，在首次进入前完成：

```csharp
public SettingsView()
{
    Transition = GodotTransitions.Fade(duration: 0.18);
}

public ConfirmView()
{
    Transition = GodotTransitions.ScaleFade(duration: 0.14, fromScale: 0.96);
}

public StaticPanelView()
{
    Transition = new InstantTransition();
}
```

配置变化只影响后续转场；进入/退出准备前捕获所用配置，准备与动画中不替换正在执行的配方。参数非法直接报错。配方可以复用，但不能在配方实例上存当前 Node、Tween 或动画前状态，防止多个容器或实例共享配置时互相污染。

#### 前进、返回与显示对象

Stack Push：源向左退出、目标从右进入；Pop/PopTo：源向右退出、目标从左进入。初次 Push 无源，仅播放目标进入；最后一次 Pop 无目标，仅播放源退出。PopTo 只涉及当前页与目标页，中间历史项直接清理。方向由容器操作提供，不根据类型名、资源名或调用方维护的布尔值猜测。SlideHorizontal 是栈前进/返回预设，非栈使用需要固定方向的滑动预设或自定义动画；Tab 默认示例用 Fade，不自行推断页签顺序。Modal 打开只动画新弹窗，底层弹窗仅失活，不强制离场；Panel Hide/Show 分别退出/进入，已显示 Single 的重复 Open 不重播。

Godot 默认目标为 Node 的显示对象；滑动/缩放若根由布局 Container 控制，项目资源提供独立的内容包装层。候选 `targetPath` 参数可指定该包装层，例如 `GodotTransitions.ScaleFade(duration: 0.16, fromScale: 0.94, targetPath: "Window")`；相对 Node 显示根解析。路径缺失/类型不匹配报错。其他引擎提供自己的配置入口，核心不解析引擎控件路径。窗口内容与模态遮罩独立动画，不能把遮罩一起缩放。

#### 执行与自定义扩展

核心提供不可变 TransitionContext，包含当前 Node/Element、Container、Source/Target、生命周期原因、进入/退出阶段和容器给出的方向；尽量复用 LifecycleContext，不重复维护导航状态。IUITransition 表示可复用配方，以同步 Create(context) 创建 IUITransitionRun；执行对象提供 PlayAsync(token) 和同步 Restore()，Restore 同时归还表现并清理播放状态。逐次状态由执行对象持有，不能在共享配方内保存。

正常业务只使用预设；特殊动画实现转场配方与执行对象即可，取得源/目标后可控制伙伴相关效果，业务仍调用容器 API。Godot AnimationPlayer 接入使用指定进入/退出动画及明确的表现恢复约定；不能宣称框架能自动恢复任意脚本/音频/方法轨道副作用。动画查找失败直接报错，不退回 Fade；标准预设负责保存其修改的颜色/位置/缩放等原值。

V4 已将旧“准备退出并播动画，再准备进入并播动画”重构为：操作守卫和输入锁 → 源退出准备 → 目标进入准备 → 建立各自执行对象并捕获原表现 → 源/目标动画同时播放 → 全部完成后一次提交 → 解锁并发送完成通知 → 必要清理。两项的准备仍依序执行；动画重叠不开放容器操作并发。转场临时绘制顺序由容器处理：Push 目标在前，Return 源在前；与最终排序区分，失败恢复原排序，不添加全局层级预设。

任一动画失败/取消，取消另一项并等待其结束，恢复所有已开始执行项的表现及原框架状态，再传播原异常；其他动画/恢复/清理异常同样记录并可获取。一次调用中的所有动画任务必须被观察，不能只等待 Task.WhenAll 而丢失其余原异常。操作取消之后的恢复和必要清理不被同一取消令牌跳过。成功后也归还临时动画状态，不能让入场偏移成为下一次基线或覆盖原 Modulate 颜色；退出项在恢复动画基线前临时隐藏，随后由容器统一提交最终显隐。

本节记录用户授权实施后的实际契约，取代此前建议。Source Generator 调用入口未变；旧 IUITransition.PlayAsync(element, entering, token)、Restore(element, visible) 与 GodotFadeTransition 已删除，工程内所有实现同步迁移。

### 9.6 V4 模态遮罩实际契约与交付记录（2026-10-02）

用户已授权实现 V4 的 1、2，两项均已落实。遮罩配置为同步 `ConfigureBackdrop(IUIBackdropFactory factory, ModalBackdropMode mode = Shared)`，要求没有打开项且不在操作中；Shared/PerModal 两种模式。`CurrentBackdrop` 查询当前项的遮罩。未配置提供方的纯核心可以管理无显示的逻辑模态，不自动创造后端显示；Godot 项目显式接入默认可用提供方：

```csharp
modals.ConfigureBackdrop(GodotBackdrops.Dim(Track), ModalBackdropMode.Shared);
// 或明确使用 PerModal；Dim 可配置颜色与独立的淡入/淡出时长。
```

IUIBackdropFactory.CreateAsync(mount, requestDismiss, token) 返回容器独占管理的 IUIBackdrop。提供方负责失败前尚未移交对象的释放；成功返回后容器负责显示、排序和销毁。IUIBackdrop 提供 ShowAsync/HideAsync、同步 Restore(visible)，以及 IUIElement 显隐、输入、排序与 DisposeAsync 契约。Godot Dim 是 owned ColorRect，不是逻辑层级中的项目节点。弹窗资源移除全屏遮罩，仅保留内容；根和全屏布局允许鼠标穿透，内容面板阻挡输入，动画指定 Center 包装层。

Shared 首次打开创建并淡入一张遮罩；再次打开复用并放到当前弹窗下、旧弹窗上，不重复加深背景。关闭顶层时遮罩重新排到恢复项下，最后一层关闭才淡出并释放。PerModal 每层独立创建，排序为遮罩/内容交替，关闭只释放该层。强制节点/容器销毁也等待必要遮罩清理；内容清理失败仍释放遮罩。物理释放沿用 Godot QueueFree 帧末语义。

遮罩动画与窗口动画独立并行（2026-10-07 更新）：先完成窗口资源创建、初始化、遮罩创建与窗口进入准备，再启动新遮罩和窗口入场动画；关闭时先完成退出准备，再并行播放窗口与需要移除的遮罩退出动画。共享遮罩仍被下层弹窗使用时不退出，嵌套开关不重复播放遮罩动画。整个操作等待两侧结束才提交输入／焦点或销毁资源。任一动画失败时沿用转场取消协议停止另一侧，等待两侧收尾，恢复原状态并传播首个原异常，其他失败按 UIFailure 契约附加。playAnimation:false 对窗口和遮罩一致生效，不新增公开动画编排 API。

并行时序验证（2026-10-07）：核心／生成器 173 项通过，新增 8 项覆盖准备完成后启动、两侧完成前不提交／释放、窗口或遮罩失败后的同伴取消与收尾、调用取消及容器销毁。原有 Shared／PerModal、关闭返回、结果等待和无动画流程继续通过。两端共用核心实现，Demo 操作说明与中英文指南同步；全仓库链接／锚点、32 组双语示例检查通过。本轮未启动 Unity／Godot，未新增原生动画验收结论。

`UINode.DismissRequested` 为可选 `Func<CancellationToken, Task>` 业务处理器。普通设置配置为 CloseAsync(this, cancellationToken: token)，Confirm 配置为 Complete(false) 并返回已完成任务。`RequestDismissAsync(token)` 返回 Task<bool>：处理器缺省是明确的“不允许外部点击关闭”，返回 false；已处理返回 true；不存在模态、失去输入、操作中或重复并发请求直接报错。异步业务失败原样传播，不转换为 false。该入口只用于模态外部点击，不扩展为未选择的 Esc/返回路由系统。

GodotBackdrops.Dim 要求提供 `Action<Task>` 观察入口。Demo 使用已有 Track，任务由应用持有，在帧入口直接读取已完成任务的结果，原始异常交给 Godot 报错，不手工捕获、包装或强制退出；即使遮罩已 QueueFree 也不丢弃任务。没有 async void、后台任务或遮罩自身销毁时停止观察的机制。没有处理器时遮罩仍拦截鼠标，不穿透背景。

模态准备与转场期间使用计数式上下文临时输入锁，阻挡背景键盘/鼠标而不提前改变 IsActive；在提交或失败结束时归还，容器单独销毁导致操作取消也归还锁。稳定 InputOwner 与生命周期仍在提交点变化。普通 Stack/Tab/Panel 继续只锁其操作容器。祖先显隐与临时输入锁传播到遮罩显示与点击资格。不同模态容器同时争用输入尚无仲裁协议，在捕获焦点/加锁前与提交前都明确检查并报错，不覆盖另一个容器的输入所有者。

IUIElement 增加 CaptureOrder/RestoreOrder，捕获不透明排序快照，Godot 使用兄弟索引；所有后端实现须同步迁移。转场临时排序在成功或失败后恢复，当前模态最终排序由容器负责。节点移除增加可等待的后端附属资源清理阶段，不丢弃遮罩 DisposeAsync 任务。所有附加清理原异常仍通过 UIFailure.GetSecondaryExceptions 暴露。

Demo 已迁移：项目默认 Fade；背包详情/手记栈 SlideHorizontal 自动反向返回；设置内部页面 Fade；设置/确认 ScaleFade 只作用于内容包装层。设置外部点击关闭，确认外部点击为正常取消；日常演示默认共享遮罩，自测同时覆盖 PerModal。资源与素材复用原 Adventure/Nunito，无新增下载或 Gameplay。

验证：构建 0 警告/0 错误；核心与生成器 **86 项测试通过**（V3 的 59 项 + 27 项 V4 用例）。Godot headless 与原生 OpenGL 自测 **12 场景通过**；适配器专项断言覆盖滑动方向、原 tint/alpha/位置/缩放/pivot 恢复、取消、AnimationPlayer 接入；模态专项覆盖共享/逐层排序、真实 GuiInput 信号回调、正常 false 结果、设置关闭、强制释放与焦点恢复。这里的 GuiInput 回调验证不等于完整的实体鼠标输入设备测试。

**15 个独立故障夹具均退出 1 并记录异常**：原 12 项加 transition-target、backdrop、backdrop-dismiss。设置与嵌套确认原生截图已目视检查，顶部内容清晰且共享遮罩只加深一次背景；截图与日志在被忽略的 artifacts，不入库。未验证全部分辨率、实体手柄、导出平台或其他引擎；AnimationPlayer 任意业务脚本/方法/音频轨道副作用需项目恢复，未承诺自动撤销。Godot 资源加载仍同步，预加载及返回/页面焦点扩展不属于 V4。

## 10. V5 实现与后续候选

### 10.1 方向与依据（2026-10-02）

用户确认优先覆盖 UnityScreenNavigator（USN）的能力，并授权实现能力清单中的 6、7、8：单次动画开关，以及统一的上下文动画选择。7、8 合并为同一能力，只保留一个同步选择包装函数 `Transitions.Select(Func<TransitionContext, IUITransition>)`。实际契约见 10.6，验收见 10.7；其他候选尚未授权实施。不新建未指定的 V5 开发文档。

覆盖指解决相同游戏 UI 需求，不要求照搬 Page/Sheet/Modal 基类、Unity 组件或全部方法重载。Node 模型、直接容器 API、内嵌 Tab、无历史兼容、原异常传播和同步/异步边界继续有效；不因参考 USN 而引入 Replace。

依据：[USN 官方 README](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md) 与 [PageContainer 源码](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/PageContainer.cs)，2026-10-02 核对 master；属于文档/源码审计，未运行 USN 项目。

### 10.2 USN 能力覆盖审计（补充核对）

此前五项是 V5 优先候选，不是全部剩余差距。不能把“存在自定义扩展接口”写成“开箱即用能力已经完整覆盖”。本次按 README 功能章节、Page/Sheet 实现与本工程实际代码核对，分为已有对应能力、缺口及实现方式差异；不宣称穷尽 USN 的每个内部 API、发行版本和 Unity 插件组合。

#### 已有主要对应能力

| USN 能力 | 本工程实际对应 | 限制 |
| --- | --- | --- |
| Page 历史、逐层返回、返回指定历史项 | Stack Push/Pop/PopTo | 使用具体实例，未复制数量/字符串 ID 全部重载 |
| 基础 Modal 嵌套和关闭 | Modal Open/Close/CloseTo，额外支持结果等待 | 已支持返回具体下层实例，实际契约见 10.4 |
| 页面生命周期及外部处理器 | V2 生命周期、AddLifecycle/RemoveLifecycle | 外部处理器执行顺序仍有差距，见 15 |
| 通用、逐页和自定义转场 | Context/Container/Node 配方继承及自定义执行对象 | 6—11 动画能力已由 V5 与本轮补齐；后端编辑器配置仍待规划 |
| 伙伴页面参与动画 | TransitionContext.Source/Target、V5 Transitions.Select | 已支持同一函数按阶段、来源/目标和业务状态选择配方 |
| 编辑器动画接入 | Godot AnimationPlayer | 属于 Godot 对应方案，未实现 Unity Timeline 适配 |
| 基础模态遮罩和外部点击 | 工厂、Shared/PerModal、DismissRequested | 不等同 USN 全部策略与时序，见 13 |
| 内嵌页面复用和互斥切换 | Tab Bind/Switch | 符合已确认默认工作流；隐藏当前项和动态资源项另列 |
| 页面数据和创建定制 | 创建参数、类型工厂、初始化生命周期 | 没有照搬 onLoad 回调或特定 DI 系统 |
| 销毁清理、容器子树释放 | Node/Context DisposeAsync | 不复制 USN 后台未等待清理；本工程必须观察失败 |

#### 剩余能力与配置差距清单

编号 1—5 延续此前候选，6—16 为本轮补充。缺失配置能力不等于完全无法自定义实现；以下明确区分。

| 编号 | 能力 | 当前差距 | 游戏使用场景 |
| --- | --- | --- | --- |
| 1 | 资源取得、实例创建分离；异步加载与释放 | 已实现 IUIResourceLoader → IUIResource → IUIElement；Godot 同步取得资源，独立归还持有 | 大型界面按需加载和关闭后归还资源 |
| 2 | 预加载与显式释放 | 已实现 Context.PreloadAsync<T> 与 UIResourceLease.Dispose | 菜单预备背包/商店资源 |
| 3 | Modal 一次返回指定下层实例 | 已实现 CloseToAsync(target)，一次提交，不逐层激活 | 商品确认后直接回商店 |
| 4 | 临时页不保留历史 | 每次 Push 都保留 | 资源准备页不出现在返回路径 |
| 5 | 容器整体操作前后回调 | 只有节点生命周期与状态通知 | 外壳统一同步标题/返回按钮 |
| 6 | 单次操作选择是否播放动画 | V5 已实现容器操作的 playAnimation 选项，生成 API 和遮罩同步透传 | 首次恢复菜单立即显示，之后操作正常动画；不临时改共享配方 |
| 7、8 | 根据转场上下文选择动画 | V5 已实现 Transitions.Select 与 TransitionContext.Phase | 阶段、来源/目标等条件由同一函数判断，覆盖推进/返回差异和不同来源的进入表现 |
| 9 | 转场绘制顺序配置 | 本轮补齐 Node.TransitionOrder；高值在转场中置前，同值保留原推进/返回默认 | 旧页覆盖新页退出，而不是始终由新页盖住旧页 |
| 10 | 动画参数配置 | 本轮补齐 Godot Simple 起止帧、延迟、Tween 缓动，预设支持同样时序配置 | 美术调整移动与淡入节奏，不为每套参数写自定义配方 |
| 11 | 转场状态、类型、进度及变化通知 | 本轮补齐 Node 状态/阶段/进度和 TransitionChanged，配方可报告进度 | 窗口动画期间协调外壳装饰/按钮状态；这是运行能力，不能自动视为诊断系统 |
| 12 | 转场输入控制策略 | 普通容器固定锁局部，模态临时锁全局；普通容器不能选择锁整个 Context | 全屏菜单切换/准备期间同时阻挡独立侧栏入口；普通局部切换仍允许其他容器交互 |
| 13 | 更多遮罩策略及时序 | Shared 会移到顶层下，PerModal 每层创建；没有始终位于首层下及动画前/后重排的全套选项 | 某项目希望嵌套弹窗不再暗化底层弹窗，或旧弹窗退出后才移动遮罩 |
| 14 | 隐藏当前 Tab 项并进入无选中状态 | 已实现 HideAsync，清空 Current/CurrentName，保留绑定页面 | 某互斥侧栏收起当前页面，但保留外壳和全部页面实例 |
| 15 | 外部生命周期处理器顺序 | 业务虚方法先执行，处理器按注册顺序；不能声明先于业务/后于业务及优先级 | 通用数据准备行为先于 View 进入，另一个行为在业务处理后执行 |
| 16 | 独立资源动态 Tab 项 | 当前 Bind 采用既有实例，没有从注册资源创建项的直接入口 | 项目扩展页面来自独立资源；内嵌 Tab 仍是默认，不替换已有工作流 |

来源：[USN 转场章节](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#screen-transition-animation) 对应 7—10；[其他功能](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#other-features) 对应 3、4、11—13；[SheetContainer 源码](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Sheet/SheetContainer.cs) 对应 6、14、16；[Page 源码](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/Page.cs) 对应 7、11、15；资源和回调依据见 10.4。这里只记录差距，不据此授权全部实现。

补充语义：14 是隐藏当前项、Current 变为无选中，区别于父容器整体隐藏。16 指加载资源并实例化后纳入 Tab，不把 USN PreloadedAssetLoader 解释成接管已挂载的场景实例。13 的 USN Only First Backdrop 与本工程 Shared 重排并不相同。12 的允许交互不意味着同一容器允许并发操作，USN 也禁止同一容器转场中再次切换。

**15 的 USN 源码补充（2026-10-02，master，仅静态核对）：** [Page.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/Page.cs) 提供 `AddLifecycleEvent(IPageLifecycleEvent lifecycleEvent, int priority = 0)` 与 RemoveLifecycleEvent，Page 自身在 AfterLoad 以 priority 0 纳入同一个集合；[PageExtensions.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/PageExtensions.cs) 还支持仅注册指定阶段的委托，不要求完整处理器实现。README 中 Modal/Sheet 也有对应接口。它同时支持继承业务回调和外部生命周期组合，AddLifecycle 概念不是本工程独有。

[CompositeLifecycleEvent.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Shared/CompositeLifecycleEvent.cs) 的 Task/IEnumerator 阶段按 priority 从小到大执行，各组完成后才执行下一组；同一 priority 组内并发启动，不保证完成顺序。因此负值在 Page 的可等待阶段之前、正值之后，不应照搬为本工程的注册顺序语义。其同步 Action 重载直接遍历 Dictionary.Values，没有显式按 priority 排序；当前源码不能据此保证 Did 等同步通知也遵循数值优先级。未运行 USN，不据此宣布所有版本有同样表现或引入本工程新 API。本工程 BeforeNode/AfterNode 仍为建议，尚未授权实施。

#### 配置便利、后端特性和有意采用不同方式的部分

| USN 功能/方式 | 本工程差异 | 处理判断 |
| --- | --- | --- |
| 每个容器覆盖资源加载器 | 目前只在 UIContext 配置 Loader | 有需要时纳入 1 的资源来源协议；不默认增加多套资源缓存 |
| 直接注册已加载 Prefab 引用 | 内嵌 Element 可绑定；资源注册仍使用字符串，Godot 未提供 PackedScene 引用注册便利入口 | 与资源预加载不同，可作为后端便利能力；不要误称已完整提供 |
| Inspector 配置动画、遮罩、加载器 | 当前以 C# 配置和 Godot 场景资源为主 | 编辑器配置是易用性差距，后续独立规划；不把 Unity Inspector 放进核心 |
| Container.Of/Find、全局 Instances | Root/Parent/GetChild、项目持有引用 | 层级定位可实现；缺少直接查祖先/深层名称的便利 API，但不因此引入全局静态注册表 |
| 按数量/ID 返回、指定字符串实例 ID | 当前主要通过具体实例定位 | 功能需求可对应，全部重载不是必须补齐的功能 |
| 同步资源加载选项 | Godot 默认 loader 同步加载；公共容器流程仍需等待生命周期/转场 | 资源是否同步与整个开窗 API 是否同步是两件事，不能为方法数量而增加同步阻塞入口 |
| Container 内容裁剪、布局填充 | Godot 资源设置 ClipContents/锚点；Demo 已使用包装层和裁剪 | 引擎适配/资源职责，不新增渲染无关核心的矩形裁剪系统 |
| Coroutine/Task/完成回调多种等待方式 | C# Task/ValueTask 和应用任务观察 | 当前足以观察结果/异常，不必复制 Unity 协程句柄 |
| Resources/Addressables、Timeline、ScriptableObject | 目前 Godot 实现；Unity 专用适配未实现 | 不能宣称支持 Unity 后端；属于后续引擎适配交付，不是核心流程缺失 |
| Pop 后实例复用/实例池 | 本工程 Stack/Modal 关闭后销毁；Panel Hide 和 Tab 保留 | USN 也不复用已 Pop 项；实例池不是 USN 缺口，不自动新增 |

所以不能概括为“仅剩五项”。后续规划同时保留完整差距清单与每版优先范围；不把所有差异都认定为必须照搬的设计。

### 10.3 当前范围与其他候选

| 编号 | 功能 | 实际游戏场景 | 建议 |
| --- | --- | --- | --- |
| 1 | 资源加载与实例创建分离；持有/释放；Godot 真正异步加载 | 较大的背包、商店按需加载，关闭后正确归还资源 | 持有/释放已实现；后台加载暂缓，见 10.4 |
| 2 | 预加载与显式释放 | 主菜单提前准备设置/背包，进入详情前准备下一页 | 推荐，依赖 1 |
| 3 | 弹窗一次返回指定下层实例 | 商店→商品详情→购买确认，完成后直接回商店，避免逐层闪现 | 推荐 |
| 4 | 临时栈页面不保留历史 | 菜单→资源准备页→目标页面，返回时跳过准备页 | 次优先，可选 |
| 5 | 容器整体操作回调 | 背包外壳统一更新详情/手记标题与返回按钮，子页面不重复通知 | 次优先，可选 |

上表保留此前 1—5 的候选用途，不代表当前执行顺序。用户已指定先实现 **6、7、8**，实际契约见 10.6；1—5 尚未授权实施。

### 10.4 实现与 API 方向

资源流程 1、2、Modal 返回 3 与 Tab 收起 14 已由用户选择并实施；其余为候选，名称、签名及细节在用户选择后定稿。

#### 1、2. 资源流程与预加载（已实现）

[USN 资源加载](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#loading-screen-resources) 支持替换加载器与资源释放；[预加载](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#preloading) 提供 Preload/ReleasePreloaded。此前加载接口直接返回实例，无法表达纯资源预加载；已替换该接口，不保留兼容入口。

流程为“取得可实例化资源 → 同步创建/挂载实例 → 节点初始化 → 容器流程”。后端实现资源句柄，核心不依赖 PackedScene/GameObject。普通 Open/Push 自动取得所需资源，使用者继续使用已有容器 API，不为普通开窗增加手动资源管理。

预加载只持有资源，不调用节点工厂、不创建显示实例、不执行生命周期。公开入口如下，普通项目只需使用第一个入口和返回的句柄：

```csharp
Task<UIResourceLease> UIContext.PreloadAsync<T>(CancellationToken cancellationToken = default)
    where T : UINode;

// 适配器协议：加载不实例化，实例化不异步。
Task<IUIResource> IUIResourceLoader.LoadAsync(string resource, CancellationToken cancellationToken);
IUIElement IUIResource.Instantiate(IUIElement? mount);
void IUIResource.Dispose();
void UIResourceLease.Dispose();

// 菜单预备资源；窗口创建成功后可以归还预加载持有。
var prepared = await ui.PreloadAsync<InventoryWindowView>();
var backpack = await windows.OpenAsync<InventoryWindowView>();
prepared.Dispose();
// backpack 仍可显示、隐藏、恢复；关闭时归还实例自己的持有。
await windows.CloseAsync(backpack);
```

句柄同步、幂等释放；只能在宿主 UI 线程操作。未注册类型、没有资源的结构性类型、已销毁 Context 均明确报错，不返回空凭据。最后一个持有释放后移除资源记录，下一次显式请求重新取得；不缓存没有持有者的已完成资源。

同一 Context 内按注册资源字符串（Ordinal 精确匹配）合并正在加载的请求和仍有持有者的资源；不同类型注册相同路径也共享。每个等待者先独立保留一次持有，成功后转为句柄/实例持有。取消等待立即结束该调用，只归还自己的保留，不取消共享加载；即使最后等待者取消，也将已经开始的加载收尾并归还结果。Context 销毁先取消节点/预加载等待，随后取消底层加载、等待收尾、释放所有剩余句柄；外部保留的句柄之后再次 Dispose 不重复释放。

加载/创建/清理失败记录并传播原始异常，不包装、不缓存失败为成功、不自动重试。取消后的无人等待加载仍可能失败：立即记录原异常，由 Context.DisposeAsync 传播无人观察的失败，不遗失后台错误。正常调用已观察到的加载失败不在 Context 销毁时重复抛出。清理失败保留首个异常并按既有 UIFailure 契约附加其他异常。资源持有在显示实例清理之后归还；显示清理失败也执行资源清理。

Godot 默认 loader 在场景线程调用 `ResourceLoader.Load<PackedScene>`，再以已完成的 Task 返回 `IUIResource`；因此 `LoadAsync` 不代表后台加载。加载无法中断，但返回后会再次检查取消令牌。`Instantiate`/`AddChild` 同样留在场景线程。Godot 4.7.2 .NET 的 `LoadThreadedRequest`/`LoadThreadedGet` 路径在完整 Demo 退出时产生 RefCounted 泄漏警告；改为同步加载后，完整自测不再出现 ObjectDB 或资源仍在使用警告。缺失资源仍抛出明确 `IOException`，已有 C# 异常原样传播。依据：[Godot ResourceLoader](https://docs.godotengine.org/en/stable/classes/class_resourceloader.html)。

Godot 资源释放清空此持有对象的 PackedScene 引用，不对引擎可能共享的托管 PackedScene 包装对象调用 Dispose，不强制 GC。这里只保证归还框架引用；实际内存/显存回收受实例、其他使用者、引擎缓存及托管 GC 影响。借用的内嵌 Tab 不另行加载或持有资源，仍由其父场景拥有。

Demo 菜单初始化时预加载背包外壳，首次开窗成功后释放预加载句柄；内部背包页/详情仍按需加载，Hide 保留实例与资源。主菜单没有缓存按钮或诊断面板，正常流程不人工延时；延时加载仅用于显式取消测试。实例池、LRU、内存预算不在本次范围。

#### 3. 弹窗一次返回指定实例（已实现）

[USN 多层返回](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#pop-multiple-screens-at-once) 同时支持 Page/Modal。本工程 Stack 使用 PopTo，Modal 使用 CloseTo；没有照搬数量/ID 等多个重载。

```csharp
Task ModalContainerNode.CloseToAsync(UINode target, bool playAnimation = true,
    CancellationToken cancellationToken = default);

await modals.CloseToAsync(shop); // 保留这个商店实例，移除其上的弹窗。
```

目标必须是同一容器中仍存活的具体实例；当前顶层正常完成。null、其他容器的节点、已移除/销毁节点仍直接报错。不按类型猜测，不使用全局 ID。CloseAsync 仍仅关闭当前项，和 CloseTo 共用关闭范围的实现，不通过循环调用 Close 模拟批量返回。

只有当前顶层执行 Exiting/退出动画/Exited；LifecycleReason.Return、Source=原顶层、Target=保留实例，动画 Phase.Exit、Direction.None。中间项暂时隐藏，逐层遮罩同步隐藏，避免顶层淡出时露出被跳过页面；它们不重新激活、不执行 Entering/Entered 或 Exiting/Exited，只在提交后的可见性变化和销毁流程中通知/清理。保留项仅恢复有效激活状态，不重播进入生命周期和动画。

一次提交最终模态列表和输入所有者；焦点采用紧邻目标的被移除项入场前捕获值，直接恢复到目标原来的焦点。准备期取消/失败恢复全部原弹窗可见性、遮罩和原输入所有者，不修改列表。提交后不回滚，结束通知或清理失败仍清理所有移除项，按首个原异常及 UIFailure 附加异常契约传播。提交后的必要清理不会被调用者取消而跳过。

Shared 遮罩保留并移到目标下；PerModal 仅最顶层遮罩播放隐藏动画，被移除的中间遮罩直接隐藏并释放，目标自己的遮罩保留。普通关闭最后项仍隐藏并释放其遮罩。显示和资源持有按节点销毁契约归还。

结果等待契约：未提交结果的 Dialog 被单步/批量关闭或销毁时，ShowDialogAsync 以取消结束，不伪造 false/default；提交过的结果在关闭与全部相关清理完成后返回。明确的调用者取消优先于结果；否则已经提交的结果优先于销毁带来的生命周期取消。ShowDialogAsync 共享正在进行的关闭结果，不重复发起 Close；外部关闭失败传播原异常，等待取消时的清理失败附加到原取消异常。未回答的 Dialog 关闭失败且已恢复时，仍可继续回答并正常关闭；已回答 Dialog 的失败不通过自动重试隐藏。直接销毁同样等待销毁完成。

Demo 使用商店 → 商品详情 → 确认订单 → 原商店的纯 UI 流程，无 Gameplay。确认页使用普通业务 Node，显式调用 CloseTo 后更新保留商店的订单展示，不强制所有确认 UI 都是结果弹窗。结果等待语义由核心及真实引擎自测另外覆盖。

#### 4. 临时页不保留历史

[USN 不保留历史](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#dont-stack-pages-in-history) 用于加载页：显示时仍为当前页，推进下一页后才移除，返回时跳过。

候选 `stack.PushAsync<LoadingView>(retainInHistory: false, cancellationToken: token)`，生成器同步支持选项。A→临时 B→C 后历史为 A/C；进入 C 失败时 B 仍是当前页，不能提前销毁。属于 Push 的历史选项，不增加 Replace，不立即替换 A。

尚未推进下一页时仍能正常返回 A；连续临时页、初始临时页、取消和提交后清理失败须明确。若选中，Demo 以实际资源准备页衔接菜单/背包，不制造假网络或 Gameplay 场景。

#### 5. 容器整体回调

USN README 的生命周期章节为 Page/Modal/Sheet 提供容器 Before/After 回调，区别于外部节点生命周期。候选同步注册观察器，回调携带操作种类、Source/Target 与移除项；外壳在提交后根据当前页更新标题与返回按钮。

等待页面准备继续使用已有异步生命周期，不另建异步消息总线、否决守卫、操作队列或诊断系统。开始与成功提交通知点明确，不把失败报告为成功。回调异常记录并原样传播；提交后回调失败不回滚已提交页面，调用方必须能判断状态。订阅可明确移除并随宿主销毁解绑。

### 10.5 边界与交付

焦点记忆、返回输入路由、窗口置前、多个模态容器仲裁、编辑器与绑定生成不作为此次主范围。动态资源 Tab、12/13 的输入和遮罩策略仍保留为后续差距；6—8 已由 V5 实现，9—11 的后续补齐见第 11 节。内嵌 Tab 不因 USN 的动态 Sheet 工作流而被替代。Analyzer、诊断分析和 Replace 仍不引入。

用户选择后：定稿选中契约 → 重构核心与 Godot → 迁移受影响的生成器入口 → 更新游戏 UI Demo/操作说明 → 核心行为与故障测试、引擎验证 → 更新本文并自动本地提交。不保留旧加载接口转发兼容层。

本轮已同步修改核心、生成器、Godot 适配器与 Demo。V4 的 86 项测试与 12 场景保留为历史记录，V5 验收独立记录于 10.7。

### 10.6 实际契约：6、7、8 转场 API

本轮实现两项：6 的单次动画开关，以及 7、8 合并后的上下文动画选择。只保留一个同步选择包装函数；不引入阶段配置集合或规则 DSL。调用直接位于对应容器，不增加 Navigation/Selection 对象。未授权的资源重构、绘制顺序配置、缓动/进度、输入策略等不混入本次。

#### 6. 单次操作动画开关

所有实际执行进入/退出的容器操作增加 `bool playAnimation = true`，放在业务参数之后、CancellationToken 之前。平常调用不增加编码；需要瞬间显示时使用命名参数。

```csharp
await pages.PushAsync<ItemDetailsView>(args, playAnimation: false);
await pages.PopAsync(playAnimation: false);
await pages.PopToAsync(itemList, playAnimation: false);
await tabs.SwitchAsync("Graphics", playAnimation: false);
await windows.OpenAsync<InventoryWindow>(playAnimation: false);
await windows.HideAsync(inventory, playAnimation: false);
await modals.OpenAsync<SettingsView>(playAnimation: false);
await modals.CloseAsync(settings, playAnimation: false);
```

影响 Panel Open/Show/Hide/Close、Stack Push/Pop/PopTo、Tab Switch（名称和类型入口）、Modal Open/Close/ShowDialog。构造/注册/Attach/Bind/Dispose 不新增动画开关；显式销毁保持清理语义，不变成自动播放退出动画。未来新增操作若包含转场再遵循相同约定。

实际主要签名：

```csharp
// Panel 与 Modal 的 Open 统一形状，各自在自己的容器类型上声明。
Task<T> OpenAsync<T>(object? args = null, bool playAnimation = true,
    CancellationToken cancellationToken = default) where T : UINode;
Task<T> PushAsync<T>(object? args = null, bool playAnimation = true,
    CancellationToken cancellationToken = default) where T : UINode;
Task PopAsync(bool playAnimation = true, CancellationToken cancellationToken = default);
Task PopToAsync(UINode target, bool playAnimation = true,
    CancellationToken cancellationToken = default);
Task SwitchAsync(string name, bool playAnimation = true,
    CancellationToken cancellationToken = default);
Task SwitchAsync<T>(bool playAnimation = true,
    CancellationToken cancellationToken = default) where T : UINode;
// Show/Hide/Close 同样采用 (node, playAnimation = true, cancellationToken = default)。
```

Panel Open 统一为一个核心签名，省略 args 或显式传 null 都表示没有创建参数；已有参数化 Single 可因此重开，新建参数化节点仍校验参数类型。非 null 参数仍只能用于新建。生成器保留具体业务参数的类型化创建入口及无需业务参数的重开入口，都调用同一核心方法。CancellationToken 使用命名参数，避免被 object args 误解释；动画开关也建议使用命名参数。旧版本文档中的无参核心重载说明由此契约取代。

`false` 仅取消该次操作的框架转场表现：不调用动画选择函数、不创建/播放节点动画执行对象、不进行只为动画服务的临时排序。模态遮罩同样即时达到所需显隐状态；必要创建/重排/释放仍执行。IUIBackdrop 的 ShowAsync/HideAsync 也接收相同开关，由后端显式即时设置最终表现，不利用 Restore 冒充正常进入/退出，Restore 继续表达失败恢复。

资源等待、初始化、Entering/Exiting、提交、Entered/Exited、输入/焦点交接及清理完整执行，失败继续传播。调用仍可能需要等待，保留 Async；不能提供阻塞式同步 Open。业务自己在生命周期内执行的动画不受此开关自动控制，也不尝试暂停任意引擎动画。

`true` 采用已配置的配方，其中明确配置的 InstantTransition 仍是即时表现；该参数不是强制所有项必有动画。Panel 已显示 Single 的重复 Open 仍返回现有实例，不因 true 重播。ShowDialog 的该参数覆盖它所管理的开场和最终关闭；Complete(result) 保持同步结果提交，不新增动画控制。等待取消后的必要关闭也沿用这次调用的开关并继续观察异常。

Source Generator 类型化入口同步增加并传递 `playAnimation`，包括自动对话框调用；业务参数仍保持生成的具体类型。旧签名调用点、Demo 和测试同步迁移，不留兼容转发重载。此次只有一个操作选项，不预建装满未来字段的 Options 类型，也不增加逐次传入源/目标配方的第二套配置。

#### 7、8. 统一的上下文动画选择

阶段、来源/目标和业务状态都是选择动画的输入，统一由普通 C# 函数表达。新增且只新增一个选择包装入口：

```csharp
public static class Transitions
{
    public static IUITransition Select(
        Func<TransitionContext, IUITransition> select);
}
```

Transitions 位于纯核心，不依赖 Godot/Unity；包装对象是内部实现，用户只接触现有 IUITransition。Select 同步创建包装配方，不执行选择函数、不创建动画运行对象，也不需要 await。固定动画直接赋值；需要动态选择时才包装：

```csharp
var fade = GodotTransitions.Fade();
var slide = GodotTransitions.SlideHorizontal();
var scale = GodotTransitions.ScaleFade();

ui.DefaultTransition = fade;

pages.ChildTransition = Transitions.Select(context => context.Phase switch
{
    TransitionPhase.PushEnter => slide,
    TransitionPhase.PushExit => fade,
    TransitionPhase.PopEnter => fade,
    TransitionPhase.PopExit => slide,
    _ => fade
});
```

同一函数可以同时处理阶段、来源、目标和业务状态，不需要区分“阶段配置”与“条件配置”：

```csharp
// 放在详情页构造或 OnInitializeAsync 中，首次进入即生效。
Transition = Transitions.Select(context => context.Phase switch
{
    TransitionPhase.PushEnter when context.Source is ItemListView => slide,
    TransitionPhase.PushEnter when context.Source is CodexView => scale,
    TransitionPhase.PopExit => fade,
    _ => fade
});

// 基础 Node 按名称选择，不要求具体 View 类型。
tabs.ChildTransition = Transitions.Select(context =>
    context.Phase == TransitionPhase.Enter &&
    context.Source?.Name == "General" && context.Target?.Name == "Graphics"
        ? scale
        : fade);
```

ItemListView/CodexView 是示例业务类型，不是内置节点。复杂分支可以写 if/switch、模式匹配和项目纯查询，也可直接传方法 `Transitions.Select(ChooseTransition)`，方法签名 `IUITransition ChooseTransition(TransitionContext context)`。预设在配置时创建并复用，选择函数只返回配方；真正自定义动画表现仍实现已有 IUITransition/IUITransitionRun。

固定动画使用 `node.Transition = fade`。无需 TransitionSet、With、When、阶段槽、规则优先级、默认配方参数或额外的公开选择器接口。之前阶段专用配置和条件选择的两套 API 已由本节完全取代，不保留旧候选设计并行路径。

#### 转场上下文与阶段

TransitionContext 增加只读 `Phase`，继续提供 Node、Element、Container、Source、Target、Direction、Reason 和 Lifecycle。公开枚举仅描述实际执行的阶段，不再表达可继承的配置槽：

```csharp
public enum TransitionPhase
{
    Enter, Exit,
    PushEnter, PushExit,
    PopEnter, PopExit
}

// TransitionContext 中新增：
public TransitionPhase Phase { get; }
```

| 操作 | 退出项阶段 | 进入项阶段 |
| --- | --- | --- |
| Stack Push | 原当前页 PushExit | 新页 PushEnter |
| Stack Pop/PopTo | 原当前页 PopExit | 恢复页 PopEnter |
| Tab Switch | 旧项 Exit | 新项 Enter |
| Panel Open/Show | 无 | 目标 Enter |
| Panel Hide/Close | 目标 Exit | 无 |
| Modal Open | 无（底层只失活） | 新弹窗 Enter |
| Modal Close | 顶层 Exit | 无（下层恢复输入，不重播入场） |

阶段由容器提供的方向和进入/退出信息计算，不根据名称猜测。首次 Push 只有 PushEnter，最后 Pop 只有 PopExit，PopTo 中间项仅清理。PushEnter/PopEnter 不自动改成 Enter，项目需要通用进入判断可使用 context.Entering；同理退出。返回恢复与新建进入不因此新增生命周期回调。

Source 是该操作切换的起点，Target 是将显示/恢复的项；两侧动画取得相同的 Source/Target，Node 表示当前动画所属项。无源/目标时保持 null。Modal Close 的 Target 可以是下层弹窗，不意味着为它重播进入动画。

#### 配置继承、解析和异常

配置入口保持 `ui.DefaultTransition`、`container.ChildTransition`、`node.Transition`，值仍为 IUITransition。选择所用整套配方依次为：Node 非 null → 直接管理 Container.ChildTransition 非 null → Context.DefaultTransition。Node/Container 的 null 明确表达继承，未配置默认仍采用 Context 的既有 InstantTransition；与函数返回 null 完全不同。

选中普通配方就直接执行；选中 Transitions.Select 包装配方，则用本动画项的上下文同步求值一次，冻结返回的具体配方。没有函数规则列表、阶段槽查找或跨层合并。项目在函数里明确写默认分支，如 `_ => fade`；包装函数没有自动默认配方参数。无动画场景返回明确的 InstantTransition。

Select 参数为 null 是配置错误；选择函数抛出异常或返回 null 是操作错误，记录并传播，不尝试容器/Context 配方、不自动改为无动画。真实配方的 Create 返回 null、播放/恢复失败继续按现有异常协议处理。只有调用方显式 playAnimation:false 才正常略过选择与动画执行。

选择函数应同步、只读，不负责资源加载、等待或播放；异步准备保留在生命周期。禁止在解析时改变框架配置、操作容器或发起嵌套变更，现有修改守卫需覆盖该阶段。包装配置可复用，但闭包中的业务状态可能变化，框架不复制闭包；每次操作解析后不再因为业务状态变化重新选择。

如果返回值是另一个 Select 包装配方，使用同一上下文继续解析，各个包装函数每项调用一次；重复遇到同一包装引用直接报循环配置错误，不无限递归。没有为组合增加第二种公开 API，也不接受 null 表示继续搜索。

#### 流程、验收和 Demo

playAnimation 与方向/阶段在操作开始时固定；新实例初始化后、Entering/Exiting 准备前，捕获源/目标所用配置并求出选择结果。准备之后才调用具体配方 Create，捕获动画表现基线。预设、自定义配方和选择包装最终进入同一个执行流程；配置变更仅影响后续操作。

保持 V4 源/目标并行动画、一次提交、失败取消伙伴并观察全部原异常、成功/失败恢复表现的契约。false 仍等待资源、生命周期和清理；模态遮罩即时显隐。生成器只需透传动画开关，动画选择由项目配置，不为函数生成独立注册系统。

验收至少包括：默认/false 不污染其他调用；false 同时即时处理遮罩且生命周期仍等待/报错；实际 Phase 和四种 Stack 阶段及首次/末次/PopTo；Panel/Tab/Modal 阶段；固定配方与 Select 同一流程；Source/Target 的类型/名称/业务状态分支；函数每项仅求值一次且发生于准备前；异常/null 不改用其他配方；三层整套继承、共享配置和选择结果冻结；嵌套包装及循环错误；取消/动画失败恢复；生成器完整透传。

Demo 使用已有设置内嵌页面验证名称选择与单次即时切换，背包列表/详情/手记验证推进/返回阶段和来源选择；普通打开保持自然动画。具体业务路径与资源布局实施时核对，不为选动画创建不存在的图鉴页面或技术展台，不用历史 V4 验收宣称新能力已验证。

### 10.7 V5 验收记录（2026-10-02）

- 解决方案构建 0 警告、0 错误；核心与生成器共 108 项测试通过。覆盖六种 Phase、来源/目标、三层继承、准备前冻结、包装组合及循环、原始异常/null、只读变更守卫、逐次开关、资源/生命周期等待与失败、结果弹窗取消清理和遮罩开关。生成器测试编译并执行生成 API，验证业务参数、动画开关、CancellationToken 和结果透传。
- Godot 4.7.2 .NET 的 headless 与原生 OpenGL 自测均通过 15 场景。新增偏好名称选择、背包阶段/类型选择与即时返回清理、两种即时遮罩及生成对话框流程；既有 12 场景继续通过。
- 18 个独立故障夹具均记录异常并退出 1；新增 selector、selector-null、selector-cycle。异常路径不回退到其他配方或跳过失败。
- Demo 的内页初始化使用 playAnimation:false；偏好 Audio→Graphics 的进入使用缩放淡入，其他切换淡入淡出；背包列表/详情按推进和返回阶段组合滑动/淡入淡出，涉及手记时淡入淡出。自测验证即时切换，日常交互保留动画，不添加技术按钮。
- 偏好与手记原生截图已目视检查，无新增素材；复用既有已许可资源。日志与截图保存在被忽略的 artifacts，工具和构建产物不入库。
- 未验证全部分辨率、实体手柄、导出平台与其他引擎。Godot 资源加载仍同步；预加载、动态 Tab、其他 USN 差距继续待定。本次只读守卫约束框架变更，不能阻止闭包直接修改项目自己的状态，选择函数应保持纯查询。

## 11. 对比 USN 补齐动画能力（2026-10-02）

用户授权先对比 USN 补全动画能力。本轮已实现 9、10、11，继续沿用已实现的 6、7、8 和自定义配方、Godot AnimationPlayer 接入。输入策略（12）、更多遮罩策略（13）、生命周期处理器顺序（15）及资源流程等继续待定，不因本次授权混入。实际验收见 11.5，不新建未指定的版本文档。

### 11.1 调研与取舍

核对 USN master 的 [README](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md)、[Page.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/Page.cs)、[SimpleTransitionAnimationObject.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Shared/SimpleTransitionAnimationObject.cs)、[SimpleTransitionAnimationBehaviour.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Shared/SimpleTransitionAnimationBehaviour.cs) 和 [SheetAlignment.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Shared/SheetAlignment.cs)。只作官方文档/源码审计，未运行 USN 项目。

| USN 动画能力 | 本工程对应 |
| --- | --- |
| 默认、逐页、阶段和伙伴页面选择 | 既有 Context/Container/Node 配方继承与唯一 Transitions.Select；不复制配置槽或正则规则表 |
| playAnimation 单次开关 | 既有容器及生成 API，false 不选择/创建/播放配方 |
| RenderingOrder | 新增 Node.TransitionOrder，转场期间比较源/目标优先级并在结束恢复原显示顺序 |
| 简单动画的 Delay/Duration/Ease、Before/After 位置/缩放/透明度 | GodotTransitionFrame + GodotTransitions.Simple；预设同样接受 delay/transition/ease |
| Left/Top/Right/Bottom/Center 对齐 | RelativeOffset 使用目标原始 Size 的倍数，任意二维位置即可覆盖五种方向，无需再建立枚举；Godot 向下为正 Y |
| IsTransitioning、动画类型、进度与通知 | Node 的 IsTransitioning、CurrentTransitionPhase、TransitionProgress、TransitionState 和 TransitionChanged |
| 自定义伙伴动画与 Timeline | IUITransition/IUITransitionRun + Source/Target，自定义进度通过 context.ReportProgress；Godot AnimationPlayer 是当前后端的编辑器动画对应能力 |

SimpleTransitionAnimationObject 当前源码 Duration 返回 _duration，而 Behaviour 返回 _delay + _duration，二者不一致；不照搬这个细节。本工程等待总时长严格为 delay + duration。Unity Inspector/Timeline 组件与完整 Unity 适配不在交付范围，不能宣称所有 USN 编辑器功能已覆盖。Godot 使用 [Tween.InterpolateValue](https://docs.godotengine.org/en/stable/classes/class_tween.html#class-tween-method-interpolate-value) 计算缓动，不为同一 Control 属性并行创建多个引擎 Tween。

### 11.2 绘制顺序

`UINode.TransitionOrder` 为同步可配置 int，默认 0；源/目标值不同，高值临时置前，相同时保持 Stack Push 目标置前、Pop/PopTo 源置前、Tab 目标置前的现有行为。值与配方一样在进入/退出准备前冻结。单侧转场没有另一侧可比较，Modal 的稳定叠层与遮罩顺序不因此改变。它只解决转场重叠表现，不建立全局 UI 层级、项目固定枚举或永久绘制优先级。playAnimation:false 不捕获/修改/恢复仅用于动画的临时顺序。

例如详情页配置 TransitionOrder=10，手记页默认 0，详情推进手记时详情保持在上方淡出，露出已经准备的手记；动画结束恢复原 sibling 顺序。成功、取消、选择/Create/播放/恢复失败继续沿用原异常及清理协议，不隐藏排序失败。

### 11.3 Godot 简单动画

`GodotTransitionFrame` 为不可变记录，包含 Offset（像素偏移）、RelativeOffset（目标当前 Size 倍数）、Scale（Vector2，默认 One）、Alpha（默认 1）。位移对捕获的原四边锚点偏移量加同一向量，并恢复原偏移量，不保存/恢复可能仍受布局调整的绝对 Position；RelativeOffset 和中心 pivot 使用当前 Size，跟随布局/窗口尺寸变化，避免动态 VBoxContainer 在首帧布局未稳定时漂移或扩张。缩放/透明度乘以原 Scale/Modulate.A，保留美术资源原值；Scale 改变时临时采用中心 pivot，结束恢复原 pivot。Offset 与 RelativeOffset 可以叠加；没有改变的属性轨道不写入，避免影响布局或业务 tint。

```csharp
var neutral = new GodotTransitionFrame();
var enter = GodotTransitions.Simple(
    neutral with { Offset = new Vector2(0, 16), Scale = Vector2.One * .96f, Alpha = 0 },
    neutral, duration: .20, delay: .03, targetPath: "Center",
    transition: Tween.TransitionType.Quart, ease: Tween.EaseType.Out);
var exit = GodotTransitions.Simple(neutral, neutral with { Alpha = 0 }, duration: .12, targetPath: "Center");
node.Transition = Transitions.Select(c => c.Entering ? enter : exit);
```

Simple 的 from/to 明确表达时间起止，不自动根据 Entering 反转；阶段分支仍由唯一 Select 表达。Fade/SlideHorizontal/ScaleFade 保留便捷调用，追加 delay、transition（默认 Cubic）、ease（默认 InOut）；SlideHorizontal 继续要求 Stack 方向，Simple 则可用于任意容器的上下/左右移动。选择所需 Tween 枚举属于后端参数，不进入渲染无关核心。

时长/延迟必须有限且非负，总和有限；零时长也等待明确的延迟，然后到达终点。帧参数必须有限，Alpha 在 0..1，Scale 允许零或负值表达折叠和镜像；路径/缓动枚举无效直接报错。缓动可能合法越过端点（如 Back/Elastic），动画进度仍按时间单调报告；不以缓动值作为进度。移动/缩放目标不能直接受 Godot Container 布局控制，仍需包装层。自定义终点是临时动画表现，框架完成和失败时都恢复捕获基线，不用它永久修改资源样式。

AnimationPlayer 继续要求非循环、未被其他播放占用的动画，新增正且有限的 SpeedScale 校验、进度报告和 AnimationFinished 完成确认。播放中被停止、换片段或速度非法直接失败，不把提前停止当成功。副作用仍由项目 restore 回调处理，不承诺自动撤销方法/音频轨道。

### 11.4 运行状态与观察

Node 提供 `bool IsTransitioning`、`TransitionPhase? CurrentTransitionPhase`、`float TransitionProgress`、`TransitionState? TransitionState` 和同步 `event Action<TransitionState> TransitionChanged`。快照包含 Context、Stage、Progress、IsRunning。Stage 为 Preparing、Playing、Completed、Canceled、Failed；阶段仍使用已有六种 TransitionPhase。没有进度条服务或诊断分析系统。

进入/退出准备前发布 Preparing(0)，选择失败也结束为 Failed；动画执行前发布 Playing(0)，后端按时间报告中间进度。配方和表现恢复成功后发布 Completed(1)；准备/播放/恢复/观察失败和取消保持最后进度，并明确发布 Failed/Canceled，不假装完成。状态在动画流程结束时结束，不包含资源加载、提交后的业务通知或销毁等待，也不表示整个容器操作已经成功；最终结果仍以 await 容器 API 为准。新资源初始化时尚未进入转场，HasEntered 与转场信息分别承担生命周期与动画职责。

playAnimation:false 仍有 Preparing→Completed，以便外部观察切换准备；没有 Playing 或中间动画进度，必要生命周期仍可等待。没有显示变化的已显示 Single 重开不发布新转场。终态保留最后快照/进度供查询，IsTransitioning=false、CurrentTransitionPhase=null；新一轮重新从 0 开始。

自定义 IUITransitionRun 使用 `context.ReportProgress(float)` 报告 0..1 的有限单调时间值，不得在选择/准备/终态后报告。未报告中间值的自定义配方只提供开始和最终完成，不伪造平滑进度；框架管理的上下文在结束后拒绝过期报告。手动构造 Context 用于独立执行配方时，没有框架状态观察者，但参数与宿主访问仍校验。

观察回调同步且只读框架状态，可以更新项目自己的渲染装饰；不能从回调改框架配置或嵌套容器操作。回调异常记录并传播，使尚未提交的操作失败并恢复；结束回调失败不能掩盖先前播放失败，附加原异常通过 UIFailure 查询且记录。各动画项分别提供状态，不把两个并行动画或资源等待加权成虚构的整体百分比。

### 11.5 Demo 与验收

Demo 使用真实设置、偏好、背包与手记流程：设置采用上移/缩放/淡入组合与短延迟；Audio→Graphics 使用 Back Out 缓动；详情保持在手记上方淡出，手记轻微上移淡入；背包外壳 Hint 随详情动画进度淡化，终态恢复。无新技术按钮、诊断面板或 Gameplay，复用既有已许可素材。

- 解决方案构建 0 警告、0 错误；核心与生成器 **127 项测试通过**（原 108 项 + 19 项运行状态、进度、观察失败和绘制顺序用例）。覆盖状态重置、取消/准备/播放/恢复失败、原始异常及附加清理失败、只读观察守卫、非法/倒退/过期进度、源/目标排序与冻结、false 不操作临时顺序。
- Godot 4.7.2 .NET 的 headless 与原生 OpenGL 自测均通过 **19 场景**（原 15 + 4 动画流程）。实际断言像素/相对偏移、起止缩放/镜像与 alpha、零时长加延迟、线性/Quart Out 采样、中间状态、AnimationPlayer 进度、延迟取消恢复、兄弟顺序和外壳装饰恢复；无效时长/延迟/枚举/帧参数直接记录并抛出。
- **22 个独立故障夹具**均记录预期异常并退出 1；原 18 项加 animation-parameters、animation-progress、animation-observer、animation-interrupted。分别覆盖无效配置、非法报告、观察回调原始 IOException 和编辑器动画提前停止；没有默认配方回退、静默成功或无观察的异步失败。
- 原生截图检查发现动态 VBoxContainer 的位移动画曾因保存绝对 Position 破坏锚点布局，已按 11.3 重构为锚点偏移播放/恢复，并加入手记位置归零、大小与挂载匹配的实际引擎回归断言。设置、手记、偏好最终原生截图已目视检查；截图、日志、构建产物和工具不入库。
- 仅核对 USN 官方文档/源码，未运行其 Unity 项目；没有交付 Unity Timeline/Inspector 适配。未验证所有分辨率、实体手柄、导出平台或其他引擎；不将 Godot 验收当作所有后端能力已完成。资源加载仍同步。

### 11.6 USN Partner Screen 原理与场景调研（2026-10-02）

Partner Screen 表示本次转场中当前动画节点的另一侧页面，由容器流程传递，不是永久绑定的父节点或另一个独立屏幕类型。Page A 推进 B 时，B 入场的伙伴是 A，A 退场的伙伴是 B；返回时交换进入/退出角色。无对应页面时伙伴为 null。Modal 也传递伙伴，但不能据此推断双方都播放动画：Modal.cs 中 Push 仅新弹窗播放入场，Pop 仅关闭弹窗播放退场，底层弹窗仍可提供几何参照。

能力分为两层：

- 条件选择：使用伙伴 Identifier 选择本节点动画，默认标识来自 prefab 名称，也可显式设置；USN 配置支持伙伴标识正则，多个条目自上而下匹配。同一详情页从列表进入或从其他页面进入，可选择不同表现。
- 跨页面动画：播放前调用 SetPartner，再 Setup 本节点；自定义 TransitionAnimationObject/TransitionAnimationBehaviour 通过 PartnerRectTransform 访问对方根 RectTransform，自行寻找需要的子元素并计算动画。官方 README 演示前一弹窗的角色图片放大并连续过渡到下一弹窗。USN 不会仅凭伙伴引用自动匹配图片、转换子元素坐标或完成共享元素动画；这些表现由项目自定义实现。

游戏场景包括角色列表头像展开到角色详情、背包物品图标展开到物品详情，以及详情关闭时缩回来源卡片。前两者是对官方图片连续过渡机制的项目应用建议，不是新增通用业务节点或本次实现范围。实际实现需要在统一坐标系采样两侧元素位置/大小，处理重复图片显隐，并在结束、取消、失败时恢复自定义动画改动。

本工程已有 TransitionContext.Source/Target/Node/Element，可取得另一侧 `ReferenceEquals(context.Node, context.Source) ? context.Target : context.Source`；Transitions.Select 承担条件选择，自定义 IUITransition/IUITransitionRun 承担跨节点表现。当前未内置自动共享元素匹配/插值系统，也不因本次问答新增 PartnerScreen 类型或第二套选择 API。

来源：[USN 条件选择说明](https://github.com/Haruma-K/UnityScreenNavigator#change-transition-animation-according-to-partner-screen)、[伙伴交互动画说明](https://github.com/Haruma-K/UnityScreenNavigator#implement-interactive-animation-with-partner-screen)、[Page.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/Page.cs)、[Modal.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Modal/Modal.cs)、[TransitionAnimationObject.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Shared/TransitionAnimationObject.cs)。本次为官方文档与源码静态核对，未运行 USN；README 所链 CharacterImageModalTransitionAnimation 示例源码当前返回 404，未将其作为已审计实现引用。

### 11.7 独立分支中的 Partner Screen Demo（2026-10-02）

用户要求创建独立分支并在 Demo 复刻伙伴图片连续转场，已在 `demo/partner-screen-transition` 实现。具体游戏路径是 Backpack 物品格 → 物品详情 → 返回原格；Equipment/Materials 每项使用对应图标，Inspect 按钮从自身图标展开，保留收藏过滤、Tab、手记与单实例窗口行为。本次是基于现有契约的 Demo 实现，不新增 PartnerScreen 类型或共享元素注册/匹配 API，不改核心或 Godot 适配器。

ItemPartnerTransition 从 Source/Target 获取另一侧 InventoryView，详情在 Entering 准备名称、描述与同一图像纹理。动画用临时 TextureRect 在共同 BodyMount 中插值来源 56px 与详情 176px 的位置/大小，每帧读取两侧实际矩形；原控件不克隆、不重挂载，SelfModulate alpha 隐藏原图但保留布局。临时图像忽略输入，置于内容上方，取消/失败/成功均恢复原表现并立即从树中移除。等待新页面延迟布局期间仍显示来源图像的临时实例，避免空白闪帧；列表与详情文字分阶段淡化。项目示例使用既定无旋转场景；类型、纹理和布局由资源、业务准备与实际引擎测试保证，不在每帧重复诊断，不降级为普通淡入。

验证：解决方案构建 0 警告、0 错误；核心/生成器既有 127 项测试通过；Godot 4.7.2 .NET headless 与原生 OpenGL 均通过 21 场景（既有 19 + Partner 展开/返回、恢复两类）。新增实际矩形插值、不同物品纹理、无重复图像/输入截获、进入与返回取消、原始 IOException 传播、失败后的历史恢复与即时操作断言。四张原生流程截图已目视检查；发现的 VBoxContainer 最小高度溢出通过列表精简与详情图文并排解决。

新增 12 张 game-icons.net SVG（Lorc/Delapouite，CC BY 3.0），透明化背景并统一颜色，来源、作者、原许可说明与修改记录在 Demo Assets/SOURCES.md，菜单提供可见署名。只提交本地素材、Godot 导入描述与脚本 UID，不提交导入缓存、截图、日志、构建产物或工具。未运行 USN、未验证所有分辨率/旋转布局/实体输入设备/导出平台，未合并回主分支或推送远程。

### 11.8 Demo 简化与持续代码规范（2026-10-02）

按用户要求清除混入业务的诊断逻辑。DemoApp 从 941 行缩减至 144 行，负责初始化、菜单业务、任务观察与退出资源释放；实际引擎自测移至 Demo/Godot/Tests/DemoApp.Tests.cs，显式截图工具移至 Tools/DemoApp.Development.cs。移除 Demo 的 22 种故障注入启动分支、FailureView 及 Run-Demo 的 -Failure 参数，不保留旧入口转发；此前故障夹具验收仅为历史记录。保留核心错误契约测试与引擎自测的预期取消/异常断言。

业务 View 移除 EnterCount/ActivationCount/DeactivationCount/InitializeCount、生命周期调试日志与仅用于打印/计数的 PageActivity，基础 Tab 直接绑定 Node。测试检查实际实例、转场快照、激活状态、准备后的业务文本与控件值，而不是为测试给业务类加计数器。物品图标初始化时按 Tab/物品名建立引用，点击直接使用对应元素，不重复扫描并添加可见性保底。

伙伴动画直接使用既定业务类型和场景引用，去掉类型、同纹理、有限值、正尺寸和旋转的重复检查。临时图像在创建运行实例时就已创建并归属挂载，播放前隐藏；Restore 直接移除、释放并恢复原表现，无空对象跳过、手工异常列表或自定义日志兜底。正常 Demo 同步事件直接调用业务，已完成的异步任务在帧入口直接读取结果；不 catch 后打印、包装或强制退出。取消检测与场景切换并发约束属于真实操作逻辑，继续保留。

验证：构建 0 警告、0 错误，核心/生成器 127 项测试通过；Godot headless 与原生 OpenGL 均通过 22 场景（原 21 + 异步任务原始异常直接从帧入口传播）。正常脚本目录无 catch、诊断断言与生命周期计数；Partner 原生截图已检查，效果与交互保持。未验证全部分辨率、实体输入设备或导出平台；未修改框架核心异常清理契约。

## 12. 下一版候选与当前 USN 覆盖结论（2026-10-02）

用户先选择第 1 项，再确认第 2、3 项；资源流程、Modal 返回和 Tab 收起已实施，其他差距继续待定，不新建版本文档。基于当前 demo/partner-screen-transition 分支代码、USN master README、PageContainer.cs 与 ScreenContainerTransitionHandler.cs 静态核对；未运行 USN。以最小场景/API 和代码简洁规范执行，不为填平功能表自动加入全部候选。

结论：常用页面历史、内嵌切换、嵌套弹窗、生命周期和动画主干已有对应能力；尚不能宣称完整覆盖 USN，资源主流程已补齐，容器边界操作及后端便利能力仍有差距。Partner Demo 验证的是现有上下文支持项目自定义伙伴动画，不代表新增自动共享元素系统。Unity Inspector、Timeline、Addressables 没有交付对应 Unity 适配，Godot AnimationPlayer 是不同后端的对应能力；不按方法数量计算覆盖百分比。

当前未完成项继续沿用 10.2 编号：4 不保留历史的临时页；5 容器整体操作通知；12 输入策略；13 更多遮罩策略/时序；15 生命周期处理器顺序；16 独立资源动态 Tab。除此之外还有后端编辑器配置、已加载 PackedScene 引用便利注册等接入差异。1/2/3、6—11、14 已实现，不再重复列为缺失。

上一轮评估的三项范围及当前状态：

1. **已实施：资源加载与预加载作为同一完整能力交付。** 场景是主菜单提前取得背包/商店资源，玩家打开时再创建页面；加载期间逐帧等待，实例化/初始化仍可能消耗主线程时间，不宣称全部开窗步骤无成本。实际接口、共享/取消/释放及 Godot 限制见 10.4，不新增实例池、LRU、缓存预算或多套策略。
2. **已实施：Modal 返回指定下层实例。** 商店弹窗→商品详情→购买确认，完成后直接回原商店；CloseToAsync 一次提交并清理跳过项，实际契约见 10.4。
3. **已实施：Tab 隐藏当前项。** 任务/地图互斥页面收起内容，保留外壳与内嵌页面；HideAsync 清空 Current/CurrentName，没有当前项正常完成，实际契约见 12.2／27。

以上三项分两轮实施。临时历史页、容器通知、输入/遮罩策略、处理器顺序、动态 Tab 与编辑器配置继续待定；不将伙伴动画通用化或 Analyzer/诊断系统作为默认范围。

调研来源：[资源加载](https://github.com/Haruma-K/UnityScreenNavigator#loading-screen-resources)、[预加载](https://github.com/Haruma-K/UnityScreenNavigator#preloading)、[多层返回](https://github.com/Haruma-K/UnityScreenNavigator#pop-multiple-screens-at-once)、[Sheet 的隐藏](https://github.com/Haruma-K/UnityScreenNavigator#create-sheet-and-transition)、[其他特性](https://github.com/Haruma-K/UnityScreenNavigator#other-features)、[PageContainer.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/PageContainer.cs)、[ScreenContainerTransitionHandler.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Shared/ScreenContainerTransitionHandler.cs)。初次评估只记录候选；用户随后确认第 1 项，其实施验收如下。

### 12.1 第 1 项实施与验收（2026-10-02）

- 核心增加 IUIResource 与 UIResourceLease，加载器改为取得资源，实例化同步执行；普通容器/生成 API 的调用保持原用法，内嵌 Tab 不变。节点销毁在显示清理后归还资源，失败仍继续必要清理并保留原始异常。Context 收尾全部加载和剩余资源，无旧接口兼容层。
- 同路径请求合并，独立持有、取消隔离、最后引用释放、重新显式取得与无人观察的后台错误收尾均纳入核心测试。追加 11 项有实际资源行为的测试，总计核心/生成器 138 项通过；构建 0 警告、0 错误。
- Godot headless 与原生 OpenGL 自测均通过 23 场景并退出 0；新增实际适配器资源共享/纯预加载/显式及 Context 释放验证，保留无晚到实例的取消测试。预期错误均明确断言，不由业务捕获或隐藏。
- Demo 菜单预备背包外壳，开窗后释放预加载持有，保留窗口和历史；内部页面按需加载。即时转场测试先预备所需资源，再验证不等待动画；不能把 playAnimation:false 当作同步资源加载保证。原生 Partner 四阶段截图生成成功，详情布局已目视检查。
- 未运行 USN、实测大资源帧耗时/加载性能、验证全部分辨率、实体手柄、导出平台或其他引擎。Godot 无底层中止接口、实际内存回收边界按 10.4；未增加池化/LRU/缓存预算/编辑器/诊断机制，也未实施本节第 2、3 项。

### 12.2 容器收起与返回（已确认并实现）

用户在资源迭代后确认实施上一轮剩余的两项容器能力，不新建版本文档。Modal 实际流程集中于 10.4；Tab 实际流程及本轮验收如下。

| 优先级 | 实际场景与此前缺口 | 实际 API |
| --- | --- | --- |
| 主项 | 商店弹窗 → 商品详情弹窗 → 购买确认，完成后直接恢复原商店。此前循环 Close 会逐层恢复中间弹窗的输入、焦点及遮罩 | ModalContainerNode.CloseToAsync(UINode target, bool playAnimation = true, CancellationToken cancellationToken = default) |
| 小项 | 任务/地图等互斥页面：点击当前标签收起内容，保留外壳与所有内嵌页面，之后按名称展开。隐藏父容器仍保留 Current，不能表达无选中状态 | TabContainerNode.HideAsync(bool playAnimation = true, CancellationToken cancellationToken = default) |

Modal 和结果等待契约见 10.4 第 3 项。CloseAsync 与 CloseToAsync 共用范围关闭，结果等待共享关闭结果；没有循环关闭、旧接口兼容层或默认业务结果。

Tab.HideAsync 只退出当前项，LifecycleReason.Hide、Source=当前项、Target=null、Phase.Exit。准备/动画成功后清空 Current/CurrentName，发布退出与有效状态通知，保留全部绑定项、控件和资源。之后继续使用 SwitchAsync(string) 重新展开原实例，重新执行进入生命周期，不重复初始化。无当前项时正常完成；prepare/播放失败或提交前取消恢复原选中，提交后的通知失败保留无选中状态并传播原异常，不回滚或静默成功。没有空白页面、历史或第二套选择对象。

Demo 增加 Merchant 和 Journal 入口。Shop/ShopItem/Purchase 为独立场景、业务 Node；Journal 父资源内嵌 Map/Tasks 基础 Node。重复点击当前日志标签明确选择 Hide，否则 Switch，重复 Switch 当前项按第 27 节正常完成。任务备注/勾选、地图缩放在页面收起和切换期间保留，离开日志窗口按普通关闭重建；不是跨窗口或磁盘存档。订单只更新保留商店里的 UI 展示，不接入真实交易、背包或 Gameplay。不增加技术按钮、故障吞错或业务诊断计数。

验收：构建 0 警告、0 错误；核心/生成器 152 项通过（新增 14 项，包含两种遮罩），覆盖无效目标、原实例/资源持有、无中间激活/动画、准备取消恢复、提交后清理/通知失败、结果等待/已提交结果/关闭失败后继续回答、Tab 保留与失败边界。Godot headless 与原生 OpenGL 均通过 25 场景并退出 0，验证两种遮罩仅保留目标层、原焦点/商店备注、结果等待结束、日志控件值及实例保持。任务/收起/购买确认/返回商店四张原生截图已目视检查，菜单采用紧凑排布避免新增入口越界；素材复用原已许可资源。截图、日志、导入缓存和构建产物不入库。未运行 USN，未验证所有分辨率、实体手柄、导出平台或其他后端。

临时历史页、整体操作回调、输入/遮罩新策略、处理器顺序、动态 Tab、编辑器、池化和诊断继续待定，不随本轮默认加入。

### 12.3 输入阻挡范围的必要性复核（2026-10-03，建议待确认）

容器返回/收起交付后曾建议优先补第 12 项，用户要求核对必要价值与其他框架处理方式。复核结论：转场期间限制交互属于基础能力，本工程已经具备；普通容器可选择全 Context 阻挡属于有实际用途的便利配置，但不是当前必需缺口。此前优先级建议过高，现改为建议暂缓，仍待用户决定，不授权实现或删除现有能力。

当前普通容器在操作期间锁自身及后代，其他独立区域仍可交互；Modal 在准备/转场期间临时锁 Root，稳定后限制输入归属到当前弹窗；同容器结构请求按第 27 节排队。这三种机制已有明确职责。新增候选只解决普通容器操作期间同时禁止其他区域交互，不解决跨容器事务、操作排队、脚本调用互斥或 Gameplay 输入。

真实适用场景是全屏菜单切换会同时更新内容、独立侧栏及外壳状态，需要这些入口在整段准备/转场期间共同停止接收用户操作。仅切换局部物品详情时，其他区域可以继续交互，现有局部锁已经足够。把侧栏与内容组织到共同业务外壳、在项目操作方法中禁用相关入口，通常也能解决前一场景；不要求框架立即增加全局策略。直接修改后端交互状态应由项目统一归属，不能与框架状态写入互相覆盖。若多个容器反复需要相同规则，且项目开始重复维护等待、取消、失败和重叠锁释放，才有足够理由由框架统一承担。

| 方案 | 官方处理方式 | 可借鉴点与边界 |
| --- | --- | --- |
| UnityScreenNavigator | UnityScreenNavigatorSettings 的 EnableInteractionInTransition 默认 false，ControlInteractionsOfAllContainers 默认 true；可允许交互或只阻挡正在转场的容器。源码 Begin/End 修改已登记 Page/Modal/Sheet 容器的 Interactable，全部转场结束才统一恢复 | 证明范围选择有成熟先例；配置属于全局设置，而非逐容器策略。锁全部已登记容器不等于拦截整个引擎输入或不归属容器的 UI。同容器重复转场仍抛异常，允许交互不代表允许并发 |
| Unreal CommonUI | 激活 Widget 树和 Action Router 根据当前活动根、层级与输入处理决定输入接收者；未激活 Widget 不参与动作路由 | 借鉴激活与输入归属。官方输入指南描述的是路由机制，不能据此断言其提供与 USN 一样的全局/局部转场锁开关；本次没有核验完整 CommonUI 转场源码 |
| Unity uGUI | CanvasGroup.interactable 控制组内控件交互，blocksRaycasts 控制图形射线检测是否考虑该组 | 项目可以在合适的公共外壳控制交互。blocksRaycasts=false 会令射线忽略该组，可能穿透到下层，不能当成全局禁输入；这类基础属性本身不管理异步操作和锁持有 |

若后续确认框架化，先选择最小统一阻挡机制或局部/Context 两档配置，不直接承诺逐容器策略、可不阻挡模式、操作队列或键位系统。必须明确阻挡涵盖加载/准备/转场，以及完成、取消、失败、销毁和重叠持有的释放边界；普通代码主动调用其他容器的 API 仍受各自操作契约约束，UI 输入锁不会自动禁止这些调用。

其他候选仍按具体需求选择：整体操作通知先判断 await 后更新外壳是否足够；动态 Tab 用于大型独立资源页面，但保持内嵌默认；临时历史页先判断普通加载面板是否足够；更多遮罩、处理器顺序与编辑器不捆绑实现。此次为官方文档与 USN 源码静态核对，没有运行其他框架；只修改调研记录，不改变核心、适配器或 Demo，不重复执行运行测试。

来源：[USN 交互配置](https://github.com/Haruma-K/UnityScreenNavigator#enable-interaction-during-transitions)、[USN ScreenContainerTransitionHandler.cs](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Shared/ScreenContainerTransitionHandler.cs)、[CommonUI 输入技术指南](https://dev.epicgames.com/documentation/en-us/unreal-engine/commonui-input-technical-guide-for-unreal-engine)、[Unity CanvasGroup](https://docs.unity3d.com/cn/6000.0/ScriptReference/CanvasGroup.html)。

## 13. 星庭远征：完整重建客户端 Demo（2026-10-03，已确认）

用户批准在当前 `demo/partner-screen-transition` 分支重建全部 Demo，不保留旧菜单、场景或兼容入口，不合并、不推送。目的为验证框架在完整游戏客户端 UI 中的实际使用，不借此增加新的框架功能。原创中文奇幻二次元客户端「星庭远征」采用浅色面板、金色装饰、角色立绘和幻想背景，基准 1600×900，验收窗口 1280×720、1600×900、1920×1080。鼠标与键盘焦点可操作，确认弹窗 Enter 确认，Esc 按层级返回；不包含网络、Gameplay、聊天、战斗奖励或服务器时间。

### 13.1 模块与业务闭环

| 模块 | 当前实现 |
| --- | --- |
| 标题／大厅 | 本地档案、实际资源预备后进入；玩家资源、六名看板角色切换、活动轮播、任务可领取与邮件提示，全部模块入口 |
| 角色／装备 | 六名角色、属性与资料；选择可用装备、卸下、金币与材料培养，属性随装备与等级更新；重复装备当前物品不推进目标 |
| 背包 | 十二件装备、八类材料，内嵌分类、搜索、收藏过滤、名称／属性／数量排序、详情；可选不同装备开两张对比浮窗 |
| 编队 | 四个槽位，选择、替换、清空；禁止重复角色，已入队项禁用 |
| 关卡 | 六个剧情关卡、四个挑战条目、目的地详情与队伍出战确认；确认后结束 UI 流程，不扣体力或发战斗奖励 |
| 任务／成就 | 各十二个行为目标，购买、培养、装备、编队、邮件等操作推进，达标后只能领取一次 |
| 商店 | 装备、材料、礼包，数量与费用、购买确认、奖励展示；扣金币与增加全部礼包物品属于一次保存 |
| 邮件 | 八封来信，正文、已读、附件、单封／批量领取、确认删除已领取邮件；已领取附件不可重复获取 |
| 好友 | 六名本地联系人，筛选、资料、收藏和移除；无聊天或在线服务 |
| 活动 | 庆典、签到、采购三个固定活动；签到每个档案一次，采购目标由购买推进，无日历重置；目标可收起再恢复 |
| 资料／设置 | 昵称、头像、看板与行为统计；音量写音频总线，窗口／全屏实际切换，画质为偏好模拟；完整／简化演出、确认恢复默认、素材署名 |
| 返回／退出 | 优先顶层弹窗，再关闭对比浮窗，再返回页面历史；大厅确认回标题，窗口关闭请求显示退出确认；重启读本地档案 |

预设档案提供 12,000 金币、600 星石、50 徽章，每件装备两份，每类材料 24 份。金币用于购买／培养，星石为活动奖励资源，徽章只作展示。没有充值或真实支付。业务限制用禁用按钮与文字说明表达；直接绕过业务条件调用仍明确报错，不以默认结果保底。所有入口有实际可操作内容，无技术菜单或诊断面板。

### 13.2 框架职责与资源结构

单个 UIContext，默认 Root 下挂项目 Client、Comparison、Dialogs。Client 使用 PanelContainerNode 保留单实例标题和外壳；Shell 自己包含 StackContainerNode，业务页面按资源加载。返回使用 PopAsync，回大厅使用 PopToAsync 原 Lobby 实例，不用重新创建首页代替返回。Comparison 为最多两张明确多实例浮窗；Dialogs 使用共享遮罩 ModalContainerNode。商店详情 → 购买确认完成后 CloseToAsync 原详情，再显示奖励，关闭奖励恢复原详情。等待确认使用 DialogNode<bool>。

分类页及设置页均在父 `.tscn` 中预置，绑定既有 Control，按中文名称 SwitchAsync(string)，不克隆分类资源；基础分类不要求业务 View。Events 的目标页 HideAsync 后 Current 为空，恢复同一实例。所有正常交互控件预置于场景，业务只填充数据、绑定事件，避免运行时新增控件越过适配器输入快照。场景集中于 `Demo/Godot/Scenes/Client`，可直接在编辑器调整。

DemoApp 负责初始化、资源预备、项目操作期间禁用外壳入口、返回和任务观察；ClientData 负责目录、模型与保存；ClientViews 负责页面；ClientModals 负责弹窗及表现。Source Generator 注册业务资源，不新增路由或 Navigation／Selection 对象。沿用容器操作锁，复杂演出使用项目输入门，只允许跳过；不引入核心全局输入策略。测试和截图工具分别置于 Tests、Tools，不混入玩家界面。

### 13.3 本地状态与异常

统一 PlayerState 保存货币、物品、角色等级／装备、队伍、行为目标／已领取集合、邮件、好友、资料和设置。View 不单独持有业务真相。ClientStore 每次复制当前快照，执行整项修改与验证，同步写 `user://client-save.json.tmp`，替换正式 `user://client-save.json`，成功后才发布新状态与刷新事件。写入失败不发布部分状态；不自动清空临时文件来掩盖失败。

首次正式文件不存在才使用预设档案；存档 JSON 要求全部字段，禁止未知字段，验证项目 ID、数量、等级与队伍等真实模型约束。损坏、缺字段、非法状态或文件写入失败直接传播原始异常，不重置、不恢复备份、不默认返回空数据。暂未实现存档迁移、云同步或跨进程写入。存档更新同步执行，资源加载、生命周期、转场、弹窗结果及清理保留必要异步。

正常业务不 catch，异步任务集中由 DemoApp 帧入口观察并抛原始异常；框架保留既有日志与清理契约。预期错误断言只在自测代码。自测与截图分别使用 `user://client-test`、`user://client-capture` 隔离档案，不修改玩家档案。

### 13.4 动画分工与生命周期

| 方式 | 场景与职责 |
| --- | --- |
| 场景 AnimationPlayer | Lobby 立绘／欢迎／任务／活动分层入场，Training 成长立绘／结果／星光，Reward 外壳／光效／最多三张实际奖励卡错峰出现，所有常规模块的卡片／格子／列表入场。原生 `.tscn` 轨道，可编辑器预览 |
| GodotTransitions | 页面推入／返回、分类切换、弹窗内容缩放淡入与遮罩，沿用已有动画等待／恢复能力 |
| Tween／自定义 | 按钮悬停反馈、运行时看板淡出换人再淡入；ItemPartnerTransition 用实际矩形让背包物品连续展开／缩回详情 |

普通转场约 0.12–0.28 秒，物品连续过渡约 0.48 秒；大厅、培养、奖励完整演出约 2 秒，常规模块入场约 1.05 秒，活动／关卡约 1.2–1.4 秒。CinematicTransition 为项目代码，通过已有 GodotTransitions.Animation 接入等待，捕获轨道原属性用于结束／取消／失败恢复，不增加核心动画编排系统。轨道没有方法调用业务，没有购买、扣款、发奖或存档；奖励先保存一次，再显示演出。跳过只推进 AnimationPlayer 到终帧，不重新执行业务。框架恢复表现快照，项目解除输入门并停止临时粒子。

复杂入场期间只允许跳过，键盘焦点移到跳过按钮，Esc 和窗口关闭请求不启动其他流程；正常动画结束后由框架恢复控件输入。看板切换禁用对应按钮与外壳入口，避免重复点击。页面有效失活时大厅持续装饰与轮播暂停，恢复继续；简化演出以五倍速度播放复杂时间轴，并关闭粒子与持续装饰。按钮 Tween 在页面销毁时停止并解绑；伙伴动画的临时图像立即清理，源／目标显示恢复。伙伴样式仅匹配背包与物品详情；直接回大厅明确选择普通转场，不通过失败后降级。

### 13.5 本轮发现的适配器错误

连续关闭／开窗时，Godot QueueFree 在帧结束前保留旧兄弟节点，下一转场捕获旧 sibling index，待释放后恢复顺序出现引擎越界错误。GodotUIElement 销毁拥有的场景根时先 RemoveChild，再 QueueFree，让逻辑销毁与挂载层级一致；不钳制索引或吞掉引擎错误。实际 Godot 自测验证关闭／取消后挂载无残留、快速连续返回、原实例与焦点恢复。渲染无关核心 API 未改，也没有顺带实施第 12.3 节待定输入策略。

### 13.6 素材与验收

六名成人幻想角色透明立绘、大厅及活动背景由 imagegen 为本项目原创生成，原文件已本地化，实际提示记录于 `Demo/Godot/Assets/SOURCES.md`。十二张既有 game-icons.net SVG 继续使用，作者 Lorc／Delapouite，CC BY 3.0，保留许可与修改说明；中文字体从官方 Noto CJK 仓库取得，SIL OFL 1.1。自制星形与光晕 SVG 无外部依赖。移除未使用的 Adventure／Nunito 旧素材与样式，游戏内署名入口提供作者与许可证信息。

验收：解决方案构建 0 警告、0 错误；核心／生成器 152 项测试通过。Godot headless 与原生 OpenGL 客户端／适配器自测各 78 项通过，另以独立 headless 进程通过 4 项重启存档检查。覆盖所有模块进入／返回、购买取消／余额不足、礼包单次提交、装备属性／任务／成就奖励闭环、重复奖励限制、邮件批量领取、编队重复限制、设置恢复、实例／历史／焦点、动画完成／跳过／取消／销毁／原始异常传播与清理；存档缺字段、未知条目和写入失败均报错。旧 Demo 的实际适配器测试迁移到 Tests/DemoApp.FrameworkTests.cs，继续验证借用控件所有权、自定义服务、原生转场方向／tint／scale／pivot 恢复、加载取消无晚到实例、资源共享／纯预加载／实例持有／最终释放、初始化／转场回滚的原异常，以及 Partner 进入／返回取消、原异常清理和即时无动画流程。预期故障仍输出框架原始错误日志，并由测试明确断言；业务不捕获，不能将未断言的错误当作成功。

1280×720、1600×900、1920×1080 原生渲染各生成并目视检查 25 张截图（共 75 张），涵盖全部模块、设置三个分类、角色／物品详情、双装备对比、购买确认、培养／奖励／伙伴转场中间帧与终态，未发现缺字、遮挡或布局越界。截图工具按实际 PNG 尺寸记录，使用隔离档案。运行脚本执行构建与资源导入，自测总脚本另启动独立进程验证存档。没有把时间轴异常日志当作成功，也不使用强制帧数退出代替完成自测。

未运行 USN，未验证其他引擎、导出平台、实体手柄、跨设备性能或联网；音量已接音频总线但没有新增音乐资产，设置自测检查保存／恢复状态，不能据此宣称验证所有显示设备的全屏行为。日志、截图、隔离存档、生成工具、构建产物和 Godot 缓存不入库。

### 13.7 标题页鼠标点击修复（2026-10-03）

用户反馈标题页无法点击继续。Main.tscn 中 ShellMount／FloatingMount／ModalMount 是结构挂载，但使用了 Control 默认 MouseFilter.Stop；空的浮窗和弹窗挂载仍铺满窗口，位于标题按钮上方，阻挡鼠标命中。三个挂载改为 MouseFilter.Ignore，具体页面按钮、模态遮罩及复杂演出输入门继续承担实际输入职责，不修改核心输入策略或添加保底。

此前自测直接调用 EnterAsync 或发送按钮 Pressed 信号，没有验证 GUI 命中，因此不能证明鼠标可以进入。现在自测从标题按钮中心向 Viewport 推送实际鼠标按下／释放事件，等待事件触发的业务任务进入大厅，再用相同路径点击商店与回大厅；不以调用 EnterAsync 代替点击。修改前明确复现“鼠标点击未到达”，修改后验证标题与大厅入口可以命中。该回归验证纳入长期自测；更新场景后需重启已运行的 Demo 才会使用新挂载配置。

验证：构建 0 警告、0 错误；Godot headless 和 1280×720 原生 OpenGL 自测各通过 79 项并正常退出，包含上述鼠标命中回归和既有框架／业务测试。没有修改渲染无关核心，因此本轮未重复运行核心测试，也未重拍全部模块截图。

### 13.8 游戏客户端演出扩充（2026-10-03）

用户要求大量不同的华丽过渡，且复杂过场必须实际使用时间轴编排。围绕现有游戏操作扩充项目表现，不增加核心 API、动画编排系统或技术展示菜单。

| 游戏操作 | 实际演出 |
| --- | --- |
| 标题进入大厅 | 双侧幕帘、庭院字幕、背景推镜、立绘滑入、欢迎及功能卡错峰落位，2 秒 |
| 角色培养 | 深色帷幕、立绘扫入、旋转星阵蓄光、闪光与金色横向光带、属性结果收束，2 秒 |
| 购买／领取奖励 | 星阵扩散、光带、面板弹入、最多三张实际奖励卡依次跃出、结果和关闭按钮最后出现，2 秒 |
| 角色／好友 | 卡片旋转扇形进入；角色详情菱形金边展开 |
| 背包／商店 | 物品格子波纹式缩放弹入；背包分类斜向擦除，商店分类及购买窗卷帘展开 |
| 编队／任务／成就／邮件 | 编队槽位上升，目标行交错滑入，邮件行纵向展开；邮件详情金边斜向揭开 |
| 活动／关卡 | 插画推镜、标题升起、分类和内容分阶段进入 |
| 资料／确认／出战／装备对比 | 资料弹性缩放、确认上升、出战横移、两张对比卡分别从两侧进入 |

以上时间轴均在对应 `.tscn` 的 AnimationPlayer 内，直接在编辑器预览 `enter`／`exit`；持续装饰仍由页面激活状态控制。ClientTransitions 是项目配方，使用已有 Transitions.Select、GodotTransitions.Simple 和 IUITransitionRun；GildedRevealTransition 使用同一独立 ShaderMaterial 让页面及子控件按屏幕位置连续揭开，播放后恢复每个节点原材质引用。取消和失败也恢复，原异常直接传播；不补默认材质或降级淡入。时间轴在布局完成后设置实际中心 pivot，播放结束恢复轨道属性和 pivot。星阵为本项目自制 SVG，轨道不触发业务。

截图发现初版大厅在 Shell.OnInitializeAsync 中进入并播放，但父外壳尚未显示，导致长演出期间只有输入门可见。重构为先打开外壳，再由 EnterLobbyAsync 推入大厅；不手动强制显示未完成初始化的父节点。截图工具采样实际 AnimationPlayer 时长，每个常规模块五帧、主要演出十帧，并记录 Shader 与 Partner 中间帧；使用隔离档案。

查看完整效果需启用设置→演出→完整演出与装饰动画。简化模式保持明确的五倍时间轴速度及短淡入，关闭粒子和持续装饰；两种模式都使用原业务流程。复杂演出只允许跳过，跳过不重复扣款或发奖。

#### 条件与来源分支

同一页面从大厅 PushEnter 进入时播放完整时间轴；从另一功能页进入时按项目入口顺序决定滑动方向（背包→商店从右进入，商店→背包从左进入）；PopEnter／PopExit 使用短返回，避免返回时反复播放长演出。简化演出直接选择短淡入。背包↔物品详情优先保持 Partner 所需稳定几何位置。全部由 ClientTransitions.Page 的一个 Transitions.Select(Func<TransitionContext,IUITransition>) 读取 Source、Target、Phase 和设置表达，不新增来源枚举或框架状态机。

同一个 RewardView，从商店详情等来源进入采用星阵和奖励卡时间轴；从邮件详情进入采用短金边揭开，适合连续领取附件。Presentation 配方直接匹配 context.Source 的 ClientModalView.Parameters.Kind，不在 View 中保存上一页面名称。关闭阶段单独选择缩放淡出。只有业务本身需要区分流程的入口参与分支，没有为了演示编造随机条件。

复现路径：大厅→背包→商店→背包→返回；分别可见完整入场、两个方向的横移及短返回。邮件→单封正文→领取附件与商店→礼包→确认购买，展示同一奖励窗口按来源选择不同动画。截图和真实容器测试验证这些路径。

邮件单封领取现在保留正文窗口：提交附件后在其上显示奖励，关闭奖励恢复同一正文并显示已领取／删除操作。这既保留阅读上下文，也让 Source 真实指向邮件窗口；不是通过虚构来源标签选择动画。批量领取流程仍返回邮件列表。

本轮验证：解决方案构建 0 警告、0 错误；核心／生成器 152 项通过；Godot headless 与 1280×720 原生窗口各 97 项通过，独立进程重启检查 4 项通过，均正常退出。新增检查覆盖奖励时间轴中间阶段与跳过后的 scale／pivot／星阵／输入恢复；三种 Shader 完成、取消、原异常传播和材质引用恢复；真实页面不同 Source／Pop Phase 分支；真实单封邮件领取的来源分支与正文恢复。原有框架／业务覆盖继续保留。1280×720、1600×900、1920×1080 各生成 112 张实际 PNG，检查全部修改模块终态及幕帘、星阵、奖励卡、卷帘、菱形和来源切换中间帧，没有发现布局越界或残留效果。截图与 GIF 预览不纳入提交。核心 API 未改变；未增加性能基准或其他平台验证。

### 13.9 Godot 动画首帧时序修复（2026-10-03）

用户反馈动画开始闪烁。审核确认三处首帧时序问题：AnimationPlayer.Play 只安排后续轨道求值，没有立即应用起点；项目 CinematicTransition 在节点已显示后先等待布局帧；GildedRevealTransition 在等待布局后才安装遮罩材质。后两者会在等待期间暴露资源中用于编辑预览的最终状态，随后跳到动画起点。

GodotTransitions.Animation 在 Play 后同步 Advance(0)，确保首次让出执行前已经采样轨道起点。CinematicTransition 在等待布局前将进入页面根节点的 Modulate alpha 设为零，布局完成后恢复原颜色并立即启动、采样时间轴，中间不等待帧；保持 Visible 使 Container 正常参与布局。完成、取消或失败时恢复原颜色、轨道属性与输入门。GildedRevealTransition 在首次等待前安装独立材质并设置进入 progress=0／退出 progress=1，布局完成后再计算实际屏幕矩形并推进动画；继续恢复每个控件原材质引用，不降级或隐藏异常。

核心 TransitionFlow 从显示节点到调用转场之间没有异步让出；资源加载器在挂载前已隐藏实例；普通 Tween 和模态遮罩在首次等待前已应用起点。Partner 转场在等待布局前隐藏原图和进入页面，以已布局的源图矩形安放飞行图，再采样实际目标矩形，无同类首帧暴露，不增加额外延时或核心 API。

验证：构建 0 警告、0 错误；Godot headless 自测 102 项通过并正常退出。新增回归验证原生 AnimationPlayer 调用后立即采样起点、复杂演出布局等待期间 alpha 遮挡及恢复显示时的首帧、三种 Shader 在首次等待前同步应用遮罩。既有跳过、取消、销毁、原异常传播与清理检查继续通过。渲染无关核心没有修改，本轮没有重复运行核心测试；为避免干扰用户窗口，没有启动原生窗口或重新截图，因此没有新增原生视觉验收结论。

### 13.10 Godot 输入、反馈与结果通知审核（2026-10-03）

继续审核 Godot 客户端及共用核心的初始化、转场、输入、异步任务观察、资源持有和生命周期清理。当前批次修复以下实际问题，不引入新 API 或全局输入策略。

- ClientView 的悬停 Tween 原先和时间轴同时写按钮 Modulate，且离开时强制写白色，会覆盖表现透明度与颜色。反馈改写 SelfModulate，并乘以资源原色；离开时恢复原 SelfModulate。时间轴继续负责 Modulate，反馈不再写其属性。回归检查时间轴隐藏按钮、轨道恢复，以及已有非白色／半透明表现不被悬停与离开覆盖；没有新增原生视觉验收结论。
- Esc 和窗口退出请求原先只检查 Busy／复杂演出门，弹窗开关通常不设置 Busy，因此转场期间可以提交重入操作。新增 headless 检查在修复前明确复现“global input cannot reenter modal opening”失败。项目入口现在依次检查退出状态、Busy、演出门及现有容器／当前层的 IsInteractive；初始化尚未完成、弹窗或浮窗开关、页面转场期间不接收这些输入，当前模态层可交互时继续优先处理模态。非法调用和生命周期重入仍抛出原异常；正常并发结构请求现按第 27 节排队，不添加业务 catch 或错误保底。
- DialogNode.Complete 原先遗漏 Context.VerifyMutation，可从只读生命周期通知提交结果，违反第 7.4 节契约。新增核心测试在修复前明确失败：Activated 中 Complete 未报错。现在复用已有守卫；通知失败保持已经提交的模态与交互状态，不发布结果，通知之外的事件仍可正常 Complete 和关闭。

验证：scripts/Test.ps1 完整通过，核心／生成器 153 项、Godot headless 客户端 109 项、独立进程存档重启 4 项，构建 0 警告、0 错误，进程正常退出。新增检查涵盖悬停／时间轴属性独立、既有透明度与颜色保持、弹窗打开／关闭及页面转场中的全局输入门控、只读通知结果守卫；既有资源、Partner、取消／异常恢复及业务闭环继续保留。本轮没有启动原生窗口、截图或调用 Unity。

分类切换输入范围的后续审核与修复已完成，见第 13.11 节；测试通过不等于整个工程不存在其他错误。

### 13.11 Godot 分类及资源准备期间的输入范围（2026-10-03）

审核确认分类内容与分类按钮、筛选框、外壳返回不在同一个物理挂载：Tab 容器锁内容区，外部选择控件仍能收到下一次操作。设置分类的关闭按钮、活动页的收起按钮也在内容挂载外。登录准备资源时尚未进入容器转场；大厅角色切换只禁用该按钮和外壳，其他大厅入口仍可触发 Busy 重入错误。这些是项目输入范围的问题，不修改核心容器的锁或添加全局策略。

DemoApp 增加项目内部 RunAsync(UINode owner, Func<Task> action) 重载，沿用 Busy 与外壳入口禁用，并使用既有 GodotUIElement 输入门暂时禁用 owner 的完整界面。结束时按 owner.IsInteractive 恢复资源输入配置，并恢复可用的原焦点；不覆盖 Button.Disabled 等业务条件。owner 已在销毁时不访问其原生控件；子容器仍由核心负责取消、恢复与清理。无捕获、重试、操作队列或保底动画，直接调用非法重入仍报错。

背包等页面的 SelectTabAsync、设置分类、活动 SelectAsync／FoldAsync、登录 EnterAsync 和大厅 SwitchHeroAsync 使用该范围。初始化绑定和默认分类选择仍走原核心流程，不在初始化时进入项目操作锁；已选择的分类仍是业务上的无变化选择。FoldAsync 直接 Hide／Switch，同一操作不再嵌套调用另一层项目 RunAsync。

验证：本轮 scripts/Test.ps1 通过核心／生成器 153 项、Godot 123 项与存档重启 4 项；追加页面销毁检查后，最终 Godot headless 124 项及独立进程重启 4 项通过，均正常退出，构建 0 警告、0 错误。新增 15 项覆盖分类按钮、筛选及外壳禁用；真实鼠标重复点击与 Esc／关闭请求；完成后的焦点和输入恢复；原异常身份、取消后的原选择；设置分类与关闭／跨层返回互斥；活动收起／恢复保留实例；重新登录等待资源时的重复点击；子操作中销毁页面后无原生节点或残留 Busy／入口锁。既有大厅角色切换检查扩充为验证其他大厅入口同时禁用。测试使用隔离档案，不改正常玩家存档。

本次 Godot 相关审核检查了初始化与动画首帧、核心容器流程及结果通知、适配器资源持有／输入与焦点、Demo 操作期间的输入范围及清理，并完成已确认问题的回归验证。没有启动原生窗口或截图，因此不新增像素层视觉验收；没有调用 Unity，也不将第 14 节待验证的 Unity 演出登记为完成。

## 14. Unity 适配与原生 Demo（2026-10-03）

用户确认增加 Unity 适配和 Unity Demo，通过 Unity CLI 操作普通编辑器，不使用 BatchMode。开发期间不得反复聚焦或切换到 Unity 窗口；重编译使用 focus=false，已连接的项目不重复 open，Game View 分辨率工具不请求焦点。

用户再次反馈后台 CLI 仍干扰窗口后，停止全部主动 Unity 编辑器调用，包括重编译、Play Mode、测试、截图及切换编辑器页面。当前只进行文件修改；不得仅因设置 focus=false 就认为操作不会抢焦点。后续原生验证暂缓，不能将未验证修改登记为完成。

### 14.1 依赖与职责

`src/UIFramework.Unity` 是本地 UPM 包 `com.ui-framework.unity`，当前使用 Unity 6000.3.24f1、uGUI 2.0.0 和原生 Animation 模块验证。纯 C# 核心仍是 netstandard2.1，未改为 Unity 专属代码。运行脚本构建核心 DLL 到包内 Runtime/Plugins；DLL 和缓存不入库，首次打开前必须准备。没有声称验证其他 Unity 版本、移动端、IL2CPP 或 Unity 内 Source Generator；Demo 采用普通 C# 注册。

UnityUIHost 提供主线程检查、帧等待、EventSystem 焦点快照和日志。UnityFramePump 用 MonoBehaviour.Update 完成帧等待，禁用时让未完成等待明确失败；不后台轮询 Unity 对象。UnityUIElement 适配 RectTransform，拥有的 Prefab 根销毁前先失活／脱离父级，再 Destroy，避免延迟销毁造成兄弟顺序不一致；内嵌节点借用已有 RectTransform，不销毁项目控件。

CanvasGroup 门控不覆盖 Button.interactable 等业务限制。锁定时让子 CanvasGroup 不忽略父级门控，恢复时采用原资源设定。空结构挂载不创建可射线命中的 Image，不能挡住标题按钮；实际模态遮罩负责屏蔽下层输入。运行期间新增跨越已有输入快照的控件不属于本 Demo 工作流。

Unity 遮罩颜色（2026-10-07）：ModalBackdrop 的 Button 仅用于点击关闭请求，显式设置 `Selectable.Transition.None`，避免默认 Color Tint 在悬停／按下／禁用状态下修改 Image 的渲染颜色。遮罩颜色由配置的 Image.color 决定，淡入／淡出只修改 CanvasGroup.alpha。本次小改动仅核对源码与差异，未构建或运行原生验证。

### 14.2 使用 API

```csharp
using UIFramework;
using UIFramework.Unity;

var ui = UnityUI.Create(canvas, eventSystem);
ui.Register<InventoryView>("UI/Inventory"); // Resources 相对路径，无扩展名。
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pageMount);
pages.ChildTransition = UnityTransitions.SlideHorizontal();
await pages.PushAsync<InventoryView>();

var dialogs = await ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", modalMount);
dialogs.ConfigureBackdrop(UnityBackdrops.Dim(observeTask), ModalBackdropMode.Shared);

var tabs = await parent.CreateContainerAsync<TabContainerNode>("Categories", embeddedMount);
await tabs.BindAsync("装备", equipmentRect);
await tabs.BindAsync("材料", materialsRect);
await tabs.SwitchAsync("材料");
```

Create 不创建 HUD／Overlay 等默认业务层。挂载和绑定有生命周期，保留异步；组件查询、配置和注册同步。GetRect／GetComponent<T>(path) 获取真实 Unity 控件，路径或组件缺失明确报错，不生成默认控件。所有异步操作须等待或由项目帧入口观察原始异常。关闭项目时先 await ui.DisposeAsync，再销毁 Canvas／EventSystem；编辑器强制停止不等于可等待的正常业务关闭。

### 14.3 资源与转场

默认 UnityPrefabLoader 使用 Resources.LoadAsync<GameObject>，要求 RectTransform 根。Unity ResourceRequest 不可取消，因此取消取得时先等待请求结束，检查取消后不创建晚到实例。资源与实例保持核心既有持有关系；PrefabResource.Dispose 释放适配器引用，实例 Dispose 销毁 GameObject。Resources 的引擎缓存由 Unity 管理，不能将引用释放说成立即卸载纹理／GPU 内存；项目可注入自己的 IUIResourceLoader 管理 Addressables 等资源，本轮不实现该加载后端。

UnityTransitions 提供 Fade、ScaleFade、SlideHorizontal、Simple 帧参数和 Animation。简单转场保存 RectTransform 位置／scale 及 CanvasGroup alpha，完成、取消、失败均恢复。Animation 等待原生非循环 legacy AnimationClip，以未缩放时间设置 time 并 Sample；保存／恢复 speed、time 和 wrapMode，timeScale=0 时可以正常展示 UI。项目提供轨道属性恢复函数，不把业务提交放入时间轴。未播放完成却被外部 Stop 时明确报错，不能将中途停止当成功。

条件与来源依然只有 Transitions.Select(Func<TransitionContext,IUITransition>)。Unity Demo 同一功能页从大厅进入缩放、背包→商店向左侧移动旧页并从右进入新页、商店→背包反向、返回用短横移、简化模式短淡入；同一个 RewardView 从 PurchaseView 进入播放完整奖励时间轴，其余来源使用短缩放，简化模式也用短缩放。直接读取 Source／Target／Direction／Entering 和玩家设置，不保存额外“上一入口”状态，也不新增核心机制。

### 14.4 Demo 范围与验收

Unity Demo 保持原创中文「星庭远征」美术和真实游戏操作：本地标题／大厅、六角色属性／装备／培养、八件装备背包与内嵌材料页、四槽编队、商店数量与确认购买、单次任务奖励、双对比浮窗、嵌套画质设置及演出偏好。十五个业务 Prefab、测试共用的 ClientRoot Prefab、原生场景和四个 AnimationClip 保存于 Assets，可直接编辑；正常运行加载资源而非重建所有控件。未移植 Godot 的邮件／活动／关卡等模块，未接 Gameplay 或联网。两端范围和动画表现不能混用验收结论。

ClientStore 独立于 View，统一存储货币、库存、等级、装备、编队、购买计数、任务领取与设置。复制快照并验证，同步写临时文件和替换正式文件，成功后发布；损坏或写入失败原异常直接传播。存档位于 Application.persistentDataPath/unity-client-save.json。自测使用隔离 GUID 档案，不读写玩家档案。

UnityClient 管理单个 UIContext、项目 Stack／Panel／Modal 挂载、外壳入口锁、返回和异步任务观察；无全局输入策略或诊断菜单。模态层还原、双比较数量限制与状态刷新使用直接项目逻辑。核心 API 未修改，Godot Demo 未迁移或删除。

当前阶段状态：此前 16 项原生测试通过，三种分辨率各生成 19 张截图；但进一步检查发现入场轨道没有完整持久化，之前仅检查完成／恢复不足以证明复杂演出有效。已调整创作脚本为全部轨道编排结束后创建动画资产，并增加实际中间属性断言。这次修正尚未重新构建资源或完成原生验证，现存 `.anim` 不能视为已完成交付。遵照用户停止窗口干扰要求，后续验证暂缓。Unity 适配和 Demo 尚未记录为全部完成。

只读查看最后一次测试输出文件，确认新增断言后为 15 项通过、1 项失败；失败为 Lobby 中间立绘位置应处于 506–759，实际为 505，验证了缺失轨道问题。没有重新调用编辑器取得该结果。运行脚本无操作开关时仅准备核心 DLL，避免默认进入编辑器流程；即便 focus=false，也不承诺 Unity 自身测试／播放流程不会抢焦点。

离线源码检查：使用本地 Unity 6000.3.24f1 引擎／编辑器／uGUI 程序集和 netstandard2.1 核心 DLL，以 C# 9 编译全部适配器、Demo 业务及编辑器创作脚本，0 警告、0 错误；仅运行 dotnet 编译器，不运行 Unity 引擎方法。PowerShell 运行脚本语法检查通过。该检查证明源码类型及 API 引用可以编译，不证明时间轴资源已生成或实际播放正确；离线检查工程及输出位于忽略的 artifacts 中。

## 15. 框架代码审核

本轮审核核心销毁与生命周期、资源释放、生成器、Godot 输入与焦点，以及 Unity 遮罩和转场。修复已复现或源码能够直接确认的问题，不新增功能、诊断系统或兼容入口。

- UINode 原先先执行 DisposeCoreAsync 再缓存任务；Lifetime.Cancel 的同步回调可以再次发起销毁，导致重复清理。现在先发布共享任务，再开始取消与清理。整个清理过程保持生命周期执行范围，继续禁止在节点自己的生命周期中销毁自己或祖先。正常外部重复请求共享同一个任务、完成状态和原始异常；任务代理通过 Unwrap 保留取消状态，不把取消转换为普通失败。
- UIContext 原先在资源释放开始后才缓存销毁任务；资源释放回调再次请求销毁时，可能取得成功任务，而外层实际释放失败。现在执行资源清理前发布共享任务，重复请求观察同一失败；资源和节点仍只清理一次。
- Source Generator 原先把所有类型化方法放在同一个命名空间；Game.Lobby.SettingsView 与 Game.Battle.SettingsView 会产生重复方法，导致整个工程无法编译。类型化方法现在生成到节点所属业务命名空间，全局命名空间节点的方法也位于全局命名空间。注册仍集中在 UIFramework.Generated.GeneratedUIRegistration。使用方导入节点命名空间即可调用 OpenSettingsViewAsync 等方法；同时导入两个同名节点所在命名空间时，使用 Game.Lobby.GeneratedUICalls.OpenSettingsViewAsync(container) 或核心泛型 API 明确目标。不生成别名、不跳过节点、不保留旧位置转发。
- Godot RestoreFocus 原先可将焦点恢复到已经 Disabled 的按钮。现在与初始焦点选择一致，只恢复仍可用的按钮；失效目标不抢走当前焦点。
- Unity 模态遮罩 Hide 原先只将 alpha 动画到零，随后恢复 alpha，没有实际隐藏节点。现在成功结束时提交 show 对应显隐；取消或失败恢复先前显隐和透明度，传播原异常。
- Unity Animation 转场原先先等待一帧才播放，会暴露资源预览状态。现在播放后同步设置 time=0 并 Sample，在首次等待之前应用轨道起点；取消、清理和原属性恢复继续沿用既有契约。

验证：scripts/Test.ps1 通过核心／生成器 157 项、Godot headless 客户端 126 项、独立进程存档重启 4 项。回归包括同步取消回调重入、清理取消状态及异常身份、资源释放失败共享、不同命名空间同名节点的编译与实际调用、禁用按钮焦点恢复。构建 0 警告、0 错误。

Unity 适配器、Demo、创作脚本及测试使用本地引擎程序集离线编译，0 警告、0 错误；新增遮罩显隐／取消和时间轴首帧两项原生测试，但未运行。没有启动 Unity 编辑器或原生 Godot 窗口，不新增视觉验收结论；第 14 节 Unity 资源轨道问题及原生验证仍待处理。此次测试通过不代表框架不存在其他错误。

## 16. 中英文用户文档（2026-10-05）

用户确认建立可在 GitHub 阅读并构建为静态站点的中英文文档，以 Netick 的 Getting Started、Manual、Samples 与 API Reference 为目录层次参考。用户文档置于 docs/site，中文默认、英文镜像 en；每种语言 29 页，另有双语文档维护说明。此次明确允许按使用主题拆分用户教程；内部通用决策、待定范围和验收仍集中于本文，不另建主题设计文档。

使用 VitePress 1.6.4，锁定 npm 依赖，配置双语侧栏、页内目录、本地搜索、代码复制、语言切换和前后篇导航。Markdown 正文使用相对 .md 链接，在 GitHub 不依赖站点插件即可阅读。根 README 改为产品能力、双语入口、运行步骤、支持边界与维护者记录；不把历史候选作为用户功能清单，也不虚构公开包、远程地址、下载版本或框架许可证。

Getting Started 以 Godot 和 Unity 的“创建一个简单窗口”为第一条可操作路径：手工制作场景资源／Prefab，编写窗口 UINode 与原生宿主脚本，再挂载宿主并连接资源。Godot 提供可编辑资源、教程工程和独立 headless 自测；Unity 明确给出 Prefab 与 Inspector 步骤。示例手写注册，不依赖生成器；按钮异步任务由帧入口观察并直接传播原异常。首课只展示单实例打开、关闭和再次打开，复杂菜单、Tab、确认及退出流程留给 Manual 与 Samples。

Manual 以背包保留、页面返回、设置画质弹窗、购买结果、双对比卡、活动收起和来源动画分支说明实际能力。API 索引按源码核对签名、默认参数与所有权；引擎配方分开描述，不把 Godot 的 delay／Tween 参数写成 Unity 能力。明确手写 Register 不读取 UIResource 属性，生成器把属性路径传给注册；类型化调用位于业务命名空间。AddLifecycle 限挂载前，空闲状态可 RemoveLifecycle 取消通知，已初始化行为仍需最终清理。

GitHub Actions 的 Documentation 工作流仅在 PR 构建检查和手动 workflow_dispatch 时运行；只有手动运行部署 Pages。configure-pages 输出决定部署 base，不写死仓库名。当前仓库无远程配置，未推送、未实际发布，也未验证真实 GitHub Actions 执行。操作要求集中于 docs/site/README.md。

验证：29 对页面的对应关系、相同 C#／XML 示例、全部站内文件链接与教程代码同步检查通过。根路径和 /UI-Framework/ 子路径站点构建通过；独立 headless Chromium 检查桌面侧栏、中文／英文搜索、同页切换语言、真实代码复制、390px 窄屏菜单及资源响应，没有页面异常或失败请求。检查桌面、英文和窄屏截图，目录与正文清晰可读；截图输出不纳入 Git。

核心文档场景可编译并运行，15 项检查通过；Godot 教程构建 0 警告、0 错误，14 项 headless 检查通过，包含初始菜单、真实按钮开关、分类实例值保留、返回后键盘焦点恢复、明确取消／确认、结果清理与关闭。Unity 教程和适配源码使用本地 6000.3.24f1 程序集离线编译，0 警告、0 错误，未执行原生运行；第 14 节待验证状态继续保留。没有启动引擎 GUI 或操作用户窗口，未修改核心 API、游戏 Demo 业务及无关未跟踪资源。

### 16.1 用户文档写作规范

用户要求描述清晰、易懂、精简、抓重点。后续文档先说明用途，再给操作和必要规则；删去重复限制、维护过程和无实际用途的示意代码。入门按实际操作顺序组织，场景资源与字段明确对应，完整源码可折叠，避免大段代码打断步骤。中英文同步维护，重要行为不能因精简而省略。

本轮重写 Unity Getting Started 为准备、安装、场景、三个 Prefab、接入、运行六步；Manual 精简为用法和要点，保留实际契约。双语与链接检查、站点构建、浏览器六步顺序和源码展开检查通过；教程源文件与框架 API 未改变。

### 16.2 全仓库文档审核（2026-10-05）

覆盖 68 个 Markdown 文件：中英文用户指南、API、Demo 操作说明、素材记录及维护者文档。用户指南先说明场景，再给必要代码与限制；删除重复说明，历史计划标明阅读范围。Godot 入门统一为六步，完整宿主代码折叠展示。

修正 Node 可为纯结构节点的定义；Core API 按所属容器分组；参数与生命周期示例改为实际控件绑定、数据刷新和装饰暂停；统一装备参数示例。修正奖励流程描述、对比窗返回列表维护，以及转场示例中的返回动画。保留完整素材许可和原生验证限制。

验证：全仓库本地链接／标题锚点、29 组双语代码和教程源码一致性通过；VitePress 根路径及 /UI-Framework/ 子路径构建通过，两种路径的 59 页浏览器检查通过（搜索、语言切换、复制、窄屏目录及布局）。核心文档示例 15 项、Godot headless 入门 14 项通过；修改后的节点参数、生命周期及生成器示例编译通过。未启动引擎 GUI；Unity 原生行为和时间轴待验证项仍保留在适配说明。

### 16.3 容器场景与使用说明（2026-10-07）

用户要求容器文档具体说明定义、使用场景和操作方式。概念页按“独立窗口、单窗口历史、内嵌分类、叠层模态”提供容器选择表；Panel／Stack／Tab／Modal 各章给出资源结构、实际代码和实例保留／销毁行为。侧栏显示容器名称，下一步与游戏场景提供直接入口，中英文同步。

明确当前行为：Modal.Open 保留下层显示，只将输入交给顶层；Stack 的稳定状态只显示当前窗口，Push 保留隐藏历史、Pop 每次销毁顶层并恢复一个历史实例。多级模态菜单用一个 Modal 外壳承载 Stack，窗口可以是独立资源，不引入新的容器模式。文档展示设置→画质→高级画质、逐级返回、首页关闭整个流程及两端挂载。转场可能短暂同时显示源／目标；不宣称当前提供串行退出再进入策略。修正旧指南“Pop 必须存在更早页面”的错误描述：最后一项可以 Pop，之后栈为空。

验证：核对当前容器、生命周期与适配扩展源码；文档检查覆盖 74 个 Markdown、32 组双语页面，示例代码镜像、教程源码、内部链接与标题锚点通过。本次只修改用户文档与站点导航，未构建或启动引擎，未执行新增示例的原生运行验证。

## 17. 引擎生命周期绑定（2026-10-05）

用户确认补全 Unity／Godot 生命周期绑定，以同步扩展方法快速配置。实际场景：退出关卡或销毁关卡对象时，关闭所属游戏 UI、窗口及弹窗，不影响其他逻辑子树；绑定不负责返回主菜单或加载场景。

- Unity：UINode／UIContext.BindLifetime(GameObject) 或 BindLifetime(Scene)。场景绑定创建场景内的生命周期管理 GameObject，HideInHierarchy 隐藏；共享观察与帧泵对象同样隐藏并跨场景存活。管理对象只属于绑定，不改动传入 GameObject 的层级、显隐或所有权。停用和从未激活的对象也能在销毁后完成清理。
- Godot：UINode／UIContext.BindLifetime(Godot.Node)，场景使用场景根节点。由 SceneTree 帧信号观察，不增加场景节点；禁用、暂停、隐藏或移出树不代表销毁，Free／QueueFree 才触发结束。
- Node 绑定调用节点的共享 DisposeAsync，清理其子树；Context 或 Root 绑定调用 Context.DisposeAsync，继续归还资源。绑定仅接受已挂载、正在初始化或 Ready 的节点，每个节点同时一个绑定。
- 返回 UnityLifetimeBinding／GodotLifetimeBinding。Completion 等待实际清理并保持原始失败与取消状态；框架观察同一个 Completion，不遗留无人观察的代理任务。Dispose 仅解除绑定，不关闭节点、不销毁传入对象；Unity 会移除自己创建的场景管理对象。主动节点销毁也会结束绑定。祖先清理过程中解除子绑定不会重复完成任务。
- 自动结束不播放退场动画；正在进行的框架操作沿用 Lifetime 取消和清理。销毁后的原生表面没有属性可恢复，仅在绑定触发的终结流程中停止写入这些表面；未绑定的外部销毁仍明确报错，已 Dispose 的显示对象仍不能继续操作。不包裹异常，不把业务回调失败降级为成功。
- 两端帧等待独立于场景对象。GodotUIHost.NextFrameAsync 公开给项目自定义动画；等待后检查取消 token，避免控件释放使信号等待永不完成。Demo 自定义过场已迁移；拥有原生场景的 Demo 节点由引擎释放原生信号与节点绑定 Tween，业务 OnDispose 只清理模型订阅及持有。
- 清理回调不依赖已经销毁的控件；需要读取控件或播放退场时，先主动等待关闭，再卸载场景。项目仍观察自己的操作任务；应用退出前等待 Context 清理，不能依赖已停止的引擎主循环。

验证：核心／生成器 157 项通过，Godot 客户端 headless 139 项通过，存档重启 4 项通过；新增生命周期专项 13 项单独通过，包含真实设置与对比窗口的外部销毁、项目时间轴／Shader 过场清理及材质释放、异步清理、重叠绑定和原始异常。Unity 6000.3.24f1 隔离 batchmode Play Mode 7 项通过，涵盖隐藏场景管理对象、场景卸载、资源与多层遮罩、未激活对象、解除绑定、重叠绑定、淡入与 AnimationClip 取消、原始错误及未绑定销毁报错。新增 scripts/Test-UnityLifetimes.ps1 和 Run-Demo.ps1 -LifetimeTest 可重复运行专项；测试存档／隔离工程／日志均在忽略目录。

适配器、Demo、测试离线编译无警告无错误。中英文指南、两端 API 与 Demo 操作说明同步更新；68 个 Markdown 的链接、标题锚点、29 组双语代码一致性及站点构建通过，59 页浏览器检查通过。此前 Unity 教程及完整 Demo 时间轴资源的待验证范围不因本次专项验证而自动宣称完成。未推送、未发布网站。

## 18. 可选 MVVM 模块（2026-10-05，已实现）

采用独立运行时、Mono.Cecil Weaver、Unity／Godot 适配和编辑器模块。普通 VM 无需框架基类；类型使用 ViewModel 标记，逐属性 FieldNotify，计算属性声明依赖，Command 标记业务方法。运行时属性和命令访问由织入代码直接调用，不使用动态代码或反射调用业务方法。

Runtime 的创建与释放现由框架内部自动管理，可跨多个 UIContext（见 18.4）。Create Instance 每个界面独立创建；Manual 接受借用实例；Shared Asset 对每个原生 VM 资产克隆一次，同一 Runtime 共享，原始资产不参与运行时写入。Unity 资产为 ScriptableObject，Godot 为 Resource。安装模块后自动启用；Core 提供渲染无关的 Context 和节点扩展生命周期。

支持四种绑定方向、嵌套路径重新订阅、显式空值表现、静态转换函数、同步／Task 命令与 CanExecute，以及 ObservableCollection／INotifyCollectionChanged 增量列表。默认错误必须抛出，缺失成员、类型错误不得变为空值表现。首版无虚拟化或额外对象池。

两端 GUI 参考 UE Viewmodels、View Bindings、控件属性入口；Unity 使用 UXML／USS，不使用 IMGUI，运行时保持 uGUI。支持原生资产发现、三种来源、拖放搜索、方向与转换、列表条目资源、保存／Undo／Prefab override，以及独立临时 Runtime 安全预览；预览不执行命令、不写原资产。Manual 无预览工厂时明确显示缺少预览来源。

验收包含两端真实游戏 UI 示例、核心和织入回归、Godot headless、Unity 原生编辑器／Play Mode、隔离 WebGL IL2CPP 示例。阶段验证后本地提交，不推送。当前已完成节点扩展和通知／访问 Weaver 初始接入：原有核心 157 项、织入后运行测试 5 项通过（setter 副作用与相等值、计算依赖、继承通知、直接命令与异常身份、双向归一化、嵌套替换与解除订阅）；列表、完整错误边界、两端适配／GUI／预览、Demo 和 AOT 验收仍在实施，不能据此宣称模块完成。

### 18.1 运行时与两端接入阶段

运行时现已包含三种来源的所有权、跨 Context 共享、带循环引用的资产克隆、绑定清理、嵌套路径替换、双向归一化、集合增量操作、共享命令忙碌状态和取消等待。Weaver 生成直接访问入口及成员描述，计算依赖通过虚方法覆盖支持继承。Cecil 固定为 0.11.5，与 Unity 的官方 Mono Cecil 包保持一致；仅构建工具引用，不进入游戏运行时。

Unity 通过 ILPostProcessor 处理项目程序集及 Portable PDB，注入 Preserve 入口供后续裁剪验证。Godot GUI 插件以源码安装到 addons，确保原生场景能保存 C# 脚本引用；Prepare-GodotMvvm.ps1 从模块源码安装，Demo 内生成副本不重复维护。代码绑定可用 UseMvvm；原生配置和帧观察分别通过 UseUnityMvvm／UseGodotMvvm 一次接入。

两端编辑器已具备数据源、绑定表、成员拖放、转换器筛选和属性入口。Unity 使用 UXML／USS；Godot 使用 EditorDock／Inspector 插件及原生 Undo 管理器。初始值预览使用独立 Runtime 克隆来源，禁用命令与反向写入，退出时恢复显示值。完整 GUI 操作验证仍在继续。

本阶段验证：核心／生成器 157 项，MVVM 织入后运行测试 13 项通过；Godot 原生 headless 14 项通过，覆盖资产不变、循环引用、原生类型数组独立、双向编辑、命令、清理、预览恢复和场景配置保存重开。Unity 真实 ILPP 编译与原生 Play Mode 1 项资产／绑定流程通过。Godot 插件启用后的 headless 编辑器导入通过。游戏 Demo 业务迁移、完整 GUI Undo／Prefab 验收、双语用户文档和 WebGL IL2CPP 尚未完成，模块仍处于实施中。

### 18.2 客户端迁移与实际流程修正

两端客户端接入可选模块：玩家资产由 Runtime 克隆并共享；设置和装备详情通过 CreateViewModel 建立节点独立实例；购买使用命令；背包通过 ObservableCollection 和原生条目资源绑定，条目采用 Manual / Item，不需要业务 View 类。项目代码继续负责布局、筛选、排序、确认和奖励演出。运行时增加同步 CreateViewModel、共享资产获取及 WhenIdleAsync，代码创建与 GUI 来源使用同一所有权逻辑。

动态列表暴露了适配器静态捕获控件树的问题。输入控制现在遍历当前原生子树，保留各控件的原始输入配置；合法增加、删除列表条目不会留下过期引用，新增条目也参与输入锁。逻辑节点根对象被无绑定销毁仍直接报错，不增加跳过或降级分支。

Unity 已重新保存编排器生成的真实 AnimationClip 轨道，替换旧的仅根透明度资源。Native Play Mode 的游戏客户端、适配、时间轴及生命周期共 26 项通过；客户端测试按约定 16:9 Game View 执行，浮点恢复使用 .001 精度检查。Godot 客户端 139 项、MVVM 原生 14 项通过。Weaver／Runtime 当前 19 项通过，包括不支持声明构建失败、抽象基类、重复织入和 Portable PDB。

隔离 WebGL IL2CPP 高裁剪示例已实际构建并在 Chromium WebGL 中通过 14 项。该流程发现 TaskCompletionSource 的 RunContinuationsAsynchronously 可使单线程 WebGL 清理停滞。核心和 Unity 生命周期共享任务采用直接发布；弹窗结果通过 Context 捕获的 SynchronizationContext 延后发布，普通 CLR 无上下文时使用其默认线程池。已接受结果有独立状态，批量关闭仍优先保留确认结果；不依靠线程池保持 UI 调用次序。核心 157 项回归通过。

双语用户文档新增 MVVM 入门、GUI 操作和 API 三组页面，链接／双语代码检查及站点构建通过。Unity 编辑器实际验证数据源编辑、Undo、Prefab Override、预览与恢复；Godot 编辑器验证来源编辑、原生 Undo、属性绑定入口及配置恢复。最终回归已完成：核心 157、MVVM 19、Godot 客户端 139、存档重启 4、原生 MVVM 14、Unity Play Mode 26、WebGL IL2CPP 高裁剪 14 项通过；74 个 Markdown、32 组双语页面检查及站点构建通过，65 页浏览器检查通过。检查了 MVVM 文档页面截图。最后修复 Godot 删除／重排撤销在切换选择后引用错误界面的问题，新增原生编辑器回归；Manual 缺少预览工厂时在面板显示来源名称。仅本地提交，未推送或发布。

当前使用边界：首版 GUI 的显式空值内容采用文本，带业务参数的命令通过代码绑定。Unity Getting Started 脚本已在 batchmode 编译，教程 UI 仍未原生运行验证。

### 18.3 命令发起关闭的生命周期重构

“购买确认后关闭确认窗口”是正常客户端流程，原实现让节点清理与发起关闭的命令互相等待。移除该使用限制，不要求项目把关闭移到另一个回调。

命令在执行前登记，使用异步执行上下文识别关闭的发起命令；取消 Token 由命令执行实例持有；节点 Lifetime 结束时按实际关闭调用链决定是否取消。外部关闭在等待子节点前即可取消父节点命令，命令自己发起关闭则保留发起命令。关闭先解除绑定，取消并等待其他命令，发起关闭的命令继续执行，独立 VM 延迟到全部所属命令结束后释放。外部关闭仍取消并等待全部命令及 VM 清理。隐藏行为不变。

Runtime 同样区分结束绑定和完整释放：命令内等待 Runtime 关闭先完成结束绑定，之后由 Runtime 持续观察该命令及延迟释放；外部 DisposeAsync 等待完整释放。WhenIdleAsync 持续收集命令运行期间新增的释放任务，即使命令失败也先等待清理，然后传播原始异常；释放失败记为原始异常的附加清理错误。私有 VM 与共享资产均不在所属任务结束前释放。

Godot 购买确认改为单个命令执行购买、CloseToAsync、奖励展示，移除按钮的第二个表现回调；Unity 购买窗口关闭按钮直接绑定 VM 命令并等待关闭自身，不改变其购买确认与奖励窗口组合。

验证：核心 157、MVVM 31（新增 12 项）、Godot 客户端 139、存档重启 4、Unity 原生 Play Mode 26、WebGL IL2CPP 高裁剪 17 项通过；新增 WebGL 检查实际等待自身窗口关闭、命令继续执行时 VM 存活及最终释放。中英文文档链接／代码检查、站点构建通过。首轮 Godot 139 项业务检查通过但进程退出时出现一次原生 finalizer 访问异常；随后重建并两次完整重跑均以 0 退出，未复现；保留原始日志，不在业务代码添加捕获或降级。

### 18.4 MVVM 初始化与释放由框架托管（已确认）

用户要求 MVVM 生命周期作为内部黑盒执行，不由业务创建、启用或释放。Core 增加 Context 扩展生命周期：Context 构造时初始化，Root 和资源结束后清理扩展，失败保留原始异常并继续必要清理。Core 不引用 MVVM、Cecil 或引擎程序集，未安装模块时不增加服务。

安装模块后自动为 Context 创建 Runtime、配置节点扩展并接入引擎任务观察。普通 .NET 的项目启动入口由 Weaver 注入注册调用；Godot 源码插件通过模块入口注册宿主适配，Unity 通过原生运行时／编辑器加载入口注册。重复注册幂等，重复织入不重复添加启动调用。

项目只使用节点绑定和 Context.RegisterViewModelFactory／GetSharedViewModel／WhenMvvmIdleAsync。跨 Context 共享使用 second.ShareMvvm(first)，需在创建节点前配置；引用计数由内部维护，最后一个 Context 自动释放 Runtime。ConfigureMvvm 保留给模块适配、隔离预览及低层测试，不作为入门初始化步骤。

两端 Demo 移除 Runtime 构造、启用和释放；项目订阅节点仅负责自身模型事件，不再承担 MVVM 生命周期。验证：核心 157、MVVM 34（包括无 VM 声明时的启动织入、自动初始化／释放、共享 Context 最后释放）、Godot 客户端 139、原生 MVVM 14、Unity Play Mode 26、WebGL IL2CPP 高裁剪 17 项通过。Godot 编辑器来源／Undo／配置恢复，Unity 编辑器 Undo／Prefab Override／预览恢复通过。源码构建无警告无错误，中英文文档链接／代码一致性及站点构建通过。

### 文档快捷启动

新增根目录 Open-Docs.cmd 与 scripts/Open-Docs.ps1：双击启动 VitePress 并打开网页；已运行服务直接复用，端口占用其他站点明确报错。服务在后台隐藏启动，首次缺少依赖时执行锁定依赖安装。默认 4173，Port 可指定，NoBrowser 用于不打开窗口的验证。

## 19. Unity Timeline 转场适配（已确认）

大厅多元素入场、奖励演出需要 Unity Timeline 原生多轨编排，现有 legacy AnimationClip 适配不能替代 Timeline。新增 UnityTransitions.Timeline(enterDirectorPath, exitDirectorPath, restore)，路径相对节点根，分别引用 Prefab 中两个 PlayableDirector；项目在资源中配置资产和绑定，不新建 Core 动画编排机制。

要求组件启用、关闭 Play On Awake、资产时长有限且 Director 无既有播放图。框架用 Manual 更新和未缩放时间采样，首帧等待前应用起点，报告单调进度。结束、取消或失败后停止图、恢复时间／更新模式／结束模式，并恢复项目声明的所有表现属性。playAnimation:false 沿用直接跳过；来源和阶段分支沿用 Select。Director 被无关代码提前停止、暂停或替换资产直接报错；已有生命周期绑定触发销毁时按取消处理，不写入销毁对象。轨道不执行业务或修改框架根节点显隐。

Runtime 仅新增 Director 引擎模块依赖；Demo 用 Package Manager 安装 Timeline 1.8.13，保存真实多轨 .playable、子 AnimationClip、Animator 与 Director 绑定。大厅和购买奖励迁移到 Timeline，其他来源及简化演出保留已有轻量分支。AnimationClip 是独立有效能力，专项测试迁至 LegacyLobby 测试资源；没有旧业务入口。

专项验证发现转场原本只监听容器 Lifetime，子节点绑定销毁不会取消动画。Core 转场现在同时链接容器及进入／退出节点 Lifetime，防止已释放节点继续播放；新增核心与原生销毁回归。验证：核心 158、MVVM 34、Godot 客户端 139、Unity 原生 Play Mode 31 项通过。Unity 新增 5 项 Timeline 专项，覆盖首帧采样、Time.timeScale=0、完成及退出、跳过、取消后重播、外部停止、原始通知异常、错误配置和绑定销毁；原有客户端来源／简化演出分支同步验证。两种语言的 API、资源结构和使用代码检查及站点构建通过。未推送、未发布。


## 20. Getting Started 从第一个窗口开始（2026-10-05）

用户要求教程手把手从创建简单窗口开始，覆盖引擎资源、脚本编写和宿主挂载。原有教程一开始要求读者搭建主菜单、设置 Tab 与确认退出；这会让初次接入同时处理多个容器能力和业务流程，现改为 HelloWindow。

Godot 与 Unity 教程顺序一致：准备项目和依赖、建立窗口资源、编写普通 C# UINode、创建 Main 场景宿主、挂载并指定资源，最后运行检查。窗口启动时显示；Open 使用默认单实例，Close 销毁，之后再次打开创建新实例。文档澄清 UINode 是框架逻辑节点，不作为 Godot Node 或 Unity MonoBehaviour 附加到窗口资源；引擎宿主脚本才挂在 Main／Canvas。

`docs/examples` 的两个引擎教程源码随页面同步。Godot 场景和 smoke 检查同步缩小到首次加载、实际按钮关闭／重开、单实例复用与 Context 清理。文档检查逐文件对比 `HelloWindow.cs`、`TutorialHost.cs` 与两种语言的代码块；站点首页和下一步链接由首个窗口场景引导到正式 Manual。

验证：文档链接、标题锚点、32 组双语代码与两个窗口源码同步检查通过；VitePress 站点构建通过，Chromium 检查 65 页、搜索、语言切换、复制、窄屏导航与六步目录。Godot 教程工程构建无警告，通过 8 项 headless 检查；Unity 示例脚本由 Unity 6000.3.24f1 batchmode 编译，未执行原生 UI。检查了桌面教程截图。未修改核心 API 或游戏 Demo 业务。


## 21. 原生业务节点与引擎 MVVM 适配重构（已实现）

用户要求业务 UI 直接继承对应引擎的节点基类，挂载在 Prefab／场景上，使用原生序列化字段、编辑器配置和引擎能力。已确认完整迁移 Unity、Godot 的适配、生成器、MVVM、Demo、测试和全部用户文档。Core 保持渲染无关，结构 Root 和逻辑容器仍由 Core 管理；业务节点由资源实例化取得，不通过普通 C# 构造函数建立原生对象。

两端适配类型和方法采用统一名称，通过 UIFramework.Unity／UIFramework.Godot 隔离；MVVM 适配通过 UIFramework.Mvvm.Unity／UIFramework.Mvvm.Godot 隔离。原生业务基类均命名 UINode、UINode<TArgs> 和 DialogNode<TResult>，不使用 Unity／Godot 前缀。共同能力的 API 名称与共同参数保持一致，真实引擎类型及专有能力允许差异。混合引用 Core 和适配时用命名空间别名解决同名歧义，不保留旧名称转发。

用户同时要求 MVVM 针对原生 UI 工作流优化：配置与 UI 资源一起编辑保存，支持原生控件选择和引用，完善项目可引用 VM 资产的发现及引用，并覆盖 Global VM 的编辑器发现／查找。Unity 编辑器继续使用 UXML／USS 与 UI Toolkit，Godot 使用原生编辑器插件；Undo、Prefab Override、场景保存重开和独立预览恢复属于验收范围。GUI 与代码绑定继续使用同一执行逻辑，Runtime 初始化和释放继续由框架托管。

原 21 阶段 Global VM 仅指编辑器发现项目内可直接引用的 VM 资产；Unity 当前重构已由第 30 节替代。该阶段定义为：Unity ScriptableObject 和 Godot Resource；不发现或选择普通 C# 对象类型，不增加运行时具名全局 VM 注册或查找。GUI 保存引擎原生资产引用，由资产取得 VM 类型和成员描述；移除 GUI 来源配置中的类型名称／程序集限定名称，不用字符串类型引用或重命名保底维持旧配置。资产搜索支持搜索、筛选、选择引用和定位来源；预览继续使用资产克隆，不修改原资产。普通 C# VM 仍支持运行时代码创建和绑定，不进入 GUI 资产发现工作流。

Godot GUI 和运行时代码创建能力继续保留；Unity GUI 不再选择三种创建方式，采用第 30 节的全局目录与本地注入。原 GUI 的来源类型由引用资产确定；Manual 的资产引用用于声明来源类型及显式提供预览样本，运行时实例必须由业务提供，不能因为缺失而自动使用资产实例。Shared Asset 仍在所属 Runtime 内缓存共享实例，跨 Context 共享沿用现有显式共享配置。用户要求绑定目标按原生控件引用保存，不以节点名称或 UI 层级路径作为绑定身份。Unity 使用 Component 引用；Godot 改为由场景维护的原生控件引用，并按引擎可序列化的能力调整配置载体，不继续在 ViewBindingResource 中保存手工 NodePath 再 GetNode 查找目标。引擎内部序列化的引用表示与框架手工路径关联区分：框架不要求用户维护路径，运行时直接使用已解析的目标对象。目标字段、属性和命令的成员名称仍用于选择绑定成员，不与控件层级身份混用。

控件重命名、移动到其他父节点后，同一控件的绑定应保持有效；保存重开、Undo／Redo、Prefab Override、PackedScene／Prefab 克隆及多个实例必须验证引用正确指向各自实例，不交叉引用其他窗口或模板控件。删除控件或真正丢失引用时直接报错，不按旧路径、同名节点或其他控件保底重绑，不以每帧扫描维持引用。

本节是当前设计契约，实施与验证结果见 21.1–21.5。第 20 节为旧教程阶段记录；当前 Getting Started、Manual、Demo 与 API 使用原生业务脚本工作流。


### 21.1 Core 身份与创建阶段（阶段记录）

Core 增加 IUINode，业务对象通过一个 Core UINode 执行状态与清理。容器泛型、查询、历史和输入所有者返回业务身份；生命周期和转场 Node／Source／Target 同样保留实际业务对象。资源原生业务节点通过 IUIResourceNodeProvider 取得；注册配置回调在 Context 与参数建立后、MVVM／业务初始化前执行。纯 Core／自定义后端使用显式工厂，生成器对 Core 类生成工厂，对原生资源节点使用资源注册；不反射构造引擎脚本。

本阶段 Core 构建无警告，核心／生成器 163 项、MVVM 34 项通过。新增 5 项验证资源业务身份、单实例重开、配置与参数顺序、来源历史身份、原生结果弹窗以及配置失败的原始异常与资源释放、工厂返回空时不转用资源脚本。两端原生类、Demo、编辑器和用户示例仍在迁移，尚未完成本次整体重构或引擎验收。

用户进一步要求编辑器可配置转换器，参考 [UE MVVM 工作流](https://dev.epicgames.com/documentation/en-us/unreal-engine/umg-viewmodel-for-unreal-engine)。绑定行支持类型筛选的转换函数选择；选择后显示函数参数配置，遵循 UE 一个输入、一个返回值的转换函数范围，输入可来自明确常量或 VM 成员，成员变更触发重新求值；本轮不添加多输入转换机制。双向绑定的正向和反向分别配置，不能猜测逆转换。沿用显式 Converter 标记与 Cecil 直接调用，不增加运行时反射业务调用或新 Analyzer；GUI 配置、预览和运行时使用同一转换执行逻辑，错误原样传播。

### 21.2 原生基类与资源阶段（阶段记录）

两端业务基类已改为原生组件：Unity UINode 继承 MonoBehaviour，Godot UINode 继承 Control。资源注册从 Prefab／PackedScene 根节点取得实际业务脚本，配置回调在 Context 与参数建立后执行，框架初始化前完成 Awake／_Ready；原生回调仅负责本地控件准备，Context 业务使用框架初始化。GetRoot 与 GetControl 使用统一名称；业务脚本可直接序列化原生控件引用。Godot 的原生 Name／IsVisible 与逻辑命名冲突，因此两端业务基类统一暴露 UIName／IsUIVisible，IUINode 接口仍使用 Name／IsVisible。

原生节点自动绑定组件生命周期，显式绑定其他对象不覆盖自动绑定。Unity 原生测试发现销毁时释放显示对象早于转场恢复；现由节点取消 Lifetime，并等待参与转场完成恢复后再释放。新增回归验证原始取消异常及无额外清理错误。

验证：核心／生成器 164 项、Unity 原生节点 7 项通过；Godot 首个窗口教程 8 项 headless 通过；Unity 教程及两端适配构建无警告无错误。首个窗口中英文教程已同步原生脚本挂载和控件引用。游戏 Demo、完整 MVVM 编辑器和其余用户文档仍在迁移，本阶段不代表整体重构完成。

### 21.3 转换器编辑与验证（2026-10-06）

两端绑定详情现可选择转换函数，显示输入／输出类型，并将单个输入配置为来源成员或常量。VM 成员沿用 Source 路径与通知，常量使用函数输入类型解析，支持文本、布尔、数字、枚举，数字采用不变区域的小数点格式。常量只支持 OneWay／OneTime，不能伪装为可写 VM 成员；TwoWay 正向来自 VM，反向来自控件，分别选择函数。配置使用 Converters.Configure，预览与运行时共用；静态函数仍由 Weaver 生成直接调用。失效函数、错误常量和转换错误直接抛出。

Godot 编辑配置后保留选中绑定并重建详情；选择转换函数后可继续配置参数。Godot VM 资产需要 Tool 才能在编辑器执行其 C# 属性和预览；无 Tool 的原生资源在编辑器不具备该行为，不进行类型名称保底。

验证：核心 164、MVVM 41 项通过。Unity 原生编辑器验证 GUI 参数、独立预览及恢复、原始资产不变、Prefab 保存重开、控件引用映射、Undo；Godot 原生 headless 验证场景保存重开与实例预览，原生编辑器验证参数 GUI、选中行保留、Undo、预览恢复。脚本 scripts/Test-MvvmConverters.ps1 在隔离项目重复验证上述流程，中英文操作／API 文档及站点构建通过。Unity UXML／USS 使用原生编辑器导入，无 IMGUI。此阶段完成转换器配置，两端完整资产发现、控件引用和游戏 Demo 的原生节点迁移仍在实施。

### 21.4 游戏 Demo 原生脚本阶段（2026-10-06）

两端业务窗口均继承引擎原生 UINode／参数节点／结果弹窗，独立脚本与资源根节点对应；Core 工厂仅用于结构节点和隔离适配测试。Godot 确认窗口使用独立 Confirm.tscn，不再与业务 Modal 的根脚本混用同一场景。确认文本通过 Register 配置回调赋值，移除引擎脚本的业务构造参数。Unity 窗口 Prefab 已通过原生 Editor API 挂载对应组件，资源编排器同步生成原生业务脚本；控件查询使用统一 GetControl，列表所属节点采用 IUINode。

两端 Demo 构建无错误。Godot 完整客户端 139 项、Unity 原生 Play Mode 31 项通过，Godot 生命周期独立检查 13 项通过。Godot 退出仍出现少量 RefCounted 引用计数为零的 ObjectDB 警告（完整客户端 3 个，独立生命周期 2 个），尚未定位，不标为已解决，不在业务添加 GC 或降级掩盖。完整资产发现、原生绑定目标引用及其余用户文档仍在迁移。

### 21.5 原生 MVVM 配置与用户文档（2026-10-06）

两端来源配置仅保存 VM 资产引用，删除 TypeName，不再列出普通 C# 类型。编辑器按 ViewModel 的显式标记发现 ScriptableObject／Resource 资产，提供搜索与定位。Manual 资产声明类型并提供明确预览样本；预览独立克隆，运行时仍必须由业务提供实例。列表 Item 来源由条目节点直接提供，项目普通 C# 条目不保存类型引用；无需另外声明 Item 来源资产。

Unity 的 MvvmBindings 继续保存组件引用与数据列表，所有 GUI 配置通过 SerializedObject／SerializedProperty 写入，支持 Undo／Prefab 修改；控件 Inspector 的绑定入口按需通过 Undo 添加可选组件。Godot MvvmBindings 同时保存原生 Control 引用数组和绑定 Resource，新增、删除、移动及 Undo 同步修改两组数据；ViewBindingResource 移除 Target NodePath，运行时不按控件路径解析。真正丢失引用直接报错。

两端预览和运行时共用 GetOptions：先校验原始转换配置，再将预览设为只更新显示，避免掩盖无效反向函数或常量写回。先完成配置校验再连接控件事件，失败不残留订阅。可复用 NodeLifecycle 的初始化和清理统一接受 IUINode，传入实际业务身份；新增回归。节点扩展仍面向 Core 状态，负责框架模块的低层接入。

全站更新统一适配名称、原生业务基类与资源挂载、注册回调依赖注入、生命周期身份及资产配置说明。Core／生成器 165、MVVM 41、Godot 客户端 139、原生 MVVM 14、Unity Play Mode 31 项通过。两端原生编辑器验证转换器参数、Undo、预览恢复、Manual 样本、资源保存重开及控件重命名／换父节点／克隆后的引用映射；验证脚本可重复运行。WebGL IL2CPP 高裁剪示例实际构建并在 Chromium 通过 17 项，结果弹窗改为原生组件。用户文档链接／双语代码、站点构建、65 页浏览器检查及 15 项无引擎示例通过；参数与生命周期原生示例编译无警告无错误。Godot 退出 RefCounted 警告继续如 21.4 保留记录，尚未定位。

最终补验：Godot 原生首个窗口教程 8 项、Unity 原生节点 7 项通过；Unity 的转换配置 SerializedProperty 修改产生 Prefab Override，Undo 恢复原始配置。转换器、原生绑定引用和资产来源阶段已完成并验证；Godot SDK 退出时 RefCounted 警告作为明确未解决项保留。

## 22. MVVM 属性绑定暂停／恢复（2026-10-06，已确认）

新增同步扩展 PauseBinding(IUINode node, string propertyName)／ResumeBinding(IUINode node, string propertyName)。字符串指定 VM 属性；简写按来源路径末端成员匹配，对应多个不同来源路径时报歧义错误，调用方须给出 Source.Member 完整路径。同一来源路径的所有属性绑定一起处理，只限当前节点，不递归子节点，不处理命令、集合和显式常量输入。代码与 GUI 属性绑定共用同一执行逻辑。

暂停保留订阅和嵌套路径跟踪，停止 VM 到控件更新及控件到 VM 回写；恢复读取 VM 最新值并执行原有转换／空值规则，OneTime 也同步一次，随后保持其原有模式。先验证整组状态，再改变暂停标记；重复暂停、未暂停恢复、缺失绑定、已释放节点直接报错，不引入嵌套计数。恢复时尝试同步整组，失败记录并传播原始首个异常，其他失败按既有清理契约关联，不隐藏失败或保底显示。

保持 IBindingTarget 与 Bind 返回类型不变。不提供句柄、接管作用域、动画执行包装或自动恢复。用户负责动画播放、停止、清理与恢复时机；节点关闭仍正常解除绑定，销毁后不再恢复。两端 Demo 在角色培养提交存档后，以 Hero.Power 暂停／恢复配对播放数值增长，其他 UI 属性继续更新，恢复后使用最新 VM 状态。

验证：核心／生成器 165、MVVM 52、Godot 客户端 140、Godot 原生 MVVM 16、Unity Play Mode 31 项通过；WebGL IL2CPP 高裁剪构建与浏览器运行 19 项通过。用户文档链接／双语检查和站点构建通过。新增测试覆盖四种绑定模式、同源多控件、来源歧义、嵌套对象替换、配对错误、转换异常身份、双向抑制及节点清理。Godot 退出的既有 ObjectDB 警告仍按 21.4 保留，未标为解决。

用户确认：小改动不需要构建验证，以源码检查为主；不因小的可逆调整追加构建或重复全量验证。2026-10-07 再次明确：不执行完整验证测试，规则写入 AGENTS.md；未解决的运行问题只做必要的最小针对性检查。生成并同步引擎使用的 DLL 属于交付步骤，不能以跳过验证为由保留旧程序集。

Unity 程序集同步（2026-10-07）：用户反馈 Modal 并行动画修改未生效。实际运行项目引用本仓库本地 UPM 包，但加载的 Core DLL 仍为 2026-10-06 版本；核心测试只更新 Debug 输出，Unity 包内的 DLL 未更新。Prepare-Mvvm 现在一次编译后同时复制 Core、MVVM 和 Weaver DLL；Run-UnityDemo 复用该准备流程，不重复编译 Core。修复交付时同步运行程序集，不以源码测试结果代替实际加载版本。

本次只进行程序集交付与版本核对：Core／MVVM 与 Unity 包 DLL 的文件散列一致；刷新后，当前 Unity 加载的 Core MVID 与新 Release 输出一致，转场入口包含并行播放参数，节点扩展接口为 AfterInitializeAsync。未运行 Play Mode、完整测试或动画视觉验收，未修改使用方项目的业务源码与场景。

## 23. 迁移项目 MVVM 缺口补齐（2026-10-06，已确认）

针对 Fruit-platter 迁移报告 F-01～F-06，统一补齐两端属性目标扩展、运行／预览共用解析、命令参数及异步集合工厂。I-01～I-06 属于现有扩展点或项目集成规则，不另建 Addressables、业务导航或状态作用域机制。生命周期通知继续只读，不放宽结构重入保护。外部项目不修改。

属性目标由程序集 BindingTargetProvider 标记的 IBindingTargetProvider 显式提供强类型 BindingTargetDefinition。运行时与编辑器共用 BindingTargetRegistry，发现成员不创建绑定、不订阅事件；基类定义适用于派生组件，同成员优先最具体定义，重复／歧义直接报错。双向及 View→VM 要求目标声明通知。Unity 增加 enabled／color／alpha，Godot 增加 Modulate／SelfModulate；其他自定义属性显式注册，不反射调用业务成员。Unity ILPostProcessor 保留提供者和方法，沿用 IL2CPP 强类型调用。

阶段一：MVVM 54 项通过，包含无订阅发现、派生定义优先、通知解绑及不可观察目标拒绝双向。两端 GUI 与预览已迁移注册表；原生编辑器和 IL2CPP 验证在本轮后续集成阶段集中执行，尚未计为通过。

命令配置增加 None／Constant／SourcePath。固定参数 API 保留，CommandBindingOptions 提供动态来源，点击时读取最新值并严格校验类型；序列化常量按命令类型解析，不保存类型字符串。MvvmMember.ParameterType 对外明确命令业务参数；Weaver 既有 Type 已生成业务参数并排除 CancellationToken，无需重复生成。两端 GUI 显示参数类型并检查常量／来源类型，预览不执行命令。阶段二 MVVM 56 项通过。

集合由 CollectionTargetBase 同步维护逻辑槽位、异步创建和释放实例。ICollectionItemFactory 使用 CollectionItemContext.CreateAsync／LoadAsync 创建一个条目，默认从资源取得原生业务脚本，没有脚本时使用 Core 结构节点。Item 默认注入；GUI 父来源→条目来源映射借用父实例，创建时取值，不隐式克隆或跟随父来源替换。自建条目 VM 可在配置回调中通过 CreateViewModel 创建并由节点拥有。映射名称唯一，Item 为保留来源。运行时工厂可选择不同资源；GUI 使用原生工厂组件／节点引用，不保存类型名。预览运行相同工厂并提供 IsPreview，默认只初始化展示用结构节点及属性／集合绑定，不执行业务脚本初始化或命令。

MVVM 内部增加条目初始化回调，在来源创建后、业务初始化前执行。Core AttachAsync 新增可选 initiallyVisible，集合传入 true，让原生脚本与结构条目一致地在准备完成后显示，不直接操作 Core 内部状态。绑定目标如支持 IAsyncDisposable，关闭先解除集合订阅，再等待目标及条目任务；其他同步目标保持 IDisposable。移动保留实例，删除／替换／Reset／关闭立即取消对应槽位，晚到资源只释放。预期的条目取消在清理成功后结束其内部任务；业务失败或取消伴随清理失败仍记录并传播原始异常，不自动重试。Core 的取消日志继续保留。

两端商店迁移到动态商品列表、共享 Player／Shop 来源、点击时读取 Item.Id 的参数命令及异步资源准备。金币影响商品颜色，原有确认、扣款和奖励流程保留。原生列表不再 new 引擎 UINode。项目布局监听 CollectionTarget.Changed，确保异步条目就绪后布局更新。I-01 Addressables 继续实现 IUIResourceLoader；I-02 正常取消日志由注入 IUILog 决定；I-03／I-04 业务状态和导航由项目管理；I-05 生命周期通知保持只读；I-06 资产打包依赖由资源加载器／项目作用域保活，不因模板跨页面引用自动扩展寿命。

命令常量可显式选择 null，仅允许可接收空值的类型；None 只适用于无业务参数命令。新增循环资产容器测试验证 Godot 独立复制的容器在释放时清空并释放，不触及原资产；只记录 Remap 创建的副本，失败时不清空尚未复制的原资产容器。Runtime 与集合目标完成清理后移除任务引用，避免完成任务继续持有条目图。

验证结果：核心／生成器 165、MVVM 61、Godot 客户端 140、原生 MVVM／条目 22 项通过。Unity 31 项既有回归通过，新增异步原生条目专项及资产绑定专项 2 项通过。两端原生编辑器验证自定义强类型成员、预览恢复与原资产不变、命令常量 GUI、来源映射、Prefab／场景保存重开、Undo 与 Prefab Override。WebGL IL2CPP 高裁剪构建并在 Chromium 通过 26 项，涵盖自定义提供者、结构体目标、enabled、动态参数与异步列表移动／Reset。双语文档及链接检查通过。

未解决项如实保留：Godot 完整客户端退出本轮报告 6 个 ObjectDB 实例；新增类型化 Resource 集合的隔离检查退出还报告 ConverterVm.cs 的 1 个 CSharpScript 资源引用。容器清理与原资产完整性断言通过，但退出警告尚未完全定位，不能宣称无残留资源。没有通过 GC、业务异常捕获或降级掩盖。

集合取消回调、布局回调或分离实例失败时仍安排所有条目的清理，并传播原始首个错误；增加取消回调抛错仍释放迟到显示实例的回归。

### 23.1 初始异步集合与窗口显示同步（2026-10-07）

用户指出集合条目创建只被观察、未纳入窗口初始化等待。节点模块的初始化后阶段改为 `Task AfterInitializeAsync(UINode node, CancellationToken token)`，Core 在业务初始化与处理器结束后等待模块准备，再将节点设为 Ready；不保留旧同步回调。MVVM 先应用 GUI 绑定，再等待本节点在 Initializing 期间由 CollectionTargetBase 登记的创建／移除收尾任务。代码绑定、两端内置目标、自定义布局包装和嵌套条目采用同一流程，完成初始条目及布局后才开始窗口入场。

等待不使用 Runtime.WhenIdleAsync，不包含命令、一般 ObserveMvvmTask 或其他节点的任务；显示后的集合变化仍为增量异步更新。取消初始化会退出等待并通过节点清理取消所有条目，底层不响应取消的加载仍等待收尾、释放资源，不显示迟到条目。失败阻止窗口显示，清理后传播原异常。CollectionItemContext 在取得显示对象所有权后、绑定到节点前检查取消，避免已取消条目尝试附加到销毁中的父节点。

验证：MVVM 67 项通过（新增 6 项），覆盖代码／GUI 初始等待、嵌套集合、与无关后台任务隔离、显示后的增量更新、调用取消／容器销毁及初始失败的原始异常与资源释放。核心／生成器 173 项回归通过，双语代码、站内链接与标题锚点检查通过；未启动引擎，两端原生窗口与编辑器预览未新增运行验收。

## 24. 关闭后实例缓存（2026-10-07，已确认）

注册 `NodeCacheMode.KeepAlive` 后，Panel Close、Stack Pop／PopTo、Modal Close／CloseTo 使用统一归还流程；默认 None 仍销毁。替代此前“只用 Hide 表达实例保留、不加 Close 保留策略”的候选建议。LRU、容量预算、通用对象池列为后续计划，不在本轮实现。

- 缓存以原容器与注册类型区分，每种类型只保留最后关闭的一个实例，替换时等待旧实例销毁。不得跨容器／Context 复用；缓存退出窗口记录、历史、输入和焦点，但仍保留逻辑父子关系、原生挂载和资源持有。IsCached 对整个子树有效，普通结构操作拒绝操作缓存子树；内部 MVVM 集合维护仍允许初始化新条目。
- 初始化与注册配置只执行一次；复用先重新设置参数，再执行入场。LifecycleContext.IsCacheReuse 标识缓存重开，IsFirstEnter 仍表示该实例首次进入。保留整个子树、Tab、内部历史、VM、绑定；缓存不取消 Lifetime 或自动取消命令，业务负责在退出／失活时停止只在显示期运行的任务和动画。
- Dialog 按每次打开创建独立结果会话。关闭取消未回答会话；已接受结果保持有效。等待与延后发布捕获原会话，原关闭记录不会作用于新会话。Modal 遮罩独立释放，缓存窗口不得持有旧遮罩；出入场仍与遮罩并行。
- ContainerNode.ClearCacheAsync<T>／ClearCacheAsync 清理自身缓存；UIContext.ClearCacheAsync 清理上下文缓存，不关闭打开或隐藏窗口。最终 Dispose、父级／Context／引擎绑定对象销毁直接释放缓存。复用准备失败或取消释放实例，原始异常继续传播；关闭准备失败恢复窗口，提交／清理失败不缓存异常实例。
- 两端 Demo 缓存背包、装备详情和确认框。内容更新迁至每次入场，订阅不重复初始化。LRU、容量预算和通用池不添加预留接口。

验证：核心／生成器 193 项、MVVM 69 项通过，包含延后结果在旧 Dialog 关闭并重开后才发布的确定性测试；Godot 4.7.2 .NET headless 客户端 143 项通过，构建无警告、退出无 ObjectDB／资源持有警告。Unity 6000.3.24f1 后台 Play Mode 的 TabsComparisonSettingsAndTeamRestrictions 通过，涵盖缓存身份、Tab 保留、新详情参数及设置确认；首次无图形执行因测试初始化 Game View 失败，改用后台图形模式后通过。两端消费的 Core／MVVM 程序集已更新。中英文 32 对页面的代码／本地链接／锚点检查通过。本轮未做全量 Unity 测试、IL2CPP 导出或三种分辨率视觉审核，未修改外部 Fruit-platter 业务或场景。

## 25. Modal 显示与遮罩首帧（2026-10-07）

Core 在进入准备完成后同步显示窗口，并在同一调用帧启动窗口与新遮罩动画。Unity Simple／Fade／ScaleFade／Slide 原先在 PlayAsync 首行等待 NextFrameAsync，导致窗口已激活、遮罩已启动，但窗口仍保留默认姿态一帧。现在先同步更新 Canvas 布局，在当前帧写入动画起点，再等待后续帧推进。Godot Simple 和 AnimationPlayer、Unity legacy Animation 和 Timeline 原本就先应用起点再等待，不需加入额外延迟。

同帧指 GameObject／Control 显示与动画起点；框架的交互激活、输入所有者与焦点仍在双方动画完成后提交。自定义动画若自行在起点前等待，不由框架隐式修改。

验证：Unity 6000.3.24f1 原生 Play Mode 的 ModalWindowAndBackdropStartInTheSameFrame 单项通过，同时记录窗口 Playing 帧、原生 activeInHierarchy、窗口／遮罩首帧 alpha 及输入未开放状态；打开与关闭均同帧开始。只做此运行问题所需的单项验证和相关源码／差异检查，不重复完整构建或跨引擎回归。

## 26. Context 默认遮罩（2026-10-07，已确认）

新增同步 UIContext.DefaultBackdropFactory，默认 null；项目统一配置引擎 Backdrops.Dim 的颜色和动画时长，Modal 容器默认继承。ConfigureBackdrop 提供容器覆盖；DisableBackdrop 明确禁用；UseDefaultBackdrop(mode = Shared) 清除覆盖／禁用，恢复继承。两端使用相同核心 API，不增加独立遮罩动画策略系统。

优先级为明确禁用、容器覆盖、Context 默认；无配置则无显示遮罩，既有模态输入策略不变。容器配置只允许在无打开窗口且无操作时修改。每次打开在异步创建窗口前捕获 Factory；全局改变只影响后续新建遮罩，已有 Shared 实例继续使用到最后一层关闭。流程起初没有遮罩、中途首次创建 Shared 时，让仍存在的下层窗口共同持有，防止返回后遮罩无人拥有或最后关闭不释放。创建失败传播原异常，不换用其他 Factory。

两端客户端将默认遮罩统一配置为 .2 秒，移除各容器重复配置；原来的并行动画和首帧契约保持不变。

验证：遮罩专项 41 项通过，包含继承／覆盖／禁用／恢复、Context 隔离、Shared 已存在时改变／清空默认、中途首次创建、PerModal 新配置及原始失败、操作中禁止配置。此次不追加全量回归或两端原生测试；Unity 包内 Core／MVVM DLL 已按交付流程更新，Core 与 Release 输出哈希一致；Godot Demo 编译输出已刷新，0 警告、0 错误。修改页面的中英文代码块一致，相关源码与差异检查通过。

## 27. 容器操作协调重构（2026-10-08，已确认）

游戏里的并行业务入口可能同时打开奖励、等待提示，或关闭多张浮窗。容器不再用 busy 锁把正常并发视为错误：结构操作按容器 FIFO 提交，每个任务等待自身的资源、生命周期、转场和清理；不同容器独立。空队列先结束再发布请求结果，紧接着的操作同帧开始并立即使用既有输入锁。不新增用户队列 API、全局输入机制、后台线程或帧延迟。查询／配置仍同步；容器配置在执行及排队期间禁止修改。

- 重复 Hide／Show、Switch 当前 Tab、PopTo／CloseTo 当前目标、未选中 Tab.Hide 和空栈 Pop 正常完成，无额外通知／动画。连续 Pop 保持逐步返回，不合并；Push 和多实例 Open 保持独立。已有 Panel Single 带新参数仍报错，防止丢弃业务数据。
- 同一次打开的重复 Close 共享一次执行。首个请求决定动画和执行取消；后续 Token 只取消自己的等待。正常关闭后 Close 成功；准备失败可显式重试，提交／清理失败不转成成功。Modal 仍只允许关闭顶层。
- 内部记录每次打开的身份；带实例目标的请求提交时捕获该身份。旧关闭请求不会关闭缓存重开的实例，旧 Hide／Show／PopTo／CloseTo 目标失效直接报错。不公开结果会话或导航对象；重开后才触发的旧业务回调由项目负责。
- 排队取消立即结束等待，不执行请求；进行中取消等待恢复／清理。请求关联容器及祖先 Lifetime；父级／Context 销毁取消并等待队列，再释放子树。ShowDialog 等待答案不占队列；MVVM 命令中关闭自身保持支持。
- 普通操作失败只结束该请求，后续请求按执行时状态校验。恢复显示、输入锁或绘制顺序失败时保留原异常并停止队列后续操作，直到显式销毁；不自动恢复、重置或保底跳过。
- 同步生命周期通知保持只读；活动生命周期不能请求自身操作容器或祖先，允许初始化并等待子容器。每个请求保存提交方 ExecutionContext，在原 UI 线程执行时恢复，保留 MVVM 命令自关闭身份等 AsyncLocal 状态；不从队列首项继承后续请求的上下文。操作上下文结束后失活，避免延后回调被误判。内部请求依赖检查发现跨容器循环等待时直接报错；不声称解决项目任意锁或任务的死锁。不用未观察任务绕开生命周期等待约定。

两端 Demo 的装备对比退出使用 Task.WhenAll 一次提交关闭请求，由 Panel 排序。测试覆盖 FIFO、失败后续、独立容器、取消、重复关闭、缓存旧目标、幂等边界、子容器初始化、失活上下文、循环依赖、销毁等待与恢复失败。验证：核心／生成器 213、MVVM 70、Godot 客户端 headless 145 项通过；Unity QueuedNativeWindows 原生 Play Mode 单项通过，确认多实例排队打开、重复关闭及全部清理。文档 74 页／32 双语对应检查通过。两端实际引用的 Core DLL 已同步，Unity 包与 Release、Godot 输出与 Debug 文件散列分别一致。本轮未执行完整 Unity 原生套件、WebGL／IL2CPP 导出或整站浏览器验收。

## 28. 发布名称（2026-10-08，已确认）

正式名称为 Common Game UI Framework，简称及 GitHub 仓库名为 CommonGameUI。根 README、双语文档首页和站点标题使用发布名称。本次发布基于当前已提交的实现；现有源码程序集及 API 命名空间仍为 UIFramework，安装示例以实际源码为准。

发布许可（2026-10-08，已确认）：框架源码采用 MIT License，根目录 LICENSE 使用 GitHub 标准 MIT 文本，版权署名为发布账号 Zzz-x1。第三方 Demo 素材仍遵循各自原有许可和署名记录，不改为项目代码许可。

## 29. 可选容器挂载（2026-10-09，已确认）

两端 CreateContainerAsync<T>(name, mount = null, token = default) 允许省略挂载。省略时查找最近逻辑祖先的显示对象，创建铺满父区域的专属空挂载；结构 Root 使用 UIHost.RootMount。Unity UI.Create 使用 Canvas；Godot UI.Create 使用 Control owner，非 Control owner 可显式提供 rootMount。没有可用挂载直接报错，不猜测场景根。自动挂载由容器释放；显式挂载保持借用，不随容器销毁。不新增默认项目节点、Canvas 或绘制层级策略。入门示例使用自动挂载，客户端已有分区与层序继续显式挂载。

验证：Godot 入门 headless 13 项检查、Unity AutomaticContainerMounts 单项 Play Mode 通过，覆盖 Root 自动挂载、跨结构节点查找、铺满布局及自有／借用挂载释放。Godot Demo 重新生成成功，0 警告、0 错误；Unity 适配源代码由原生测试重新编译。中英文教程代码和链接检查通过。未运行全量回归或完整客户端验收。

## 30. Unity 全局 VM Provider 与目录（2026-10-09，已确认）

Unity MvvmBindings 移除窗口级 ViewModels 列表及 Creation 配置。新增 GlobalViewModelCatalog SO 维护 SO VM 引用，保存到 Resources/UIFramework/Mvvm 目录；Unity 默认 GlobalViewModelProvider 读取所有目录，由适配器在 Context 初始化时自动注册。登记定义不创建实例；首次使用时克隆原资产，同一 Context 默认共享，不同 Context 独立，Context 清理释放克隆。目录和原资产只提供引用及初始值，不参与业务写入。显式 ShareMvvm 仍代表项目主动共用 Runtime，不改变默认隔离。

Core MVVM 提供 IGlobalViewModelProvider（ViewModels、Assets）与 GlobalViewModelProviderAttribute；框架初始化自动注册程序集标记的项目 Provider 和适配器默认 Provider。Provider 不持有运行时实例，重复定义或不合法 VM 直接报错。自定义 Provider 无参构造入口需按引擎裁剪规则保留。用户无需手动初始化或释放 Runtime。

Unity 每行 ViewBindingData 保存 ViewModel 原生资产引用和 IsGlobal。来源路径仍为节点内别名与成员路径，同一别名的资产及模式必须一致。全局引用必须已在 Provider 注册，不按名称查找其他资产保底。GUI 左侧只读浏览全局目录，拖动成员保存资产引用；本地来源可选择类型／预览资产，不在运行时克隆。集合 Item 始终由集合注入，禁止标为全局；父来源映射和业务 SetViewModel 保持借用。预览使用独立克隆，缺少预览来源明确显示原因，不执行命令或写回原资产。配置仍通过 SerializedObject 保存、Undo 和 Prefab Override；不引入 IMGUI。

本轮仅迁移 Unity 来源 GUI；Godot 保留既有 Resource 来源配置。两端共享的 Runtime 接口和程序集同步更新。无旧 Unity 来源类型或兼容读取层。

验证：核心 GlobalProviders 专项 1 项、Unity 原生 MVVM 2 项及默认目录自动注册 1 项通过，包含同 Context 共享、不同 Context 隔离、模板不变、窗口关闭保留、Context 释放和集合注入。Unity 编辑器实际执行 Undo、Prefab Override、局部／全局资产预览及恢复检查通过。中英文教程／代码／链接检查通过；Unity 包内 MVVM DLL 与 Release 输出 SHA256 一致，Godot Demo 已重新生成，0 警告、0 错误。未运行全量回归、Godot 原生 GUI 或 WebGL／IL2CPP 导出。

## 31. 错误与异常日志分离（2026-10-09，已确认）

IUILog 提供同步 Error(string message) 与 Exception(Exception exception, string context)。Error 记录普通错误文本；框架捕获的原始异常统一调用 Exception，保持上下文、去重、附加清理失败及原始异常传播。Unity Error 使用 Debug.LogError，Exception 先记录操作上下文，再使用 Debug.LogException(exception)，不把异常转成普通错误字符串或包装新异常。Godot 与控制台同步实现两种入口，项目日志实现、测试与中英文接口说明同步迁移，不保留旧 Error(Exception, context) 重载。

交付检查：Core／MVVM 生成成功，Unity 实际引用 DLL 已同步且 Core SHA256 与 Release 一致；Godot Demo 重新生成成功，0 警告、0 错误；Unity 批处理编译无 C# 错误。只进行接口迁移、调用点与差异检查及必要程序集交付，不执行全量测试。
