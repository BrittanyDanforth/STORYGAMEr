#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SignalDebt.Editor
{
    [InitializeOnLoad]
    public static class SignalDebtSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Apartment.unity";
        private const string Marker = "Assets/Scenes/.signal-debt-built";
        private const string PlanPath = "Assets/Art/Apartment/apartment_collision_plan.json";
        private const string BodyPath = "Assets/Art/Characters/PlayerBody.fbx";
        private static readonly string[] ModelPaths =
        {
            "Assets/Art/Apartment/Apartment.fbx", "Assets/Art/Apartment/SignalDebt_Apartment.fbx"
        };

        [Serializable] private sealed class Anchor { public float[] unity; }
        [Serializable] private sealed class Anchors
        {
            public Anchor PlayerSpawn, DeskSeat, LaptopFocus, DoorHinge, DoorInteract, BedInteract, BillInteract;
        }
        [Serializable] private sealed class CollisionPart
        {
            public string name;
            public bool dynamic;
            public float[] center_unity;
            public float[] size_unity;
        }
        [Serializable] private sealed class CollisionPlan { public Anchors anchors; public CollisionPart[] colliders; }

        static SignalDebtSceneBuilder() { EditorApplication.delayCall += TryAutoBuild; }

        private static void TryAutoBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += TryAutoBuild; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(Marker) || File.Exists(ScenePath)) return;
            if (!ModelPaths.Any(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)) return;
            // Do not replace a user's existing or modified editing scene automatically.
            Scene current = SceneManager.GetActiveScene();
            if (current.isDirty || !string.IsNullOrEmpty(current.path)) return;
            Build(false);
        }

        [MenuItem("Signal Debt/Build Apartment Scene")]
        public static void BuildApartmentScene() { Build(true); }

        private static void Build(bool interactive)
        {
            if (interactive && !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ConfigureInput();
            Directory.CreateDirectory("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject imported = ModelPaths.Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path)).FirstOrDefault(item => item != null);
            GameObject room = imported != null ? (GameObject)PrefabUtility.InstantiatePrefab(imported) : BuildFallbackRoom();
            room.name = imported != null ? "Apartment · Blender source" : "Apartment · procedural fallback";
            FixUnsupportedMaterials(room);
            CollisionPlan plan = ReadPlan();
            Matrix4x4 mapping = MapPlanToImportedAnchors(room.transform, plan);
            if (plan != null && imported != null) AddPlanColliders(room.transform, plan, mapping);
            else AddMeshColliders(room.transform);

            var runtimeObject = new GameObject("Signal Debt Runtime");
            var runtime = runtimeObject.AddComponent<SignalDebtBootstrap>();
            runtime.playerSpawn = GetAnchor(room.transform, "PlayerSpawn", mapping.MultiplyPoint3x4(new Vector3(0f, 0.035f, -1.8f)));
            runtime.deskSeat = GetAnchor(room.transform, "DeskSeat", mapping.MultiplyPoint3x4(new Vector3(-1.8f, 0.025f, 0.85f)));
            runtime.laptopFocus = GetAnchor(room.transform, "LaptopFocus", mapping.MultiplyPoint3x4(new Vector3(-1.8f, 1.04f, 1.72f)));
            FacePoint(runtime.playerSpawn, runtime.laptopFocus.position);
            FacePoint(runtime.deskSeat, runtime.laptopFocus.position);
            // Authored from this apartment: key tops ~0.856 m, wrist centers ~0.884 m.
            Vector3 keyboardWristCenter = runtime.laptopFocus.position - runtime.deskSeat.forward * 0.26f - Vector3.up * 0.111f;
            GetAnchor(room.transform, "KeyboardWristLeft", keyboardWristCenter - runtime.deskSeat.right * 0.105f);
            GetAnchor(room.transform, "KeyboardWristRight", keyboardWristCenter + runtime.deskSeat.right * 0.105f);
            runtime.playerBodyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath);
            Type relay = typeof(SignalDebtBootstrap).Assembly.GetType("SignalDebt.SignalDebtNpcClient");
            if (relay != null && typeof(MonoBehaviour).IsAssignableFrom(relay)) runtimeObject.AddComponent(relay);

            Transform doorHinge = Find(room.transform, "DoorHinge");
            Transform doorAnchor = GetAnchor(room.transform, "DoorInteract", new Vector3(1.6f, 1.1f, -2.35f));
            Transform bill = GetAnchor(room.transform, "BillInteract", new Vector3(-0.5f, 1.35f, 2.42f));
            Transform bed = GetAnchor(room.transform, "BedInteract", new Vector3(1.75f, 0.8f, 1f));
            Interaction(runtime.laptopFocus, SignalDebtInteraction.Desk, "E  Sit at laptop", new Vector3(0.66f, 0.5f, 0.8f));
            Interaction(bill, SignalDebtInteraction.Bills, "E  Read rent notice", new Vector3(0.58f, 0.55f, 0.3f));
            Interaction(bed, SignalDebtInteraction.Bed, "E  Rest / review tomorrow", new Vector3(0.5f, 0.55f, 0.5f));
            var doorAction = Interaction(doorAnchor, SignalDebtInteraction.Door, "E  Open apartment door", new Vector3(0.7f, 0.65f, 0.5f));
            doorAction.doorPivot = doorHinge;
            doorAction.openDegrees = -92f;
            // Door surface is also usable after the fixed interaction marker is left behind.
            if (doorHinge != null)
            {
                // The hinge is ancestor of both the imported leaf and its manifest collider.
                var proxy = doorHinge.gameObject.AddComponent<SignalDebtDoorProxy>();
                proxy.controller = doorAction;
            }
            AddLighting(mapping);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.12f, 0.16f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            File.WriteAllText(Marker, "Generated first-room scene. Rebuild explicitly using Signal Debt/Build Apartment Scene.\n");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = runtimeObject;
            Debug.Log("Signal Debt: Apartment scene created. Press Play. Imported model: " + (imported != null) + ". This build step is not a Play Mode validation.");
        }

        private static void FacePoint(Transform anchor, Vector3 point)
        {
            Vector3 direction = point - anchor.position; direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f) anchor.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private static CollisionPlan ReadPlan()
        {
            if (!File.Exists(PlanPath)) return null;
            try { return JsonUtility.FromJson<CollisionPlan>(File.ReadAllText(PlanPath)); }
            catch (Exception ex) { Debug.LogWarning("Collision plan could not load: " + ex.Message); return null; }
        }

        private static Vector3 V(float[] values)
        { return values != null && values.Length == 3 ? new Vector3(values[0], values[1], values[2]) : Vector3.zero; }

        private static Matrix4x4 Basis(Vector3 origin, Vector3 b, Vector3 c, Vector3 d)
        {
            var matrix = Matrix4x4.identity;
            matrix.SetColumn(0, new Vector4(b.x - origin.x, b.y - origin.y, b.z - origin.z, 0f));
            matrix.SetColumn(1, new Vector4(c.x - origin.x, c.y - origin.y, c.z - origin.z, 0f));
            matrix.SetColumn(2, new Vector4(d.x - origin.x, d.y - origin.y, d.z - origin.z, 0f));
            matrix.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
            return matrix;
        }

        private static Matrix4x4 MapPlanToImportedAnchors(Transform model, CollisionPlan plan)
        {
            if (plan == null || plan.anchors == null) return Matrix4x4.identity;
            Transform a = Find(model, "PlayerSpawn"), b = Find(model, "DeskSeat"), c = Find(model, "LaptopFocus"), d = Find(model, "DoorHinge");
            Anchors expected = plan.anchors;
            if (a == null || b == null || c == null || d == null || expected.PlayerSpawn == null || expected.DeskSeat == null || expected.LaptopFocus == null || expected.DoorHinge == null)
                return Matrix4x4.identity;
            Matrix4x4 source = Basis(V(expected.PlayerSpawn.unity), V(expected.DeskSeat.unity), V(expected.LaptopFocus.unity), V(expected.DoorHinge.unity));
            if (Mathf.Abs(source.determinant) < 0.0001f) return Matrix4x4.identity;
            return Basis(a.position, b.position, c.position, d.position) * source.inverse;
        }

        private static void AddPlanColliders(Transform model, CollisionPlan plan, Matrix4x4 map)
        {
            if (plan.colliders == null || plan.colliders.Length == 0) { AddMeshColliders(model); return; }
            var root = new GameObject("Simple collision from Blender manifest").transform;
            Transform hinge = Find(model, "DoorHinge");
            foreach (CollisionPart part in plan.colliders)
            {
                if (part.center_unity == null || part.size_unity == null) continue;
                GameObject node = new GameObject(part.name + " Collider");
                Vector3 right = map.MultiplyVector(Vector3.right), up = map.MultiplyVector(Vector3.up), forward = map.MultiplyVector(Vector3.forward);
                node.transform.position = map.MultiplyPoint3x4(V(part.center_unity));
                node.transform.rotation = Quaternion.LookRotation(forward.normalized, up.normalized);
                node.transform.SetParent(part.dynamic && hinge != null ? hinge : root, true);
                BoxCollider collider = node.AddComponent<BoxCollider>();
                Vector3 dimensions = V(part.size_unity);
                // World orientation is explicitly established; import wrapper axes cannot tilt this box.
                Vector3 worldSize = new Vector3(dimensions.x * right.magnitude, dimensions.y * up.magnitude, dimensions.z * forward.magnitude);
                Vector3 scale = node.transform.lossyScale;
                collider.size = new Vector3(worldSize.x / Mathf.Abs(scale.x), worldSize.y / Mathf.Abs(scale.y), worldSize.z / Mathf.Abs(scale.z));
                if (part.name == "Laptop" || part.name == "Bed")
                {
                    // The visible solid surface must work even if it occludes an anchor trigger.
                    var interaction = node.AddComponent<SignalDebtInteractable>();
                    interaction.interaction = part.name == "Laptop" ? SignalDebtInteraction.Desk : SignalDebtInteraction.Bed;
                    interaction.prompt = part.name == "Laptop" ? "E  Sit at laptop" : "E  Rest / review tomorrow";
                }
                node.isStatic = !part.dynamic;
            }
        }

        private static void AddMeshColliders(Transform root)
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                string lower = filter.name.ToLowerInvariant();
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null || lower.Contains("glass") || lower.Contains("light")) continue;
                if (lower.Contains("doorleaf"))
                {
                    BoxCollider door = filter.gameObject.AddComponent<BoxCollider>();
                    door.center = filter.sharedMesh.bounds.center; door.size = filter.sharedMesh.bounds.size;
                }
                else { filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh; filter.gameObject.isStatic = true; }
            }
        }

        private static SignalDebtInteractable Interaction(Transform anchor, SignalDebtInteraction kind, string prompt, Vector3 size)
        {
            GameObject node = new GameObject(kind + " focus volume");
            node.transform.position = anchor.position;
            node.transform.SetParent(anchor, true);
            // Keep trigger axes in world space even when FBX anchor axes retain a conversion rotation.
            node.transform.rotation = Quaternion.identity;
            var box = node.AddComponent<BoxCollider>(); box.isTrigger = true;
            Vector3 scale = node.transform.lossyScale;
            box.size = new Vector3(size.x / Mathf.Abs(scale.x), size.y / Mathf.Abs(scale.y), size.z / Mathf.Abs(scale.z));
            var action = node.AddComponent<SignalDebtInteractable>(); action.interaction = kind; action.prompt = prompt;
            return action;
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static Transform GetAnchor(Transform root, string name, Vector3 fallback)
        {
            Transform result = Find(root, name);
            if (result != null) return result;
            result = new GameObject(name).transform; result.SetParent(root, true); result.position = fallback;
            return result;
        }

        private static void AddLighting(Matrix4x4 map)
        {
            var key = new GameObject("Soft window moonlight", typeof(Light));
            key.transform.rotation = Quaternion.Euler(36f, -48f, 0f);
            Light sun = key.GetComponent<Light>(); sun.type = LightType.Directional;
            sun.color = new Color(0.59f, 0.72f, 0.9f); sun.intensity = 0.45f; sun.shadows = LightShadows.Soft;
            var lamp = new GameObject("Desk practical glow", typeof(Light));
            lamp.transform.position = map.MultiplyPoint3x4(new Vector3(2.55f, 1.65f, 2.12f));
            Light warm = lamp.GetComponent<Light>(); warm.type = LightType.Point;
            warm.color = new Color(1f, 0.66f, 0.37f); warm.range = 4.2f; warm.intensity = 1.8f; warm.shadows = LightShadows.Soft;
            var ceiling = new GameObject("Room bounce light", typeof(Light));
            ceiling.transform.position = map.MultiplyPoint3x4(new Vector3(0f, 2.45f, 0f));
            Light fill = ceiling.GetComponent<Light>(); fill.type = LightType.Point;
            fill.color = new Color(0.43f, 0.53f, 0.7f); fill.range = 5.5f; fill.intensity = 0.75f;
        }

        private static void FixUnsupportedMaterials(GameObject model)
        {
            Directory.CreateDirectory("Assets/Art/GeneratedMaterials");
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material old = materials[i];
                    bool wrongPipeline = old != null && old.shader != null && !srp && old.shader.name.Contains("Universal Render Pipeline");
                    if (old != null && old.shader != null && old.shader.isSupported && !old.shader.name.Contains("InternalErrorShader") && !wrongPipeline) continue;
                    Color color = old != null && old.HasProperty("_Color") ? old.color : new Color(0.28f, 0.33f, 0.38f);
                    string name = "Fallback_" + renderer.GetInstanceID() + "_" + i;
                    Material replacement = SignalDebtPlayer.MakeMaterial(name, color);
                    string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Art/GeneratedMaterials/" + name + ".mat");
                    AssetDatabase.CreateAsset(replacement, path); materials[i] = replacement;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static GameObject BuildFallbackRoom()
        {
            var root = new GameObject("Fallback apartment");
            Material dark = SaveMaterial("Fallback room", new Color(0.17f, 0.20f, 0.24f));
            Material wood = SaveMaterial("Fallback wood", new Color(0.26f, 0.16f, 0.10f));
            Cube("Floor", root.transform, new Vector3(0f, -0.08f, 0f), new Vector3(6.4f, 0.16f, 5.2f), dark);
            Cube("North wall", root.transform, new Vector3(0f, 1.4f, 2.65f), new Vector3(6.4f, 2.8f, 0.15f), dark);
            Cube("West wall", root.transform, new Vector3(-3.25f, 1.4f, 0f), new Vector3(0.15f, 2.8f, 5.4f), dark);
            Cube("East wall", root.transform, new Vector3(3.25f, 1.4f, 0f), new Vector3(0.15f, 2.8f, 5.4f), dark);
            Cube("Desk", root.transform, new Vector3(-1.8f, 0.78f, 1.6f), new Vector3(1.8f, 0.12f, 0.8f), wood);
            Cube("Laptop screen", root.transform, new Vector3(-1.8f, 1.04f, 1.78f), new Vector3(0.5f, 0.32f, 0.035f), dark);
            Cube("Bed", root.transform, new Vector3(2.0f, 0.32f, 1.3f), new Vector3(1.4f, 0.55f, 2.1f), dark);
            Transform hinge = GetAnchor(root.transform, "DoorHinge", new Vector3(1.1f, 0f, -2.5f));
            Cube("DoorLeaf", hinge, new Vector3(0.48f, 1.05f, 0f), new Vector3(0.96f, 2.1f, 0.06f), wood);
            return root;
        }

        private static Material SaveMaterial(string name, Color color)
        {
            Directory.CreateDirectory("Assets/Art/GeneratedMaterials");
            string path = "Assets/Art/GeneratedMaterials/" + name.Replace(' ', '_') + ".mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null) { result = SignalDebtPlayer.MakeMaterial(name, color); AssetDatabase.CreateAsset(result, path); }
            return result;
        }

        private static void Cube(string name, Transform parent, Vector3 local, Vector3 size, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name;
            cube.transform.SetParent(parent, false); cube.transform.localPosition = local; cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }

        internal static void ConfigureInput()
        {
            UnityEngine.Object asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset").FirstOrDefault();
            if (asset == null) { Debug.LogWarning("InputManager.asset not found. See Documentation/unity_notes.md for required axes."); return; }
            var serialized = new SerializedObject(asset);
            SerializedProperty axes = serialized.FindProperty("m_Axes");
            if (axes == null) return;
            Axis(axes, "Horizontal", "a", "d", "left", "right", 0, 0, 3f, 3f, true);
            Axis(axes, "Vertical", "s", "w", "down", "up", 0, 0, 3f, 3f, true);
            Axis(axes, "Mouse X", "", "", "", "", 1, 0, 0f, 0.1f, false);
            Axis(axes, "Mouse Y", "", "", "", "", 1, 1, 0f, 0.1f, false);
            Axis(axes, "Mouse ScrollWheel", "", "", "", "", 1, 2, 0f, 0.1f, false);
            Axis(axes, "Submit", "", "return", "", "space", 0, 0, 1000f, 1000f, false);
            Axis(axes, "Cancel", "", "escape", "", "", 0, 0, 1000f, 1000f, false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Axis(SerializedProperty axes, string name, string negative, string positive, string altNegative, string altPositive, int type, int axis, float gravity, float sensitivity, bool snap)
        {
            SerializedProperty item = null;
            for (int i = 0; i < axes.arraySize; i++)
                if (axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue == name) { item = axes.GetArrayElementAtIndex(i); break; }
            if (item == null) { int at = axes.arraySize; axes.InsertArrayElementAtIndex(at); item = axes.GetArrayElementAtIndex(at); }
            item.FindPropertyRelative("m_Name").stringValue = name;
            item.FindPropertyRelative("negativeButton").stringValue = negative; item.FindPropertyRelative("positiveButton").stringValue = positive;
            item.FindPropertyRelative("altNegativeButton").stringValue = altNegative; item.FindPropertyRelative("altPositiveButton").stringValue = altPositive;
            item.FindPropertyRelative("gravity").floatValue = gravity; item.FindPropertyRelative("dead").floatValue = 0.001f;
            item.FindPropertyRelative("sensitivity").floatValue = sensitivity; item.FindPropertyRelative("snap").boolValue = snap;
            item.FindPropertyRelative("invert").boolValue = false; item.FindPropertyRelative("type").intValue = type;
            item.FindPropertyRelative("axis").intValue = axis; item.FindPropertyRelative("joyNum").intValue = 0;
        }
    }
}
#endif
