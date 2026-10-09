namespace UIFramework.Mvvm.Unity
{
    /// <summary>Called once for a framework-created runtime SO instance, after initial data is prepared.</summary>
    public interface IViewModelInitialize
    {
        void OnInitialize();
    }
}
