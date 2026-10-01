# UI 管理框架设计文档

更新日期：2026-10-02（Asia/Shanghai）。当前已实现初版框架与 Godot Demo；实际契约和边界见第 6 节。

## 目录

1. [约束与范围](#1-约束与范围)
2. [概念与职责](#2-概念与职责)
3. [API 场景与归属](#3-api-场景与归属)
4. [待定设计与调研](#4-待定设计与调研)
5. [V1 开发文档入口](#5-v1-开发文档入口)
6. [初版实际契约](#6-初版实际契约)

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

主操作与清理同时失败的协议待定，不允许用 finally 中的新异常覆盖原异常，也不能吞掉任一失败。

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

## 5. V1 开发文档入口

V1 范围建议、默认规则、开发阶段及验收标准统一维护在 [V1 开发文档](v1-development.md)。本设计文档维护通用概念与已确认约束，不重复保存版本开发清单。

## 6. 初版实际契约

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

NodeState 为 Detached、Initializing、Ready、Disposing、Disposed；可见和交互是独立的局部状态，经祖先状态及输入拥有者计算有效状态。生命周期扩展点为 OnInitializeAsync、OnEnterAsync、OnExitAsync、OnDisposeAsync。容器内部执行进入/退出，业务使用相应容器 API，不公开万能节点导航入口。

名称采用 StringComparer.Ordinal；Tab 项容器内唯一，未知名、重复名、重复切换当前项、泛型匹配多项直接抛错。Node 子节点允许同类型多实例，GetChild(name) 必须唯一，否则抛错，关闭使用具体实例。

同容器不排队，并发或重入直接抛错。销毁取消进行中的操作，等待其协作结束，然后按逆序销毁子树。生命周期内销毁当前操作所属节点或祖先直接报错以避免等待自身。回调和自定义资源加载器必须配合 CancellationToken。

故障后执行一致性清理并重新抛出原异常，不把清理当成业务成功或降级。额外清理异常保留在主异常 Data 的 UIFramework.SecondaryExceptions 中，提供 UIFailure.GetSecondaryExceptions 获取，单独记录日志；主异常不包装，使用 throw 或 ExceptionDispatchInfo 保留原堆栈。

Godot 调用必须在场景线程；GodotUIHost.VerifyAccess 校验，初版不自动跨线程调度。场景加载采用同步 ResourceLoader.Load；异步接口表达可等待生命周期和取消协议，不宣称已经实现后台资源加载。隐藏页面不会自动停掉业务任务，业务订阅和任务由生命周期回调及 Lifetime 管理。

Root 无具体引擎显示对象，不自动创建项目容器。Demo 自行在 Root 下附加菜单、场景、卡片及模态节点。资源文件、样式和控件是后端能力，未引入统一跨引擎资源格式。

Godot 初始化便利 API（2026-10-02 已实现）：

| API | 职责 |
| --- | --- |
| GodotUI.Create(owner, loader?, logger?) | 从已进入场景树的 Godot Node 构造宿主、默认场景加载器及日志服务，返回 UIContext；可显式替换加载器/日志，不注册业务类型、不自动创建项目容器 |
| parent.CreateContainerAsync<T>(name, mount, token) | 创建指定 ContainerNode 派生类型并挂载到已有 Control；支持自定义派生容器，名称与层级由项目指定 |
| node.AttachToAsync(parent, name, control, token) | 将已有逻辑节点绑定到已有 Control，返回同一逻辑节点，适合自定义构造/依赖注入 |
| tabs.BindAsync(name, pageControl, token) | 自动创建基础 UINode，绑定既有内嵌 Control；不加载或克隆 |
| tabs.BindAsync(name, node, pageControl, token) | 为内嵌页面绑定项目提供的逻辑节点 |
| node.GetControl() / GetControl<T>(path) | 获取节点显示 Control 或指定路径的类型化子控件，避免业务重复转换显示句柄 |

上述扩展方法只在 Godot 适配器中提供。Create 应从场景线程调用；显示 Control 必须仍有效并在场景树中，核心继续验证节点状态、归属、并发与取消。AttachToAsync、CreateContainerAsync、Control 版 BindAsync 使用 borrowed 所有权，逻辑节点销毁不会释放父场景中的 Control。项目自行创建且需由框架释放的显示根仍显式使用 `new GodotUIElement(control, owned: true)` 配合底层 AttachAsync。挂载失败仍执行核心清理并原样抛出；重复项、缺失控件或类型不匹配均报错，不隐式选择其他控件。创建的 UIContext 必须由调用方 DisposeAsync，便利 API 不隐藏任务观察或自动销毁流程。
