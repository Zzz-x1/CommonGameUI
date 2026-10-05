using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Unity;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Unity;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace StarCourt.UnityDemo
{
    public sealed class CompareArgs
    {
        public CompareArgs(int item, int position) { Item = item; Position = position; }
        public int Item { get; }
        public int Position { get; }
    }
}
