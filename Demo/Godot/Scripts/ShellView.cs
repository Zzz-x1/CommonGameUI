using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineMvvm = global::UIFramework.Mvvm.Godot;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;

namespace HierarchyDemo
{
[UIResource("res://Scenes/Client/Shell.tscn")]
public partial class ShellView : ClientView
{
    public StackContainerNode Pages { get; private set; } = null!;
    public LobbyView Home { get; private set; } = null!;

    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        this.SetViewModel("Player", App.PlayerVm);
        this.Bind("Player.Wallet", Get<Label>("Top/Wallet").TextTarget());
        Pages = await this.CreateContainerAsync<StackContainerNode>("Pages", Get<Control>("PageMount"), token);
        Pages.ChildTransition = EngineUI.Transitions.SlideHorizontal(.24);
        foreach (var name in new[]
        {
            "Home",
            "Characters",
            "Inventory",
            "Team",
            "Stages",
            "Shop",
            "Tasks"
        })
            Wire("Bottom/" + name, () => App.OpenAsync(name));
        Wire("Top/Mail", () => App.OpenAsync("Mail"));
        Wire("Top/Back", App.BackAsync);
        Wire("Top/Credits", () => App.ModalAsync(new ModalArgs("Credits")));
        Wire("Top/Profile", () => App.ModalAsync(new ModalArgs("Profile")));
        Wire("Top/Settings", () => App.ModalAsync(new ModalArgs("Settings")));
        Listen(() => App.Store.Changed += Refresh, () => App.Store.Changed -= Refresh);
        Feedback();
        Refresh();
    }

    public async Task EnterLobbyAsync(CancellationToken token = default)
    {
        Home = await Pages.PushAsync<LobbyView>(cancellationToken: token);
    }

    public override void Refresh()
    {
        Get<Button>("Top/Profile").Text = Data.Name;
        Get<Button>("Top/Mail").Text = $"邮件 · {Enumerable.Range(0, 8).Count(i => !Data.ReadMail.Contains(i) && !Data.DeletedMail.Contains(i))}";
    }

    public void SetNavigation(bool enabled)
    {
        foreach (var b in Descendants(Get<Control>("Top")).Concat(Descendants(Get<Control>("Bottom"))).OfType<Button>())
            b.Disabled = !enabled;
    }
}
}
