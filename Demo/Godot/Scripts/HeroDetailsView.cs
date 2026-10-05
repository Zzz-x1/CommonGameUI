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
[UIResource("res://Scenes/Client/HeroDetails.tscn")]
public partial class HeroDetailsView : EngineUI.UINode<int>
{
    private DemoApp App => DemoApp.Instance;
    private Control Root => ((EngineUI.UIElement)Element!).Control;
    private ClientHeroVm _hero = null!;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = ClientTransitions.Hero();
        _hero = this.CreateViewModel<ClientHeroVm>("Hero");
        this.Bind("Hero.Power", Root.GetNode<Label>("Stats").TextTarget(), new BindingOptions { Convert = value => Stats((int)value!) });
        Root.GetNode<Button>("Train").Pressed += Train;
        Root.GetNode<Button>("Equip").Pressed += Equip;
        Root.GetNode<Button>("Unequip").Pressed += Unequip;
        Refresh();
        return Task.CompletedTask;
    }

    private void Train() => App.Track(TrainAsync());
    public Task TrainAsync() => App.RunAsync(this, TrainCoreAsync);
    private async Task TrainCoreAsync()
    {
        var previous = _hero.Power;
        this.PauseBinding(nameof(ClientHeroVm.Power));
        App.Store.Train(Parameters);
        Refresh();
        var duration = App.Store.State.FullMotion ? .55 : .1;
        var started = Time.GetTicksMsec();
        double elapsed = 0;
        while (elapsed < duration)
        {
            await ((EngineUI.UIHost)Context.Host).NextFrameAsync();
            Lifetime.ThrowIfCancellationRequested();
            elapsed = (Time.GetTicksMsec() - started) / 1000.0;
            var progress = Math.Min(1, elapsed / duration);
            Root.GetNode<Label>("Stats").Text = Stats((int)Math.Round(previous + (_hero.Power - previous) * (1 - Math.Pow(1 - progress, 3))));
        }
        this.ResumeBinding(nameof(ClientHeroVm.Power));
        await App.Modals.OpenAsync<TrainingView>(new PresentationArgs($"{Catalog.Heroes[Parameters].Name}\n等级提升至 Lv.{App.Store.State.Levels[Parameters]}\n战力 {App.Store.State.Power(Parameters)}", Parameters));
    }
    private string Stats(int power) => $"Lv.{App.Store.State.Levels[Parameters]}       战力 {power}";

    private void Equip() => App.Track(App.ModalAsync(new ModalArgs("Equip", Parameters)));
    private void Unequip()
    {
        App.Store.Equip(Parameters, "");
        Refresh();
    }

    protected override void OnActivated(StateChangeContext context) => Refresh();
    public void Refresh()
    {
        var s = App.Store.State;
        var hero = Catalog.Heroes[Parameters];
        Root.GetNode<TextureRect>("Portrait").Texture = GD.Load<Texture2D>($"res://Assets/Client/hero{Parameters}.png");
        Root.GetNode<Label>("Name").Text = hero.Name + " · " + hero.Role;
        Root.GetNode<Label>("Story").Text = hero.Story;
        _hero.Power = s.Power(Parameters);
        Root.GetNode<Label>("Equipment").Text = "当前装备：" + (s.Equipment[Parameters] == "" ? "未装备" : Catalog.Item(s.Equipment[Parameters]).Name);
        Root.GetNode<Button>("Unequip").Disabled = s.Equipment[Parameters] == "";
        Root.GetNode<Button>("Train").Disabled = s.Gold < 100 || s.Inventory["mat6"] < 1 || s.Levels[Parameters] >= 60;
    }

}
}
