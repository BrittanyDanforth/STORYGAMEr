#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalDebt.Editor
{
    public sealed class SignalDebtRigImporter : AssetPostprocessor
    {
        public override uint GetVersion() { return 3; }

        private bool IsCharacter { get { return assetPath.StartsWith("Assets/Art/Characters/", StringComparison.Ordinal); } }
        private bool IsAnimation { get { return assetPath.StartsWith("Assets/Art/Animations/", StringComparison.Ordinal); } }

        private void OnPreprocessModel()
        {
            if (!IsCharacter && !IsAnimation) return;
            var model = (ModelImporter)assetImporter;
            model.animationType = ModelImporterAnimationType.Generic;
            model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            model.optimizeGameObjects = false; // IK and review need named joints and markers.
            model.importNormals = ModelImporterNormals.Import; // Preserve shared cloth normals at renderer borders.
            model.importAnimation = IsAnimation;
            model.animationCompression = ModelImporterAnimationCompression.Off;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsAnimation) return;
            var model = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] takes = model.defaultClipAnimations;
            if (takes == null || takes.Length == 0) return;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            ModelImporterClipAnimation clip = takes[0];
            clip.name = name;
            // Preserve actual imported take frame limits instead of assuming whether FBX starts at frame 0 or 1.
            clip.loopTime = name == "Patrol_LowReadyIdle";
            clip.loopPose = clip.loopTime;
            clip.wrapMode = clip.loopTime ? WrapMode.Loop : WrapMode.ClampForever;
            clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true; clip.keepOriginalPositionY = true; clip.keepOriginalPositionXZ = true;
            clip.events = new AnimationEvent[0]; // Contact frames never dispatch gameplay actions.
            model.clipAnimations = new[] { clip };
        }
    }

    public static class SignalDebtAnimationBuilder
    {
        private const string DirectoryPath = "Assets/Art/Animations/";
        private static readonly string[] ClipNames = { "SWAT_DoorKick", "Patrol_LowReadyIdle", "Patrol_LowReadyToAim", "Patrol_AimToLowReady" };

        [MenuItem("Signal Debt/Build Animation Review Scene")]
        public static void BuildReviewScene()
        {
            foreach (string name in ClipNames)
            {
                string path = DirectoryPath + name + ".fbx";
                if (!File.Exists(path)) { Debug.LogError("Missing animation asset: " + path); return; }
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SignalDebtSceneBuilder.ConfigureInput();
            foreach (string name in ClipNames) AssetDatabase.ImportAsset(DirectoryPath + name + ".fbx", ImportAssetOptions.ForceUpdate);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var review = new GameObject("Animation Review — visual only").AddComponent<SignalDebtAnimationReview>();
            review.swat = MakeActor("SWAT / DOOR CONTACT", "SWAT_DoorKick", new Vector3(-1.55f, 0f, 0f), new[] { "SWAT_DoorKick" });
            review.patrol = MakeActor("PATROL / LOW READY & AIM", "Patrol_LowReadyIdle", new Vector3(1.55f, 0f, 0f),
                new[] { "Patrol_LowReadyIdle", "Patrol_LowReadyToAim", "Patrol_AimToLowReady" });
            var camera = new GameObject("Review Camera", typeof(Camera), typeof(AudioListener));
            review.reviewCamera = camera.GetComponent<Camera>();
            review.reviewCamera.fieldOfView = 44f; review.reviewCamera.nearClipPlane = 0.05f;
            review.reviewCamera.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
            review.reviewCamera.clearFlags = CameraClearFlags.SolidColor;
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1.9f, -6.5f);
            camera.transform.LookAt(new Vector3(0f, 1f, 0f));
            Cube("Review floor", new Vector3(0f, -0.05f, 0f), new Vector3(10f, 0.1f, 8f), new Color(0.12f, 0.15f, 0.18f));
            // A static contact reference; the clip never toggles any gameplay door.
            Color frameColor = new Color(0.22f, 0.36f, 0.40f);
            Cube("Contact reference left jamb", new Vector3(-1.98f, 1.02f, -0.80f), new Vector3(0.035f, 2.04f, 0.045f), frameColor);
            Cube("Contact reference right jamb", new Vector3(-1.12f, 1.02f, -0.80f), new Vector3(0.035f, 2.04f, 0.045f), frameColor);
            Cube("Contact reference header", new Vector3(-1.55f, 2.04f, -0.80f), new Vector3(0.895f, 0.035f, 0.045f), frameColor);
            var key = new GameObject("Studio key", typeof(Light));
            key.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            key.GetComponent<Light>().type = LightType.Directional; key.GetComponent<Light>().intensity = 1.1f;
            key.GetComponent<Light>().shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.43f);
            RenderSettings.skybox = null;
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/AnimationReview.unity");
            // Intentionally preserve apartment build settings and do not auto-build this scene.
            Selection.activeGameObject = review.gameObject;
            Debug.Log("Animation review scene created. Enter Play Mode to review clips; no Unity runtime validation is implied.");
        }

        private static SignalDebtAnimationReview.Actor MakeActor(string label, string modelName, Vector3 at, string[] clips)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(DirectoryPath + modelName + ".fbx");
            Transform wrapper = new GameObject(label).transform; wrapper.position = at;
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.transform.SetParent(wrapper, false);
            Transform origin = Find(model.transform, "BodyOrigin"), forward = Find(model.transform, "BodyForward"), up = Find(model.transform, "BodyUp");
            if (origin != null && forward != null && up != null)
            {
                Vector3 f = forward.position - origin.position, u = up.position - origin.position;
                if (f.sqrMagnitude > 0.01f && u.sqrMagnitude > 0.01f)
                    wrapper.rotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(Quaternion.LookRotation(f, u));
            }
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                wrapper.position += Vector3.up * (at.y - bounds.min.y);
            }
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
            return new SignalDebtAnimationReview.Actor
            {
                label = label, animator = animator,
                clips = clips.Select(name => AssetDatabase.LoadAllAssetsAtPath(DirectoryPath + name + ".fbx").OfType<AnimationClip>()
                    .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal) && clip.name == name)).ToArray()
            };
        }

        private static Transform Find(Transform root, string suffix)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == suffix || item.name.EndsWith("_" + suffix, StringComparison.Ordinal));
        }

        private static void Cube(string name, Vector3 at, Vector3 scale, Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name; cube.transform.position = at; cube.transform.localScale = scale;
            Directory.CreateDirectory("Assets/Art/GeneratedMaterials");
            string materialPath = "Assets/Art/GeneratedMaterials/Review_" + name.Replace(' ', '_') + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = SignalDebtPlayer.MakeMaterial(name, color);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            cube.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        }
    }
}
#endif
