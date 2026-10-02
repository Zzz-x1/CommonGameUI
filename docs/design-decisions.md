# UI 管理框架设计文档

更新日期：2026-10-02（Asia/Shanghai）。当前已实现 V4 转场与模态遮罩；转场实际契约见 9.5，遮罩与验收见 9.6。V3 历史验收见 8.10。

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
10. [V5 迭代计划候选](#10-v5-迭代计划候选)

## 阅读约定

- **已确认**：用户明确提出或确认的要求，后续工作必须遵守。
- **建议**：设计候选，不代表已获用户确认。
- **待定**：尚不能作为实现依据的具体契约或语义。

类型名、签名、继承图和示例除明确标注的约束外均为草案，不代表已实现。用户后续指令优先；新决定更新本文，移除被替代的建议，按需同步 AGENTS.md。

## 1. 约束与范围

### 1. 目标（已确认）

基于 C# 的渲染无关 UI 管理框架，可集成到使用 C# 的游戏引擎。开箱即用、简单易上手、API 简洁，支持快速原型、复杂项目与高扩展性。

新引擎需要相应适配器，不能承诺未经适配即可运行。

长期 Demo（已确认）：Godot 项目作为本工程持续维护的 Demo，也是实际验证/测试载体。Demo 随框架已实现功能更新迭代，不是一次性验证项目，维护范围不限于 V1。初版已创建于 `Demo/Godot`，采用 Godot 4.7.2 .NET。V1 交付安排维护在 V1 开发文档。

Demo 美术方向（已确认，2026-10-02）：用户选择候选④ Kenney UI Pack - Adventure，替换初版通用 UI Pack 的视觉方案。采用纸张、木框与卡通冒险风格；素材及字体许可记录在 Demo 的 Assets/SOURCES.md，Godot 样式集中于 Themes/Adventure.tres，不进入渲染无关核心契约。

版本管理（已确认，2026-10-02）：工程初始化本地 Git 仓库，后续修改完成并执行适用验证后自动提交相关代码、Demo 与文档。提交前检查变更范围和暂存内容；构建产物、工具、缓存及截图输出不入库。阶段性提交必须如实记录未完成/未验证状态。当前仅管理本地仓库，远程推送或改写历史需另有用户授权。

设计演进规范（已确认，2026-10-02）：发现功能设计存在缺陷或不合理时，优先提出解决方案并重构，不以围绕原设计局部修补为默认路线。所有改动不考虑历史版本兼容，可以直接调整或删除旧类型、API 和行为；不引入兼容层、旧入口转发、双轨钩子或旧行为开关。重构须同步迁移工程内调用方、生成器、适配器、Demo、测试和文档，保持新设计完整可验证。此规范不取消原始异常传播、资源清理及用户已确认的其他要求。

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
- 框架默认包含一个 Root 节点，可通过 API 获取。Root 是结构性根节点，不属于禁止提供的项目默认节点；获取方法或属性名称尚未定稿。

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
| ModalContainerNode | OpenAsync<T>、CloseAsync(node)、ShowDialogAsync<T,TResult>；仅关闭当前模态，持有输入并恢复焦点；V4 增加 ConfigureBackdrop、RequestDismissAsync、CurrentBackdrop |
| DialogNode<TResult> | 可选结果契约，Complete(result)；普通模态窗口仍可为一般 UINode，不要求继承此类 |
| GodotUIElement | owned 动态场景与 borrowed 内嵌 Control 句柄；显隐、输入、排序及按所有权销毁 |
| Source Generator | UIResource 特性生成模块注册、容器直接调用、类型化参数及弹窗结果便利 API |

所有实际转场操作均支持默认 true 的 playAnimation，准确签名和选择配方契约见 10.6。

NodeState 为 Detached、Initializing、Ready、Disposing、Disposed；可见和交互是独立的局部状态，经祖先状态及输入拥有者计算有效状态。生命周期已重构为第 7 节中的前置/完成、状态通知及处理器契约，旧 OnEnterAsync/OnExitAsync 已删除。容器内部执行进入/退出，业务使用相应容器 API，不公开万能节点导航入口。

名称采用 StringComparer.Ordinal；Tab 项容器内唯一，未知名、重复名、重复切换当前项、泛型匹配多项直接抛错。Node 子节点允许同类型多实例，GetChild(name) 必须唯一，否则抛错，关闭使用具体实例。

同容器不排队，并发或重入直接抛错。销毁取消进行中的操作，等待其协作结束，然后按逆序销毁子树。生命周期内销毁当前操作所属节点或祖先直接报错以避免等待自身。回调和自定义资源加载器必须配合 CancellationToken。

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

内嵌页面仍使用基础 Node + PageActivity 处理器；背包更新仅在选中页面有效激活时执行，详情覆盖、父隐藏及模态阻挡均暂停，恢复后继续。素材继续使用已本地化的 Adventure/Nunito 及原许可；本版没有新增外部素材。
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
| P0 | 资源与实例复用 | 根据真实加载需求评估可等待的加载、预加载、取消与释放；区分资源缓存和实例保留；保留实例优先由 Hide/Show 表达，不另加 Close 保留策略；自动实例池暂缓 | 首次打开大型商店；高频背包；退出游戏会话后释放 |
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

返回目标建议使用实例或 Id；相同类型可多次入栈，不能按类型默认找到最近/最早项。非法数量、目标缺失、归属不符和空栈返回直接报错。目标就是当前项、返回根页时已在根页等边界需要明确采用严格报错还是显式成功语义，不作为未定义的静默跳过。

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

Hide 已隐藏、Show 已显示、操作其他容器节点或已销毁对象均报错。判断使用窗口本地已提交显示状态，不用有效 IsVisible 推断：父隐藏不意味着窗口已执行 Hide；窗口本地隐藏时，即使父恢复也不得自动显示。Open 已显示单实例的“取得现有对象”属于明确成功语义，不是忽略非法状态；两次真正并发的容器操作仍报错，不自动排队或合并任务。

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

仅增加实例目标入口，不同时提供 Id/类型/名称/数量/根页重载。历史已有实例引用与 Id，项目如持有 Id 可先在 History 显式解析。目标必须属于当前栈、Ready、仍在历史且位于当前项之前；空栈、缺失目标、其他容器对象、已销毁目标或目标等于当前项直接报错，不转为成功。

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
| 可替换资源加载及预加载/释放 | 已有可替换 loader；Godot 同步加载，无预加载契约 | 候选项；区分资源缓存和节点实例保留 |
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

### 9.3 延期候选（不纳入 V4）

**3. 真正异步加载与资源预加载。** USN 的大型页面加载场景支持这项候选；本 Demo 的小型资源尚无卡顿测量，不能把性能问题描述为已观察事实。若选中，Godot 用引擎支持的后台资源加载流程，实例化/挂载保留场景线程；预加载只持有资源，不创建隐藏节点、不运行初始化。先明确加载句柄、共享持有、取消和显式释放，再定 `PreloadAsync<T>` 及释放 API 所在服务。构造/查询/配置同步，可能等待进行中加载或后端释放的操作才异步；不默认增加 LRU、内存预算或实例池。Demo 在进入背包前准备详情资源，验证不提前创建 View；性能收益需测量，不能用人工等待冒充真实资源加载。

**4. 容器操作观察。** 项目侧面包屑/标题需要一次观察源与目标时可评估；目前节点处理器已有 Source/Target，能完成的场景直接使用现有机制。若确有公共缺口，只增加提交前/后明确观察契约，不把观察者变成隐式导航控制器或错误包装器。

**5. 临时页不保留历史。** 适用于加载/连接等完成后不应返回的独立页面；当前 Demo 没有该流程，且许多加载表现可以作为临时覆盖完成。先判断覆盖是否足够；只有页面确实属于同一栈时，才考虑 USN 式 Push 的历史保留选项。移除时点及清理规则必须明确，不等同 Replace，不作为本轮默认范围。

**6. 返回请求和页面焦点。** 从当前 Demo 可直接验证：Esc 关闭顶层确认或退回设置/背包详情，返回后继续选中原物品。当前只有 Modal 捕获/恢复焦点，Stack/Tab/Panel 未协调页面焦点。属于本工程自身缺口；本轮核对的 USN 文档没有提供其对应通用返回/焦点契约的依据，不能说是照搬 USN 功能。建议单独迭代或由用户选择纳入，不与动画/遮罩顺带捆绑。

### 9.4 范围边界与交付要求

用户已确认 V4 只包含 1、2。资源异步加载/预加载、容器操作观察、临时页历史选项、返回请求与页面焦点均延期；工具增强、编辑器、Analyzer、Replace、自动实例池、全局路由和项目默认层级继续不纳入。用户随后已授权实施，核心、适配器、Demo 与测试已同步迁移，交付见 9.6。

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

遮罩动画与窗口动画独立：新遮罩先淡入，随后播放窗口入场；窗口退出后，该操作需释放的遮罩再淡出；共享遮罩的嵌套打开/关闭不重复播放。窗口/页面源目标动画并行，遮罩不参加其源目标列表。这一时序避免缩放全屏背景，且整个容器任务等待所有阶段。若以后调整为同时播放须单独验证，不宣称当前已经并行淡入窗口与遮罩。

`UINode.DismissRequested` 为可选 `Func<CancellationToken, Task>` 业务处理器。普通设置配置为 CloseAsync(this, cancellationToken: token)，Confirm 配置为 Complete(false) 并返回已完成任务。`RequestDismissAsync(token)` 返回 Task<bool>：处理器缺省是明确的“不允许外部点击关闭”，返回 false；已处理返回 true；不存在模态、失去输入、操作中或重复并发请求直接报错。异步业务失败原样传播，不转换为 false。该入口只用于模态外部点击，不扩展为未选择的 Esc/返回路由系统。

GodotBackdrops.Dim 要求提供 `Action<Task>` 观察入口。Demo 使用已有 Track，任务由应用持有，在帧入口记录原异常、退出 1 并重新抛出；即使遮罩已 QueueFree 也不丢弃任务。没有 async void、后台任务或遮罩自身销毁时停止观察的机制。没有处理器时遮罩仍拦截鼠标，不穿透背景。

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
| 基础 Modal 嵌套和关闭 | Modal Open/Close，额外支持结果等待 | 多层一次返回仍缺失 |
| 页面生命周期及外部处理器 | V2 生命周期、AddLifecycle/RemoveLifecycle | 外部处理器执行顺序仍有差距，见 15 |
| 通用、逐页和自定义转场 | Context/Container/Node 配方继承及自定义执行对象 | 配置维度及运行信息仍有差距，见 6—11 |
| 伙伴页面参与动画 | TransitionContext.Source/Target | 自定义代码能实现；条件选择缺少直接配置入口 |
| 编辑器动画接入 | Godot AnimationPlayer | 属于 Godot 对应方案，未实现 Unity Timeline 适配 |
| 基础模态遮罩和外部点击 | 工厂、Shared/PerModal、DismissRequested | 不等同 USN 全部策略与时序，见 13 |
| 内嵌页面复用和互斥切换 | Tab Bind/Switch | 符合已确认默认工作流；隐藏当前项和动态资源项另列 |
| 页面数据和创建定制 | 创建参数、类型工厂、初始化生命周期 | 没有照搬 onLoad 回调或特定 DI 系统 |
| 销毁清理、容器子树释放 | Node/Context DisposeAsync | 不复制 USN 后台未等待清理；本工程必须观察失败 |

#### 剩余能力与配置差距清单

编号 1—5 延续此前候选，6—16 为本轮补充。缺失配置能力不等于完全无法自定义实现；以下明确区分。

| 编号 | 能力 | 当前差距 | 游戏使用场景 |
| --- | --- | --- | --- |
| 1 | 资源取得、实例创建分离；异步加载与释放 | 接口直接返回实例；Godot 同步加载 | 大型界面按需加载和关闭后归还资源 |
| 2 | 预加载与显式释放 | 没有独立资源持有凭据 | 菜单预备背包/商店资源 |
| 3 | Modal 一次返回指定下层实例 | 只有顶层 Close | 商品确认后直接回商店 |
| 4 | 临时页不保留历史 | 每次 Push 都保留 | 资源准备页不出现在返回路径 |
| 5 | 容器整体操作前后回调 | 只有节点生命周期与状态通知 | 外壳统一同步标题/返回按钮 |
| 6 | 单次操作选择是否播放动画 | V5 已实现容器操作的 playAnimation 选项，生成 API 和遮罩同步透传 | 首次恢复菜单立即显示，之后操作正常动画；不临时改共享配方 |
| 7、8 | 根据转场上下文选择动画 | V5 已实现 Transitions.Select 与 TransitionContext.Phase | 阶段、来源/目标等条件由同一函数判断，覆盖推进/返回差异和不同来源的进入表现 |
| 9 | 转场绘制顺序配置 | 支持固定 Push 目标在前、Return 源在前；没有项目覆盖策略 | 旧页覆盖新页退出，而不是始终由新页盖住旧页 |
| 10 | 动画参数配置 | Godot 预设有时长、部分缩放；没有延迟、缓动选择及通用起止位置/缩放/透明度配置 | 美术调整移动与淡入节奏，不为每套参数写自定义配方 |
| 11 | 转场状态、类型、进度及变化通知 | 当前没有对应公开运行信息；内部操作守卫不等于公共查询 | 窗口动画期间协调外壳装饰/按钮状态；这是运行能力，不能自动视为诊断系统 |
| 12 | 转场输入控制策略 | 普通容器锁局部，模态临时锁全局；无法配置全部/局部锁及允许转场期交互 | 主内容换页时允许独立侧栏继续交互 |
| 13 | 更多遮罩策略及时序 | Shared 会移到顶层下，PerModal 每层创建；没有始终位于首层下及动画前/后重排的全套选项 | 某项目希望嵌套弹窗不再暗化底层弹窗，或旧弹窗退出后才移动遮罩 |
| 14 | 隐藏当前 Tab 项并进入无选中状态 | Tab 只能 Switch；隐藏整个父容器不会清空 Current | 某互斥侧栏收起当前页面，但保留外壳和全部页面实例 |
| 15 | 外部生命周期处理器顺序 | 业务虚方法先执行，处理器按注册顺序；不能声明先于业务/后于业务及优先级 | 通用数据准备行为先于 View 进入，另一个行为在业务处理后执行 |
| 16 | 独立资源动态 Tab 项 | 当前 Bind 采用既有实例，没有从注册资源创建项的直接入口 | 项目扩展页面来自独立资源；内嵌 Tab 仍是默认，不替换已有工作流 |

来源：[USN 转场章节](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#screen-transition-animation) 对应 7—10；[其他功能](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#other-features) 对应 3、4、11—13；[SheetContainer 源码](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Sheet/SheetContainer.cs) 对应 6、14、16；[Page 源码](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/Assets/UnityScreenNavigator/Runtime/Core/Page/Page.cs) 对应 7、11、15；资源和回调依据见 10.4。这里只记录差距，不据此授权全部实现。

补充语义：14 是隐藏当前项、Current 变为无选中，区别于父容器整体隐藏。16 指加载资源并实例化后纳入 Tab，不把 USN PreloadedAssetLoader 解释成接管已挂载的场景实例。13 的 USN Only First Backdrop 与本工程 Shared 重排并不相同。12 的允许交互不意味着同一容器允许并发操作，USN 也禁止同一容器转场中再次切换。

#### 配置便利、后端特性和有意采用不同方式的部分

| USN 功能/方式 | 本工程差异 | 处理判断 |
| --- | --- | --- |
| 每个容器覆盖资源加载器 | 目前只在 UIContext 配置 Loader | 有需要时纳入 1 的资源来源协议；不默认增加多套资源缓存 |
| 直接注册已加载 Prefab 引用 | 内嵌 Element 可绑定；资源注册仍使用字符串，Godot 未提供 PackedScene 引用注册便利入口 | 与资源预加载不同，可作为后端便利能力；不要误称已完整提供 |
| Inspector 配置动画、遮罩、加载器 | 当前以 C# 配置和 Godot 场景资源为主 | 编辑器配置是易用性差距，后续独立规划；不把 Unity Inspector 放进核心 |
| Container.Of/Find、全局 Instances | Root/Parent/GetChild、项目持有引用 | 层级定位可实现；缺少直接查祖先/深层名称的便利 API，但不因此引入全局静态注册表 |
| 按数量/ID 返回、指定字符串实例 ID | 当前主要通过具体实例定位 | 功能需求可对应，全部重载不是必须补齐的功能 |
| 同步资源加载选项 | 后端当前实际同步；公共容器流程可能仍需异步生命周期/转场 | 资源是否同步与整个开窗 API 是否同步是两件事，不能为方法数量而增加同步阻塞入口 |
| Container 内容裁剪、布局填充 | Godot 资源设置 ClipContents/锚点；Demo 已使用包装层和裁剪 | 引擎适配/资源职责，不新增渲染无关核心的矩形裁剪系统 |
| Coroutine/Task/完成回调多种等待方式 | C# Task/ValueTask 和应用任务观察 | 当前足以观察结果/异常，不必复制 Unity 协程句柄 |
| Resources/Addressables、Timeline、ScriptableObject | 目前 Godot 实现；Unity 专用适配未实现 | 不能宣称支持 Unity 后端；属于后续引擎适配交付，不是核心流程缺失 |
| Pop 后实例复用/实例池 | 本工程 Stack/Modal 关闭后销毁；Panel Hide 和 Tab 保留 | USN 也不复用已 Pop 项；实例池不是 USN 缺口，不自动新增 |

所以不能概括为“仅剩五项”。后续规划同时保留完整差距清单与每版优先范围；不把所有差异都认定为必须照搬的设计。

### 10.3 当前范围与其他候选

| 编号 | 功能 | 实际游戏场景 | 建议 |
| --- | --- | --- | --- |
| 1 | 资源加载与实例创建分离；持有/释放；Godot 真正异步加载 | 较大的背包、商店按需加载，关闭后正确归还资源 | 推荐 |
| 2 | 预加载与显式释放 | 主菜单提前准备设置/背包，进入详情前准备下一页 | 推荐，依赖 1 |
| 3 | 弹窗一次返回指定下层实例 | 商店→商品详情→购买确认，完成后直接回商店，避免逐层闪现 | 推荐 |
| 4 | 临时栈页面不保留历史 | 菜单→资源准备页→目标页面，返回时跳过准备页 | 次优先，可选 |
| 5 | 容器整体操作回调 | 背包外壳统一更新详情/手记标题与返回按钮，子页面不重复通知 | 次优先，可选 |

上表保留此前 1—5 的候选用途，不代表当前执行顺序。用户已指定先实现 **6、7、8**，实际契约见 10.6；1—5 尚未授权实施。

### 10.4 实现与 API 方向

以下为候选，名称、签名及细节在用户选择后定稿；示例不代表当前已提供。

#### 1、2. 资源流程与预加载

[USN 资源加载](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#loading-screen-resources) 支持替换加载器与资源释放；[预加载](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#preloading) 提供 Preload/ReleasePreloaded。当前 IUIResourceLoader.LoadAsync 直接创建 IUIElement，提前调用会产生实例，不能当作单纯资源预加载。

应重构为“取得可实例化资源 → 创建/挂载实例 → 节点初始化 → 容器流程”。后端实现资源句柄，核心不依赖 PackedScene/GameObject。普通 Open/Push 自动取得所需资源，使用者继续使用已有容器 API，不为普通开窗增加手动资源管理。

预加载只持有资源，不创建 Node/显示实例，不执行初始化或进入生命周期。候选 `ui.PreloadAsync<InventoryWindow>(token)` 返回持有凭据；打开窗口时复用已准备资源。凭据提供明确释放入口：即时归还引用使用同步 Dispose，确实需要等待才用 DisposeAsync；取得异步不等于释放也必须异步。最终完整示例在协议定稿后提供。

预加载与打开实例共享底层资源、分别持有；释放预加载不能销毁仍使用资源的窗口。关闭实例归还其持有，Context 销毁清理剩余资源。重复请求是否合并及一个等待者取消的隔离规则须明确，不能误取消其他使用者。加载/创建失败原样传播并清理已取得资源，附加清理失败不能掩盖最初异常；不缓存失败为成功、不自动重试或返回空资源。

Godot 使用真正可等待的资源加载，实例化/场景树操作留在引擎线程；不以 Task.FromResult 包装同步读取宣称异步，也不把场景树操作放进 Task.Run。后端是否能中止底层加载及取消后的资源归还，实施前按可用 API 核对。

Demo 复用菜单、背包和详情：菜单预备常用资源，进入时创建页面，预加载持有释放后现有页面仍正常使用。验收包含预加载无实例、重复持有、取消/失败、独立释放、Context 清理及 Godot 线程/挂载。不用人工延时声称性能提升，不增加技术缓存面板。实例池、LRU、内存预算不顺带纳入。

#### 3. 弹窗一次返回指定实例

[USN 多层返回](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#pop-multiple-screens-at-once) 同时支持 Page/Modal。本工程 Stack 已有 PopTo，Modal 只有关闭顶层。候选 `await modals.CloseToAsync(shop, token)`，shop 是当前容器中仍存活的下层具体实例；保留目标，移除其上的弹窗。

沿用 Open/Close 术语，方法名待定。目标不按类型猜测、不依赖项目全局 ID；不同时堆叠数量、索引、类型、名称等重载。

整体准备/提交一次，中间项不重新进入，输入与焦点只交给最终目标。需明确被移除项的动画和生命周期：USN 中间 Modal 可同时退出；本工程建议可见的被移除项并行退出，隐藏项只清理，最终目标正常恢复，实施前核对现有覆盖可见性语义。

Shared 遮罩保留并移到目标下；PerModal 清理被移除层。准备/动画失败保留原栈；提交后清理失败传播原异常并明确已提交。带结果等待的对话框被移除时必须完成或取消等待，协议先定稿，禁止悬挂任务或默认结果伪装业务确认。

Demo 使用商店详情/确认的纯 UI 流程，或复用已有嵌套弹窗形成同类场景；无 Gameplay。验证归属错误/已销毁目标、一次交接、两种遮罩、结果等待、失败恢复与提交后清理失败。

#### 4. 临时页不保留历史

[USN 不保留历史](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md#dont-stack-pages-in-history) 用于加载页：显示时仍为当前页，推进下一页后才移除，返回时跳过。

候选 `stack.PushAsync<LoadingView>(retainInHistory: false, cancellationToken: token)`，生成器同步支持选项。A→临时 B→C 后历史为 A/C；进入 C 失败时 B 仍是当前页，不能提前销毁。属于 Push 的历史选项，不增加 Replace，不立即替换 A。

尚未推进下一页时仍能正常返回 A；连续临时页、初始临时页、取消和提交后清理失败须明确。若选中，Demo 以实际资源准备页衔接菜单/背包，不制造假网络或 Gameplay 场景。

#### 5. 容器整体回调

USN README 的生命周期章节为 Page/Modal/Sheet 提供容器 Before/After 回调，区别于外部节点生命周期。候选同步注册观察器，回调携带操作种类、Source/Target 与移除项；外壳在提交后根据当前页更新标题与返回按钮。

等待页面准备继续使用已有异步生命周期，不另建异步消息总线、否决守卫、操作队列或诊断系统。开始与成功提交通知点明确，不把失败报告为成功。回调异常记录并原样传播；提交后回调失败不回滚已提交页面，调用方必须能判断状态。订阅可明确移除并随宿主销毁解绑。

### 10.5 边界与交付

焦点记忆、返回输入路由、窗口置前、多个模态容器仲裁、编辑器与绑定生成不作为此次主范围。动态资源 Tab 与 9—13 的其他转场配置仍保留为后续差距；6—8 已实现。内嵌 Tab 不因 USN 的动态 Sheet 工作流而被替代。Analyzer、诊断分析和 Replace 仍不引入。

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
