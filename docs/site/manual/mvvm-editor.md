# MVVM 编辑器

在界面根节点添加 `MvvmBindings`。Unity 打开 **Window → UIFramework → View Bindings**；Godot 启用插件后使用底部 **View Bindings** 面板。

## 1. 配置 VM 来源

**Unity**：通过 Create → UIFramework → MVVM → Global ViewModel Catalog 创建目录 SO，保存到 `Assets/Resources/UIFramework/Mvvm/`。将可用的 SO VM 拖入目录的 ViewModels 列表。框架创建 Context 时自动读取所有目录并注册资产，首次使用时克隆；同一 Context 共用实例，不同 Context 默认独立。

窗口不再保存 ViewModels 列表，也不选择 Creation。左侧 Global Viewmodels 只浏览目录中的资产；拖动成员到绑定表，只保存 `资产名.成员`。绑定详情可开启“全局来源”并选择名称；运行时由 Provider 查找。名称区分大小写，目录中同名资产直接报错；重命名后须更新绑定路径。

集合条目关闭“全局来源”，填写 `Item.Title`；Item 由集合自动注入。业务对象也使用本地来源，在初始化中调用 `SetViewModel`。本地来源不保存资产或类型引用，运行时由注入对象提供成员。没有预览来源时，编辑器明确显示“缺少预览来源”。

**Godot**：继续添加具名来源，选择 Resource 和 CreateInstance／Manual／SharedAsset。Manual 实例在初始化中由业务提供。VM Resource 使用 `[Tool]`，普通 C# VM 通过代码创建和绑定。

## 2. 建立 View Bindings

选控件和目标属性，再拖入成员或填写路径，例如 `Player.Gold`。

| 场景 | 配置 |
|---|---|
| 金币文本 | OneWay，FormatNumber |
| 音量滑杆 | TwoWay，分别选择需要的正向／反向转换 |
| 购买按钮 | Command，`Shop.Buy` |
| 背包列表 | Collection，条目 Prefab／PackedScene |

转换器按输入、输出类型筛选。条目来源名称固定为 `Item`，例如 `Item.Title`。列表布局在资源中配置；Godot 可使用 VBoxContainer／GridContainer，Unity 可使用 LayoutGroup 或项目布局代码。

绑定表支持搜索、删除和重排。Unity 搜索结果中禁用重排；清除搜索后调整完整顺序。Unity 控件 Inspector 有绑定入口，Godot 常见控件属性下有绑定按钮。

## 配置转换器

例如把 `Player.Gold` 的整数显示为金币文本：选择 **OneWay → 正向转换 → FormatNumber**。输入绑定到 `Player.Gold`，金币变化时自动更新文本。

选择转换函数后，详情显示输入和输出类型。**转换器输入**有两种配置：

- **ViewModel**：填写来源成员路径，支持嵌套成员和对象替换。
- **Constant**：填写输入常量，例如 `125`。按转换函数的输入类型解析，不需要创建 VM。支持文本、布尔、数字和枚举；数字使用小数点，不使用千位分隔符。

常量用于 OneWay／OneTime。TwoWay 的正向输入来自 VM，反向输入来自控件，分别选择转换函数；框架不会推断逆转换。错误常量、丢失函数和转换异常直接报错。

项目自定义函数使用 `[Converter]` 标记的静态方法，接受一个参数并返回一个结果；由 Cecil 生成直接调用入口。列表只显示类型匹配的函数。预览和运行时执行同一转换配置。

## 3. 预览初始值

点击 **预览初始值**。预览使用独立 Runtime 和资产克隆，只刷新显示及列表，不执行命令、不反向写入原资产。结束后恢复控件原值。

Unity 全局预览从 Provider 目录取得模板并独立克隆；本地来源没有样本时禁用预览。Godot Manual 来源仍使用显式引用的预览资产，运行时必须由业务调用 `SetViewModel`。

## 保存

Unity 配置保存在组件中，随 Prefab／场景保存，支持 Undo 和 Prefab Override。Godot 场景组件保存绑定 Resource 和原生控件引用数组，编辑器同步修改两者，支持原生 Undo。控件引用不依赖框架维护的层级路径；重命名、换父节点及保存重开保持引用。

预览不保存场景。编辑配置前先结束预览，避免展示值与编辑值混在一起。

相关：[入门与实例方式](mvvm.md) · [API](../api/mvvm.md)

## 命令与集合配置

命令选择无参数、常量或来源路径；编辑器显示业务参数类型并检查匹配。无业务参数的命令不显示参数字段。

集合选择条目资源，添加父来源→条目来源映射。可选工厂引用实现 `ICollectionItemFactory` 的 Unity 组件或 Godot 节点。工厂也接收 `IsPreview`，预览分支仅做展示准备；预览不执行命令，结束后恢复控件。Item 自动提供。

通过 `IBindingTargetProvider` 注册的自定义控件成员直接出现在属性列表中，预览无需另外安装解析器。

Godot 工厂节点脚本需要 `[Tool]` 才能用于编辑器预览。将命令改为无业务参数的方法时，通过编辑器提供的操作明确清除旧参数配置。
