# UI 管理框架设计文档

更新日期：2026-10-02（Asia/Shanghai）。当前已实现并验证 V2 生命周期和游戏 UI Demo；实际契约见第 6、7 节。

## 目录

1. [约束与范围](#1-约束与范围)
2. [概念与职责](#2-概念与职责)
3. [API 场景与归属](#3-api-场景与归属)
4. [待定设计与调研](#4-待定设计与调研)
5. [版本开发文档入口](#5-版本开发文档入口)
6. [当前实际契约](#6-当前实际契约)
7. [V2 生命周期契约](#7-v2-生命周期契约)
8. [V3 功能覆盖方向](#8-v3-功能覆盖方向)

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
| PanelContainerNode | OpenAsync<T>(args, token)、CloseAsync(node, token)；独立多实例 |
| StackContainerNode | PushAsync<T>(args, token)、PopAsync(token)、Current、History；覆盖保留，出栈销毁 |
| TabContainerNode | BindAsync(name, node, embeddedElement, token)、SwitchAsync(name, token)、可选 SwitchAsync<T>(token)、Items、CurrentName；仅绑定既有项 |
| ModalContainerNode | OpenAsync<T>、CloseAsync(node)、ShowDialogAsync<T,TResult>；仅关闭当前模态，持有输入并恢复焦点 |
| DialogNode<TResult> | 可选结果契约，Complete(result)；普通模态窗口仍可为一般 UINode，不要求继承此类 |
| GodotUIElement | owned 动态场景与 borrowed 内嵌 Control 句柄；显隐、输入、排序及按所有权销毁 |
| Source Generator | UIResource 特性生成模块注册、容器直接调用、类型化参数及弹窗结果便利 API |

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

`LifecycleContext` 不可变，含 Node、Container（可空）、Reason、Source/Target（可空）和 IsFirstEnter。原因枚举为 Initialization、Open、Push、Covered、Switch、Return、Close、Modal、Ancestor、Disposal，不包含项目层级名称。IsFirstEnter 在本次上下文创建时取值；节点提交显示后 HasEntered 为 true，完成通知失败也不重置。首次 Tab 显示与再次显示均为 Switch，用 IsFirstEnter 区分。

`StateChangeContext` 含 Operation、Node、Previous/Current。直接参与操作的源/目标保留各自原因；后代因祖先变化使用 Ancestor；模态造成其他节点激活变化使用 Modal。初始化和销毁可无 Source/Target。

- IsVisible 是本地显示意图与祖先可见的合成结果，转场可有临时显示。
- IsActive 是已提交可见、本地激活意图、祖先激活和模态输入范围的合成结果，尚未提交的转场不改变业务激活。
- IsInteractive 进一步包含本节点和祖先的临时输入锁，表示后端实际输入资格。
- 临时输入锁不会发 Activated/Deactivated；稳定状态变化在提交点发布。转场期间显隐查询可能反映临时显示状态，Current/History 仍是此前提交状态。

状态通知先提交整棵树的有效状态快照，然后发通知：失去按后序（兄弟按挂载逆序），每节点先 Deactivated 后 VisibilityChanged(false)；获得按前序（兄弟按挂载顺序），每节点先 VisibilityChanged(true) 后 Activated。只通知实际变化。父隐藏不会重跑子节点初始化、Entering/Entered 或动画；未选中 Tab 不重复失活。结构性容器初始化完成可激活，Root 从构造时就可见/激活。

父 View 初始化中可以选中内嵌 Tab，其局部进入已提交，但有效可见/激活仍受尚未显示的父 View 限制；父 View 显示时才传播有效状态通知。

### 7.3 容器提交与失败边界

统一顺序：操作守卫 → 捕获模态原焦点（如适用） → 临时输入锁 → 前置回调与转场 → 检查取消 → 提交 Current/History/显隐与模态输入所有者/焦点 → 解除临时锁 → 状态变化通知 → 源 Exited → 目标 Entered → 解除守卫。通知中能查询提交状态和可交互性，但不能修改层级或重入操作。

Panel 独立打开/关闭；Stack Push 将旧项退出并保留，新项入历史，Pop 移除栈顶并以 Return 恢复旧实例；Tab 切换既有项；Modal 覆盖旧项只导致失活，不重放旧项退出/进入。正常 Close/Pop 先发 Exited，再销毁目标，外层任务等清理完成。

提交前失败或取消恢复框架状态并清理新实例，不发本次成功完成或临时显隐通知，不重执行业务准备；业务前置回调的外部副作用由业务负责。源实例先恢复，再清理失败新实例，避免清理发布临时隐藏。转场必须实现同步 IUITransition.Restore(element, visible)，恢复其修改的透明度等表现状态；不重播动画、不转成成功或换用默认转场。GodotFadeTransition 恢复透明度，恢复异常作为附加原异常暴露。

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

| 优先级 | 功能组 | 建议范围 | 游戏场景 |
| --- | --- | --- | --- |
| P0 | 实例与显示管理 | 明确单实例/多实例打开策略；实例查询；显示、隐藏、再次显示；刷新参数/数据入口；批量关闭。重复请求的处理必须由显式策略定义，不隐式挑选实例或忽略错误 | 背包、地图、任务、商店反复打开；同时比较多个物品窗口 |
| P0 | 栈流程扩展 | 替换当前页、返回指定历史实例、返回根页、清空/重置历史；定义中间项的退出与销毁时序 | 主菜单→存档→角色创建；商店多级界面；结算后回主菜单 |
| P0 | 显示排序与模态范围 | 项目定义容器显示顺序、窗口置前；明确多个容器的模态优先关系；全局与局部输入阻挡；遮罩点击关闭等项目可配置行为 | 聊天与背包共存；暂停菜单；加载界面覆盖其他窗口；局部选择弹窗 |
| P0 | 返回与焦点 | 顶层返回请求分发、业务拦截/消费、明确处理顺序；初始焦点、页面焦点记忆与恢复；适配键盘/手柄返回入口 | Esc 逐层返回；未保存设置确认；返回背包后继续选中原物品 |
| P0 | 资源与实例复用 | 真正可等待的资源加载、预加载、取消与释放；区分资源缓存和实例保留；显式关闭销毁/保留策略及主动清理；定义复用时进入和数据刷新，初始化不重复 | 首次打开大型商店；高频背包；场景加载前预热；退出游戏会话后释放 |
| P0 | 项目作用域 | Root 下由项目组织菜单/游戏会话等子树；统一关闭和释放一个作用域、取消其未完成操作、限制跨作用域残留；保持全局项目 UI 的生命周期 | 主菜单进入游戏会话；返回登录界面；切换存档；全局提示持续存在 |
| P1 | 短暂与附着 UI | 在已有 Node/容器能力上验证限时提示、显式提示排队/替换规则、Tooltip 随目标失效关闭；位置和屏幕边缘约束由适配器处理 | 道具获得提示、错误提示、奖励通知、鼠标/手柄物品 Tooltip |
| P1 | 内嵌 Tab 完整管理 | 项查询、可用状态与绑定移除；当前项不可用时要求项目明确选择目标；内嵌资源继续为默认路径，不据此引入动态资源 Tab | 根据角色权限显示设置页；不同商店页签；页面内容改变后维护选择 |

以上功能需要先检查现有模型能否完整承载，存在职责缺陷时重构，不机械地在通用 Container 上添加全部操作。单实例策略不能替代多实例身份；实例复用不能成为失败兜底；资源缓存不等于实例池。多个容器的显示顺序、输入优先级和逻辑归属需要分别定义。

### 8.3 验收方向建议

Demo 扩展为完整的游戏 UI 场景集合：登录/主菜单与会话切换、背包与地图、任务与商店、物品详情与 Tooltip、暂停设置与嵌套确认、加载遮挡、奖励提示及结算返回。不含 Gameplay；每个场景必须对应框架能力和核心测试，不能只增加界面外观。

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
| ReplaceAsync<T> | [A,B,C] → [A,B,D] | 用新实例替换当前栈顶，不影响更早历史 |
| ClearAsync | [A,B,C] → [] | 退出当前页、清理全部历史，保留可再次使用的容器 |
| ResetAsync<T> | [A,B,C] → [D] | 准备新页并一次提交新历史，不拆成先 Clear 后 Push |

返回目标建议使用实例或 Id；相同类型可多次入栈，不能按类型默认找到最近/最早项。非法数量、目标缺失、归属不符和空栈返回直接报错。目标就是当前项、返回根页时已在根页等边界需要明确采用严格报错还是显式成功语义，不作为未定义的静默跳过。

批量返回只让当前可见源页退出、最终目标进入；已被覆盖并完成 Exited 的中间页不补播进入/退出或转场，仅按栈顶向下清理。Clear 不恢复中间页；Reset/Replace 先准备目标，提交前失败保持原历史并清理新实例；提交后清理失败不回滚已提交历史，任务仍失败。所有操作均等待其负责的清理，不启动无法由调用方观察的后台释放。

新增操作需要补充相应生命周期原因与 Source/Target 语义；上面是方向建议，不能把现有 Reason 枚举硬套为已实现支持。多页清理顺序、通知失败后的必要清理、取消边界要在正式开发清单中给出验收用例。

#### 本轮建议验证场景

窗口：背包隐藏后重开保留当前内嵌 Tab 与选中物品；重复打开不增加单实例；同类型两个详情窗口分别关闭；批量关闭包含隐藏窗口。栈：主菜单→存档→角色创建→确认，演示返回指定存档实例、替换确认页、重置回主菜单和清空后再次推入。对新页准备失败、返回目标缺失、多实例歧义和批量清理失败补核心测试。Demo 展示业务流程，不能以 API 按钮面板替代。

### 8.6 其他方案如何实现 Replace（调研补充，非已确认设计）

这里的 Replace 严格指 `[A,B,C] → [A,B,D]`：B 不暂时恢复，C 不留在返回历史中；新页准备失败时尽量保持 C。以下区分公开 API 可组合的流程与需要扩展框架的方案；没有运行验证这些组合。

- **UnityScreenNavigator**：核对的 PageContainer 没有直接 Replace 入口。顺序等待 `Pop(false)` 再 `Push("D", true)` 可以得到目标历史，但 Pop 仍会恢复 B 并调用其生命周期，关动画不能取消这些回调；Push 失败时 C 已移除。这是两个操作，不是一次替换。若 C 当初以 `stack: false` 推入，下一次推 D 会移除 C，可达成相同历史结果；已正常入栈的 C 不能通过给 D 设置 `stack: false` 追溯改变。任意栈顶的完整替换需要扩展源码，在一次操作中加载 D、转场 C→D、替换历史并清理 C，不能从外部修改只读历史或直接 Destroy C 绕过管理器。
- **CommonUI**：核对的容器公开 GetActiveWidget、AddWidget、RemoveWidget，没有独立 Replace 契约。基于这些 API 的组合建议是保存旧顶 C，添加 D，待 D 成为显示项且转场结束后移除已隐藏 C；结果可为 `[A,B,D]`。这是 API 层面的推导，具体版本的转场完成、重入和释放时序需集成验证，不能宣称自带事务替换。先让 C Deactivate 会自动出栈并恢复 B，不满足“不激活中间页”的要求；不可移除的 RootContent 也不能套用该组合。
- **QFramework UIKit**：其 UIPanelStack 保存已关闭页面的 PanelInfo，而不是当前显示项组成的完整实例栈。若当前页 C 尚未压入该历史，关闭 C 后直接打开 D、不要保存 C，便可保持此前返回记录。若已把 C 保存为历史，需扩展历史管理移除该记录。关闭再打开并非原子操作，D 创建失败无法自动保留 C；实例状态恢复也不是该栈的保证。
- **FairyGUI**：Window 是窗口显示能力，不是内置页面历史。项目可自行准备 D、隐藏/销毁 C 并显示 D；如果还需要 `[A,B,D]` 的返回关系，历史、转场协调和失败恢复均由项目的页面管理器负责，不能仅把窗口切换称为栈 Replace。

来源沿用 8.4 的官方源码，并核对 [CommonUI ContainerBase](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/CommonUI/UCommonActivatableWidgetContaine-) 的公开操作。建议本工程把 Replace 实现为 StackContainerNode 内部的一次操作，共享准备、提交与清理机制；不在公共 API 层包装 PopAsync + PushAsync，避免额外恢复 B 及新页失败后丢失 C。该建议尚未实现。
