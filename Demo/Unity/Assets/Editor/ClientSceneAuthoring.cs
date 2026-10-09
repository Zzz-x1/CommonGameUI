using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIFramework.Mvvm.Unity;

namespace StarCourt.UnityDemo.Editor
{
    // Authoring only: runtime uses saved scene/Prefab/AnimationClip assets.
    public static class ClientSceneAuthoring
    {
        private static Font _font;
        private static readonly Color Ink = new Color(.15f, .25f, .29f);
        private static readonly Color Paper = new Color(.96f, .95f, .89f);
        private static readonly Color Gold = new Color(.79f, .66f, .34f);
        private static readonly Color Teal = new Color(.28f, .46f, .44f);
        public static void SetResolution(int width, int height)
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSizeType", true);
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize", true),
                new object[] { Enum.Parse(sizeType, "FixedResolution"), width, height, "StarCourt " + width + "×" + height });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            var total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var viewType = assembly.GetType("UnityEditor.GameView", true);
            var view = EditorWindow.GetWindow(viewType, false, "Game", false);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, total - 1);
            view.Repaint();
        }
        public static void PrepareGlobalViewModels()
        {
            const string folder = "Assets/Resources/UIFramework/Mvvm";
            if (!AssetDatabase.IsValidFolder("Assets/Resources/UIFramework")) AssetDatabase.CreateFolder("Assets/Resources", "UIFramework");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Resources/UIFramework", "Mvvm");
            const string path = folder + "/Client.asset";
            var player = AssetDatabase.LoadAssetAtPath<ClientPlayerVm>("Assets/Resources/MVVM/Player.asset");
            if (!player) throw new InvalidOperationException("Player VM template is missing.");
            var catalog = AssetDatabase.LoadAssetAtPath<GlobalViewModelCatalog>(path);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<GlobalViewModelCatalog>(); AssetDatabase.CreateAsset(catalog, path); }
            catalog.ViewModels.Clear(); catalog.ViewModels.Add(player);
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
        }
        public static void Build()
        {
            PrepareGlobalViewModels();
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring assets.");
            Directory.CreateDirectory("Assets/Resources/UI"); Directory.CreateDirectory("Assets/Scenes"); Directory.CreateDirectory("Assets/Animations");
            AssetDatabase.Refresh();
            _font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansSC.otf");
            if (!_font) throw new FileNotFoundException("Noto Chinese font must be imported.");
            foreach (var name in new[] { "lobby", "event", "hero0", "hero1", "hero2", "hero3", "hero4", "hero5" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Art/" + name + ".png");
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 2048; importer.SaveAndReimport();
            }
            Save("Login", Login()); Save("Lobby", Lobby()); Save("Inventory", Inventory()); Save("Characters", Characters());
            Save("Item", Item()); Save("Hero", Hero()); Save("Shop", Shop()); Save("Team", Team()); Save("Quests", Quests());
            Save("Settings", Settings()); Save("Graphics", Graphics()); Save("Confirm", Confirm()); Save("Purchase", Purchase());
            Save("Reward", Reward()); Save("Compare", Compare());
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvasGo = new GameObject("StarCourtCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)canvasGo.transform;
            var top = Box("Header", root, 0, 0, 1600, 80, Paper);
            Label("Brand", top, "星庭远征 · 晨星庭院", 38, 12, 620, 52, 30);
            var wallet = Label("Wallet", top, "", 720, 12, 840, 52, 26);
            var pages = Stretch("PageMount", root); pages.offsetMin = new Vector2(0, 85); pages.offsetMax = new Vector2(0, -90);
            var navigation = Stretch("Navigation", root); navigation.gameObject.AddComponent<CanvasGroup>();
            var navPaper = Box("Paper", navigation, 0, 815, 1600, 85, Paper);
            var routes = new[] { "Home", "Characters", "Inventory", "Team", "Shop", "Quests", "Settings" };
            var names = new[] { "大厅", "角色", "背包", "编队", "商店", "任务", "设置" };
            for (int i = 0; i < routes.Length; i++) Button(routes[i], navigation, names[i], 25 + i * 223, 828, 210, 54);
            var floating = Stretch("WindowMount", root); var modals = Stretch("ModalMount", root);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
            var app = new GameObject("Client").AddComponent<UnityClient>();
            app.Canvas = canvas; app.Events = events; app.PageMount = pages; app.WindowMount = floating; app.ModalMount = modals;
            app.Wallet = wallet; app.Navigation = navigation.gameObject;
            var camera = new GameObject("MainCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .1f, .15f); camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            var clientRoot = new GameObject("StarCourtClient");
            foreach (var child in new[] { canvasGo, events.gameObject, app.gameObject, camera.gameObject }) child.transform.SetParent(clientRoot.transform, false);
            PrefabUtility.SaveAsPrefabAsset(clientRoot, "Assets/Resources/UI/ClientRoot.prefab");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Client.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Client.unity", true) };
            PlayerSettings.companyName = "UIFramework"; PlayerSettings.productName = "StarCourt Unity";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            ClientTimelineAuthoring.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("UNITY CLIENT ASSETS AUTHORED");
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform;
            if (parent) r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height);
            return r;
        }
        private static RectTransform Stretch(string name, Transform parent)
        {
            var r = Rect(name, parent, 0, 0, 1600, 900);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }
        private static RectTransform Root(string name)
        {
            var r = Stretch(name, null); r.gameObject.AddComponent<CanvasGroup>(); return r;
        }
        private static RectTransform Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var r = Rect(name, parent, x, y, w, h); var image = r.gameObject.AddComponent<Image>(); image.color = color;
            image.raycastTarget = false; return r;
        }
        private static Text Label(string name, Transform parent, string value, float x, float y, float w, float h, int size = 26)
        {
            var r = Rect(name, parent, x, y, w, h); var text = r.gameObject.AddComponent<Text>();
            text.font = _font; text.text = value; text.fontSize = size; text.color = Ink;
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            return text;
        }
        private static Button Button(string name, Transform parent, string title, float x, float y, float w, float h)
        {
            var r = Box(name, parent, x, y, w, h, new Color(.91f, .92f, .85f)); var image = r.GetComponent<Image>(); image.raycastTarget = true;
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1, .93f, .73f); colors.pressedColor = new Color(.76f, .82f, .73f); button.colors = colors;
            var text = Label("Text", r, title, 16, 5, w - 32, h - 10, 24); text.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        private static void Art(string name, Transform parent, string asset, float x, float y, float w, float h)
        {
            var r = Rect(name, parent, x, y, w, h); var image = r.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/" + asset + ".png"); image.preserveAspect = true; image.raycastTarget = false;
            r.gameObject.AddComponent<CanvasGroup>();
        }
        private static void Background(Transform parent)
        {
            var crop = Rect("Background", parent, 0, 0, 1600, 725); crop.gameObject.AddComponent<RectMask2D>();
            var image = Stretch("Art", crop).gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/lobby.png"); image.raycastTarget = false;
            var fit = image.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
        }
        private static void Dropdown(string name, Transform parent, string[] values, float x, float y, float width = 460)
        {
            var root = Box(name, parent, x, y, width, 52, new Color(.87f, .9f, .84f)); root.GetComponent<Image>().raycastTarget = true;
            var dd = root.gameObject.AddComponent<Dropdown>(); dd.targetGraphic = root.GetComponent<Image>();
            dd.captionText = Label("Label", root, "", 16, 0, width - 32, 52, 24);
            var template = Box("Template", root, 0, 52, width, 260, Paper); template.pivot = new Vector2(0, 1);
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var viewport = Stretch("Viewport", template); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 0, width, 48); scroll.viewport = viewport; scroll.content = content;
            var item = Box("Item", content, 0, 0, width, 48, Paper); item.GetComponent<Image>().raycastTarget = true;
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<Image>();
            var check = Box("Check", item, 5, 15, 14, 14, Teal); toggle.graphic = check.GetComponent<Image>();
            dd.itemText = Label("ItemLabel", item, "", 28, 0, width - 40, 48, 22);
            dd.template = template; dd.options = values.Select(v => new Dropdown.OptionData(v)).ToList();
            template.gameObject.SetActive(false); dd.RefreshShownValue();
        }
        private static RectTransform Page(string name, string title)
        {
            var root = Root(name); var paper = Stretch("Paper", root); paper.gameObject.AddComponent<Image>().color = Paper; paper.GetComponent<Image>().raycastTarget = false;
            Label("Title", root, title, 60, 18, 1480, 56, 38); Box("Accent", root, 60, 80, 1480, 2, Gold); return root;
        }
        private static RectTransform Modal(string name, string title, float width = 720, float height = 540)
        {
            var root = Root(name); var panel = Box("Panel", root, 800 - width / 2, 450 - height / 2, width, height, Paper);
            panel.gameObject.AddComponent<CanvasGroup>(); panel.GetComponent<Image>().raycastTarget = true;
            Label("Title", panel, title, 40, 22, width - 80, 65, 32); Box("Accent", panel, 40, 92, width - 80, 2, Gold);
            return root;
        }
        private static RectTransform Login()
        {
            var root = Root("Login"); Background(root);
            var panel = Box("Panel", root, 90, 120, 640, 485, Paper);
            Label("Title", panel, "星庭远征", 50, 30, 540, 90, 60);
            Label("Text", panel, "本地旅途档案\n同行者与补给已准备就绪\n\n欢迎返回晨星庭院", 50, 145, 540, 180, 28);
            Button("Enter", panel, "继续启程  ›", 50, 375, 540, 64); return root;
        }
        private static RectTransform Lobby()
        {
            var root = Root("Lobby"); Background(root); Art("Hero", root, "hero0", 505, -20, 650, 760);
            var welcome = Box("Welcome", root, 48, 58, 465, 280, Paper); welcome.gameObject.AddComponent<CanvasGroup>();
            Label("Text", welcome, "", 28, 25, 410, 230, 29);
            var entries = Rect("Entries", root, 1165, 75, 390, 610); entries.gameObject.AddComponent<CanvasGroup>();
            var routes = new[] { "Characters", "Inventory", "Team", "Shop", "Quests", "Settings" };
            var labels = new[] { "同行者档案", "旅途背包", "远征编队", "星庭工坊", "旅途任务", "客户端设置" };
            for (int i = 0; i < routes.Length; i++) Button(routes[i], entries, labels[i], 0, i * 90, 370, 72);
            var clip = NewClip("LobbyEnter", 1.6f);
            Curve(clip, "Hero", typeof(RectTransform), "m_AnchoredPosition.x", 0, 760, .2f, 760, .85f, 505);
            Curve(clip, "Hero", typeof(CanvasGroup), "m_Alpha", 0, 0, .2f, 0, .9f, 1);
            Curve(clip, "Welcome", typeof(CanvasGroup), "m_Alpha", 0, 0, .45f, 0, 1.05f, 1);
            Curve(clip, "Entries", typeof(CanvasGroup), "m_Alpha", 0, 0, .8f, 0, 1.6f, 1);
            AddPlayer(root, clip, "LobbyExit"); return root;
        }
        private static RectTransform Inventory()
        {
            var root = Page("Inventory", "旅途背包"); var tabs = Rect("Tabs", root, 60, 102, 1480, 60);
            Button("装备", tabs, "装备", 0, 0, 220, 52); Button("材料", tabs, "材料", 240, 0, 220, 52);
            var pages = Rect("Pages", root, 60, 175, 1480, 510);
            var equipment = Stretch("装备", pages); var materials = Stretch("材料", pages);
            for (int i = 0; i < 8; i++) Button("C" + i, equipment, "", i % 4 * 370, i / 4 * 220, 340, 195);
            Label("Text", materials, "", 45, 30, 1300, 340, 34); return root;
        }
        private static RectTransform Characters()
        {
            var root = Page("Characters", "同行者档案"); var cards = Rect("Cards", root, 60, 110, 1480, 550);
            for (int i = 0; i < 6; i++)
            {
                var button = Button("C" + i, cards, "", i % 3 * 490, i / 3 * 270, 465, 245);
                Art("Portrait", button.transform, "hero" + i, 15, 0, 185, 240);
                var text = button.transform.Find("Text").GetComponent<Text>(); text.rectTransform.anchoredPosition = new Vector2(220, -20); text.rectTransform.sizeDelta = new Vector2(220, 180);
            }
            return root;
        }
        private static RectTransform Item()
        {
            var root = Page("Item", "装备详情"); var panel = Box("Panel", root, 200, 140, 1200, 480, new Color(.9f, .91f, .85f));
            Label("Title", panel, "", 55, 35, 1090, 60, 38); Label("Text", panel, "", 55, 120, 1090, 210, 32);
            Button("Compare", panel, "加入对比", 55, 365, 480, 65); Button("Back", panel, "返回背包", 610, 365, 535, 65); return root;
        }
        private static RectTransform Hero()
        {
            var root = Page("Hero", "同行者培养"); var portraits = Rect("Portraits", root, 110, 95, 680, 600);
            for (int i = 0; i < 6; i++) Art("Hero" + i, portraits, "hero" + i, 0, 0, 680, 600);
            var panel = Box("Panel", root, 830, 115, 650, 520, new Color(.9f, .91f, .85f));
            Label("Title", panel, "", 35, 25, 580, 60, 36); Label("Text", panel, "", 35, 110, 580, 140, 30);
            Dropdown("Equipment", panel, ClientStore.Items, 35, 270, 580);
            Button("Equip", panel, "装备", 35, 340, 280, 55); Button("Unequip", panel, "卸下", 335, 340, 280, 55);
            Button("Train", panel, "培养 · 300 金币 / 2 手记", 35, 425, 580, 60); return root;
        }
        private static RectTransform Shop()
        {
            var root = Page("Shop", "星庭工坊"); var cards = Rect("Cards", root, 60, 115, 1480, 520);
            for (int i = 0; i < 8; i++) Button("C" + i, cards, "", i % 4 * 370, i / 4 * 230, 340, 200);
            return root;
        }
        private static RectTransform Team()
        {
            var root = Page("Team", "远征编队"); var slots = Rect("Slots", root, 60, 150, 1480, 475);
            for (int i = 0; i < 4; i++)
            {
                var card = Box("S" + i, slots, i * 370, 0, 340, 440, new Color(.9f, .91f, .85f));
                Label("Text", card, "", 25, 25, 290, 140, 34);
                Dropdown("Hero", card, new[] { "空位" }.Concat(ClientStore.Heroes).ToArray(), 25, 200, 290);
                Button("Assign", card, "应用到此位置", 25, 330, 290, 65);
            }
            return root;
        }
        private static RectTransform Quests()
        {
            var root = Page("Quests", "旅途任务"); var panel = Box("Panel", root, 160, 125, 1280, 515, new Color(.9f, .91f, .85f));
            Label("Text", panel, "", 45, 25, 1190, 330, 32); Button("Claim", panel, "领取奖励", 45, 405, 1190, 65); return root;
        }
        private static RectTransform Settings()
        {
            var root = Modal("Settings", "客户端设置", 840, 620); var panel = root.Find("Panel");
            var tabs = Rect("Tabs", panel, 40, 110, 760, 55); Button("画面", tabs, "画面", 0, 0, 350, 52); Button("演出", tabs, "演出", 390, 0, 350, 52);
            var pages = Rect("Pages", panel, 40, 185, 760, 290); var graphics = Stretch("画面", pages); var effects = Stretch("演出", pages);
            Label("Text", graphics, "", 15, 0, 730, 145, 28); Button("Graphics", graphics, "打开画质设置", 15, 190, 730, 65);
            var toggleRoot = Box("Motion", effects, 15, 35, 730, 75, new Color(.88f, .91f, .85f)); toggleRoot.GetComponent<Image>().raycastTarget = true;
            var toggle = toggleRoot.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = toggleRoot.GetComponent<Image>();
            var mark = Box("Mark", toggleRoot, 18, 20, 35, 35, Teal); toggle.graphic = mark.GetComponent<Image>();
            Label("Text", toggleRoot, "完整演出与装饰动画", 78, 0, 625, 75, 28);
            Label("Text", effects, "关闭后使用简化入场，业务流程保持一致。", 15, 155, 730, 110, 25);
            Button("Defaults", panel, "恢复默认", 40, 525, 350, 60); Button("Close", panel, "关闭", 450, 525, 350, 60); return root;
        }
        private static RectTransform Graphics()
        {
            var root = Modal("Graphics", "画质设置", 720, 460); var panel = root.Find("Panel");
            Label("Text", panel, "选择本地画质档位。关闭后返回设置窗口。", 40, 130, 640, 90, 28);
            Dropdown("Quality", panel, new[] { "流畅", "均衡", "精致" }, 40, 240, 640); Button("Close", panel, "返回设置", 40, 340, 640, 65); return root;
        }
        private static RectTransform Confirm()
        {
            var root = Modal("Confirm", "请确认", 680, 430); var panel = root.Find("Panel");
            Label("Text", panel, "", 40, 120, 600, 150, 28); Button("No", panel, "取消", 40, 310, 280, 65); Button("Yes", panel, "确认", 360, 310, 280, 65); return root;
        }
        private static RectTransform Purchase()
        {
            var root = Modal("Purchase", "星庭补给", 820, 560); var panel = root.Find("Panel");
            Label("Text", panel, "", 40, 120, 740, 150, 29); Dropdown("Quantity", panel, Enumerable.Range(1, 10).Select(i => "数量 " + i).ToArray(), 40, 300, 740);
            Button("Close", panel, "返回商店", 40, 440, 350, 65); Button("Buy", panel, "确认购买", 430, 440, 350, 65); return root;
        }
        private static RectTransform Reward()
        {
            var root = Modal("Reward", "旅途的馈赠", 790, 570); var panel = root.Find("Panel");
            var glow = Box("Glow", panel, 175, 125, 440, 250, new Color(.88f, .74f, .4f, .18f)); glow.gameObject.AddComponent<CanvasGroup>();
            var card = Box("Card", panel, 115, 130, 560, 275, new Color(.92f, .93f, .85f)); card.gameObject.AddComponent<CanvasGroup>();
            Label("Text", panel, "", 140, 150, 510, 235, 31); Button("Close", panel, "收下", 40, 460, 710, 65);
            var clip = NewClip("RewardEnter", 1.5f);
            Curve(clip, "Panel", typeof(CanvasGroup), "m_Alpha", 0, 0, .25f, 1, 1.5f, 1);
            Curve(clip, "Panel/Glow", typeof(RectTransform), "m_LocalScale.x", 0, .2f, .7f, 1.4f, 1.5f, 1);
            Curve(clip, "Panel/Glow", typeof(RectTransform), "m_LocalScale.y", 0, .2f, .7f, 1.4f, 1.5f, 1);
            Curve(clip, "Panel/Glow", typeof(RectTransform), "localEulerAnglesRaw.z", 0, -30, 1.5f, 30);
            Curve(clip, "Panel/Card", typeof(CanvasGroup), "m_Alpha", 0, 0, .4f, 0, .9f, 1);
            Curve(clip, "Panel/Card", typeof(RectTransform), "m_AnchoredPosition.y", 0, -200, .4f, -200, 1.05f, -130);
            AddPlayer(root, clip, "RewardExit"); return root;
        }
        private static RectTransform Compare()
        {
            var root = Root("Compare"); var panel = Box("Panel", root, 825, 180, 345, 440, Paper); panel.GetComponent<Image>().raycastTarget = true;
            Label("Title", panel, "", 25, 25, 295, 70, 29); Label("Text", panel, "", 25, 130, 295, 160, 28);
            Button("Close", panel, "关闭对比", 25, 340, 295, 65); return root;
        }
        private static AnimationClip NewClip(string name, float duration)
        {
            var clip = new AnimationClip { name = name, legacy = true, frameRate = 60, wrapMode = WrapMode.Once };
            Curve(clip, "", typeof(CanvasGroup), "m_Alpha", 0, 1, duration, 1);
            return clip;
        }
        private static void Curve(AnimationClip clip, string path, Type type, string property, params float[] pairs)
        {
            var keys = new Keyframe[pairs.Length / 2]; for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(pairs[i * 2], pairs[i * 2 + 1]);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), new AnimationCurve(keys));
            EditorUtility.SetDirty(clip);
        }
        private static void AddPlayer(RectTransform root, AnimationClip enter, string exitName)
        {
            // Finish every track before importing/saving the persistent asset.
            AssetDatabase.CreateAsset(enter, "Assets/Animations/" + enter.name + ".anim");
            var exit = new AnimationClip { name = "exit", legacy = true, frameRate = 60, wrapMode = WrapMode.Once };
            Curve(exit, "", typeof(CanvasGroup), "m_Alpha", 0, 1, .2f, 0); AssetDatabase.CreateAsset(exit, "Assets/Animations/" + exitName + ".anim");
            var player = root.gameObject.AddComponent<Animation>(); player.playAutomatically = false; player.cullingType = AnimationCullingType.AlwaysAnimate;
            AnimationUtility.SetAnimationClips(player, new[] { enter, exit });
            player.clip = enter;
        }
        private static void Save(string name, RectTransform root)
        {
            root.gameObject.AddComponent(typeof(UnityClient).Assembly.GetType("StarCourt.UnityDemo." + name + "View", true));
            root.gameObject.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Resources/UI/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
    }
}
