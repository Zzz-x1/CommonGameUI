# MVVM 编辑器

在界面根节点添加 `MvvmBindings`。Unity 打开 **Window → UIFramework → View Bindings**；Godot 启用插件后使用底部 **View Bindings** 面板。

## 1. 添加 Viewmodels

添加具名来源，例如 `Player`、`Settings`、`Inventory`。选择 VM 类型、创建方式及可选模板资产。

- 独立详情选 **CreateInstance**。
- 业务传入对象选 **Manual**，在节点初始化调用 `SetViewModel`。
- 多个窗口使用同一资产时选 **SharedAsset**。

类型列表只显示标记为 `ViewModel` 的项目类型。资产选择原生 `ScriptableObject`／`Resource`；普通 C# 类使用类型配置或注册工厂。

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

## 3. 预览初始值

点击 **预览初始值**。预览使用独立 Runtime 和资产克隆，只刷新显示及列表，不执行命令、不反向写入原资产。结束后恢复控件原值。

Manual 来源需项目提供样本：

```csharp
PreviewSources.Register(() => new PlayerVm { Gold = 12000 });
```

未提供时明确提示“缺少预览来源”。样本是项目明确提供的数据，不会自动制造运行时状态。

## 保存

Unity 配置保存在组件中，随 Prefab／场景保存，支持 Undo 和 Prefab Override。Godot 配置使用场景内的 Resource，支持原生 Undo；保存场景后重开仍保留配置。

预览不保存场景。编辑配置前先结束预览，避免展示值与编辑值混在一起。

相关：[入门与实例方式](mvvm.md) · [API](../api/mvvm.md)
