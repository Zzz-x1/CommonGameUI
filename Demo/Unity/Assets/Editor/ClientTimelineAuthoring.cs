using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
namespace StarCourt.UnityDemo.Editor
{
    public static class ClientTimelineAuthoring
    {
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Timelines"); Directory.CreateDirectory("Assets/Resources/Tests"); AssetDatabase.Refresh();
            const string legacy = "Assets/Resources/Tests/LegacyLobby.prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(legacy) && !AssetDatabase.CopyAsset("Assets/Resources/UI/Lobby.prefab", legacy))
                throw new IOException("Could not create legacy Animation test fixture.");
            BuildView("Lobby", "LobbyEnter", "LobbyExit"); BuildView("Reward", "RewardEnter", "RewardExit");
            AssetDatabase.SaveAssets(); Debug.Log("Timeline Demo assets saved: Lobby and Reward, multi-track enter/exit.");
        }
        private static void BuildView(string view, string enter, string exit)
        {
            var path = "Assets/Resources/UI/" + view + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animation = root.GetComponent<Animation>(); if (animation) UnityEngine.Object.DestroyImmediate(animation);
                var previous = root.transform.Find("Transitions"); if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var folder = new GameObject("Transitions", typeof(RectTransform)); folder.transform.SetParent(root.transform, false);
                BuildDirector(root, folder.transform, "Enter", enter); BuildDirector(root, folder.transform, "Exit", exit);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void BuildDirector(GameObject root, Transform parent, string name, string clipName)
        {
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/" + clipName + ".anim");
            if (!source) throw new FileNotFoundException("Animation source not found: " + clipName);
            var path = "Assets/Timelines/" + clipName + ".playable";
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (!timeline) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); timeline.name = clipName; AssetDatabase.CreateAsset(timeline, path); }
            else
            {
                foreach (var track in timeline.GetRootTracks().ToArray()) timeline.DeleteTrack(track);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path).Where(asset => asset != timeline)) UnityEngine.Object.DestroyImmediate(asset, true);
            }
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = source.length;
            var go = new GameObject(name, typeof(RectTransform), typeof(PlayableDirector)); go.transform.SetParent(parent, false);
            var director = go.GetComponent<PlayableDirector>(); director.playOnAwake = false; director.extrapolationMode = DirectorWrapMode.Hold; director.playableAsset = timeline;
            foreach (var group in AnimationUtility.GetCurveBindings(source).GroupBy(binding => binding.path).OrderBy(group => group.Key))
            {
                var target = group.Key.Length == 0 ? root.transform : root.transform.Find(group.Key);
                if (!target) throw new InvalidOperationException("Timeline target not found: " + group.Key);
                var animator = target.GetComponent<Animator>(); if (!animator) animator = target.gameObject.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.applyRootMotion = false;
                var track = timeline.CreateTrack<AnimationTrack>(null, target.name);
                var data = new AnimationClip { name = target.name + " presentation", frameRate = 60 };
                foreach (var binding in group) AnimationUtility.SetEditorCurve(data, EditorCurveBinding.FloatCurve("", binding.type, binding.propertyName), AnimationUtility.GetEditorCurve(source, binding));
                AssetDatabase.AddObjectToAsset(data, timeline);
                var segment = track.CreateClip<AnimationPlayableAsset>(); ((AnimationPlayableAsset)segment.asset).clip = data;
                segment.duration = source.length; segment.displayName = target.name + " / " + name;
                director.SetGenericBinding(track, animator);
                EditorUtility.SetDirty(track);
            }
            EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(director);
        }
    }
}
