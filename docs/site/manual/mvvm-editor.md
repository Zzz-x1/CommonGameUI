# MVVM 编辑器

在界面根节点添加 `MvvmBindings`。Unity 打开 **Window → UIFramework → View Bindings**；Godot 启用插件后使用底部 **View Bindings** 面板。

## 1. 添加 Viewmodels

添加具名来源，例如 `Player`、`Settings`、`Inventory`。选择可引用的 VM 资产及创建方式。

- 独立详情选 **CreateInstance**。
- 业务传入对象选 **Manual**，在节点初始化调用 `SetViewModel`。
- 多个窗口使用同一资产时选 **SharedAsset**。

资产列表只发现标记为 `ViewModel` 的原生 `ScriptableObject`／`Resource`，支持搜索和定位。Godot VM 资源使用 `[Tool]`。类型由资产引用取得，不保存类型名称；普通 C# VM 只通过代码创建与绑定。

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

Manual 来源的引用资产用于声明类型和预览样本。预览克隆该资产；运行时仍必须由业务调用 `SetViewModel`，不会自动使用样本代替。缺少资产时不能开始预览。

## 保存

Unity 配置保存在组件中，随 Prefab／场景保存，支持 Undo 和 Prefab Override。Godot 场景组件保存绑定 Resource 和原生控件引用数组，编辑器同步修改两者，支持原生 Undo。控件引用不依赖框架维护的层级路径；重命名、换父节点及保存重开保持引用。

预览不保存场景。编辑配置前先结束预览，避免展示值与编辑值混在一起。

相关：[入门与实例方式](mvvm.md) · [API](../api/mvvm.md)
