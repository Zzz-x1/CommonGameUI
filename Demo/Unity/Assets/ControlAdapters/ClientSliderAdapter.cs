using UIFramework.Mvvm;
using UnityEngine.UI;

namespace StarCourt.UnityDemo
{
    [ExtendControlAdapter(typeof(Slider))]
    [BindProperty(nameof(Slider.minValue))]
    internal static class ClientSliderAdapter { }
}
