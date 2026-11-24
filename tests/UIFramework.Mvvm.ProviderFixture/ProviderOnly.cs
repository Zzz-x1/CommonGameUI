using UIFramework.Mvvm;

namespace ProviderFixture;

// The consumer references this project, but never references a type from it.
public static class Currency
{
    [Converter] public static string Display(int value) => value + " gold";
}
