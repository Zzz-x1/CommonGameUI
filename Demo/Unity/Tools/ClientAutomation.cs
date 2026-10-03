using System.Threading.Tasks;
using StarCourt.UnityDemo;

public static class ClientAutomation
{
    public static Task Shutdown() => UnityClient.Instance.ShutdownAsync();
    public static Task Enter() => UnityClient.Instance.EnterAsync();
    public static Task Inventory() => UnityClient.Instance.OpenAsync("Inventory");
    public static Task Characters() => UnityClient.Instance.OpenAsync("Characters");
    public static Task Hero() => UnityClient.Instance.OpenHeroAsync(0);
    public static Task Item() => UnityClient.Instance.OpenItemAsync(0);
    public static Task Shop() => UnityClient.Instance.OpenAsync("Shop");
    public static Task Purchase() => UnityClient.Instance.OpenPurchaseAsync(0);
    public static Task Reward() => UnityClient.Instance.RewardAsync("晨星长剑 × 1\n旅途补给已收入背包");
    public static Task Team() => UnityClient.Instance.OpenAsync("Team");
    public static Task Quests() => UnityClient.Instance.OpenAsync("Quests");
    public static Task Settings() => UnityClient.Instance.OpenAsync("Settings");
    public static Task Back() => UnityClient.Instance.BackAsync();
    public static Task Home() => UnityClient.Instance.OpenAsync("Home");
}
