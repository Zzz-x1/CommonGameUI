using UIFramework.Mvvm;

namespace MvvmWebSample
{
    public static class TextConversions
    {
        [Converter] public static string Gold(int value) => "Gold " + value;
    }
}
