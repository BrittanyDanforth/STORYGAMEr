#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalDebt.Editor
{
    // One measured environment import. This builder adds navigation geometry only.
    public static class SignalDebtNeighborhoodBuilder
    {
        private const string DirectoryPath = "Assets/Art/Neighborhood";
        private const string ModelPath = DirectoryPath + "/Neighborhood.fbx";
        private const string ManifestPath = DirectoryPath + "/neighborhood_collision_anchors.json";
        private const string GeneratedPath = DirectoryPath + "/Generated";
        private const float Tolerance = .015f, Radius = .23f, Height = 1.78f;
        private static readonly string[] ReferenceNames = { "Ref_Origin", "Ref_East", "Ref_North", "Ref_Up" };
        private static readonly Vector3[] ReferencePoints = { Vector3.zero, Vector3.right * 2f, Vector3.forward * 3f, Vector3.up * 4f };

        [Serializable] private sealed class Anchor { public string name, kind; public float[] position_design_unity; }
        [Serializable] private sealed class Aperture { public float[] floor_design_unity; public float clear_width, clear_height; }
        [Serializable] private sealed class Fixture
        {
            public string name;
            public float[] position_design_unity, rotation_delta_design_xyzw;
        }
        [Serializable] private sealed class Part
        {
            public string name, renderer_name, type;
            public bool visible, trigger;
            public float[] center_design_unity, size_unity, vertices_xyz;
            public int[] triangles;
        }
        [Serializable] private sealed class SurfaceMaterial
        {
            public string name, alpha_mode, base_color, normal, metallic_smoothness, emission;
            public float[] base_color_rgba, emission_rgb;
            public float metallic, smoothness, alpha_cutoff;
        }
        [Serializable] private sealed class TextureInfo
        {
            public string path, role;
            public bool srgb, normal_map;
            public float[] physical_tile_size_m;
        }
        [Serializable] private sealed class Daylight
        {
            public float[] forward_design_unity, color_srgb, ambient_sky_srgb, ambient_equator_srgb, ambient_ground_srgb;
            public float intensity, ambient_intensity;
        }
        [Serializable] private sealed class Manifest
        {
            public int schema_version;
            public string units, export_scope, immutable_apartment_sha256;
            public float[] apartment_source_to_design;
            public Anchor[] anchors;
            public Aperture aperture;
            public Fixture[] moved_apartment_fixtures;
            public Part[] colliders;
            public SurfaceMaterial[] materials;
            public TextureInfo[] textures;
            public Daylight daylight;
        }

        public static bool AddIfAvailable(GameObject apartment, Transform apartmentCollisionRoot,
            Matrix4x4 apartmentPlanToWorld, out Action configureDaylight)
        {
            configureDaylight = null;
            bool modelExists = File.Exists(ModelPath), manifestExists = File.Exists(ManifestPath);
            if (!modelExists && !manifestExists) return false;
            Require(modelExists && manifestExists, "The neighborhood FBX and manifest must be supplied together.");
            Require(apartment != null && apartmentCollisionRoot != null,
                "The neighborhood requires the original apartment and its measured collision plan.");
            Require(new FileInfo(ManifestPath).Length <= 8 * 1024 * 1024, "Neighborhood manifest is too large.");
            Manifest plan;
            try { plan = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)); }
            catch (Exception) { throw Failure("Neighborhood manifest is not valid JSON."); }
            ValidateManifest(plan);
            Matrix4x4 sourceToDesign = Matrix(plan.apartment_source_to_design);
            ValidateRigid(sourceToDesign, "apartment source basis");
            ValidateRigid(apartmentPlanToWorld, "actual apartment basis");
            Matrix4x4 designToWorld = apartmentPlanToWorld * sourceToDesign.inverse;
            ValidateRigid(designToWorld, "design/world basis");
            Require(Vector3.Dot(designToWorld.MultiplyVector(Vector3.up), Vector3.up) > .999f,
                "The imported apartment must remain upright.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Require(prefab != null, "Neighborhood FBX did not import as a model.");

            GameObject stage = new GameObject("Neighborhood · measured Blender source");
            stage.SetActive(false);
            try
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stage.transform);
                Require(model.GetComponentsInChildren<Camera>(true).Length == 0
                    && model.GetComponentsInChildren<Light>(true).Length == 0
                    && model.GetComponentsInChildren<Collider>(true).Length == 0,
                    "Neighborhood FBX must contain geometry/anchors only, without imported lights, cameras or generated colliders.");
                Transform[] nodes = model.GetComponentsInChildren<Transform>(true);
                Require(!nodes.Any(node => node.name == "Laptop_Screen" || node.name == "DoorHinge"
                    || node.name == "PlayerSpawn" || node.name == "DeskSeat"
                    || node.name.StartsWith("ExistingApartment", StringComparison.Ordinal)
                    || node.name.StartsWith("REFERENCE_ONLY", StringComparison.Ordinal)),
                    "The neighborhood export contains a copied apartment or its gameplay anchors.");
                var importedPoints = ReferenceNames.Select(name => Unique(nodes, name).position).ToArray();
                Matrix4x4 correction = Basis(ReferencePoints.Select(designToWorld.MultiplyPoint3x4).ToArray())
                    * InvertBasis(Basis(importedPoints));
                SetWorldMatrix(model.transform, correction * model.transform.localToWorldMatrix, false);
                foreach (Anchor anchor in plan.anchors)
                {
                    Transform actual = Unique(nodes, anchor.name);
                    Require(Vector3.Distance(actual.position, designToWorld.MultiplyPoint3x4(V(anchor.position_design_unity))) < .001f,
                        "Imported anchor does not match the measured design: " + anchor.name);
                }

                var meshes = model.GetComponentsInChildren<MeshFilter>(true);
                Require(meshes.Length > 0, "Neighborhood has no visible geometry.");
                foreach (Part part in plan.colliders) ValidateVisiblePart(part, meshes, designToWorld);
                ValidateSupport(plan);
                Transform cap = Unique(apartment.GetComponentsInChildren<Transform>(true), "Corridor_End");
                BoxCollider capCollider = UniqueCollider(apartmentCollisionRoot, "Corridor_End Collider");
                ValidateApartmentSeam(apartment, apartmentCollisionRoot, plan, designToWorld);
                var fixtures = plan.moved_apartment_fixtures.Select(item => Unique(apartment.GetComponentsInChildren<Transform>(true), item.name)).ToArray();

                // Asset generation happens only after geometry/schema validation. The
                // original cap remains whole until every material/collider also exists.
                ApplyMaterials(model, plan);
                Transform collision = new GameObject("Neighborhood visible collision").transform;
                collision.SetParent(stage.transform, false);
                foreach (Part part in plan.colliders) AddCollider(part, collision, designToWorld);
                Material sky = CreateSky(plan.daylight);

                for (int index = 0; index < fixtures.Length; index++)
                {
                    Fixture target = plan.moved_apartment_fixtures[index];
                    Matrix4x4 delta = designToWorld * Matrix4x4.Rotate(Q(target.rotation_delta_design_xyzw)) * designToWorld.inverse;
                    Matrix4x4 moved = delta * fixtures[index].localToWorldMatrix;
                    moved.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));
                    Vector3 destination = designToWorld.MultiplyPoint3x4(V(target.position_design_unity));
                    moved.SetColumn(3, new Vector4(destination.x, destination.y, destination.z, 1f));
                    SetWorldMatrix(fixtures[index], moved, false);
                }
                var disabled = new List<string> { cap.name, capCollider.name };
                cap.gameObject.SetActive(false);
                capCollider.gameObject.SetActive(false);
                // The apartment FBX contains its former outside-window study. The
                // new neighborhood replaces that scene-only scenery, not the room.
                foreach (Transform item in apartment.GetComponentsInChildren<Transform>(true))
                    if (OldExterior(item.name)) { item.gameObject.SetActive(false); disabled.Add(item.name); }
                foreach (Collider item in apartmentCollisionRoot.GetComponentsInChildren<Collider>(true))
                    if (OldExterior(item.name.Replace(" Collider", ""))) { item.gameObject.SetActive(false); disabled.Add(item.name); }
                stage.SetActive(true);
                configureDaylight = () => ApplyDaylight(plan.daylight, designToWorld, sky);
                Debug.Log("Neighborhood validated. Disabled original scene objects: " + string.Join(", ", disabled)
                    + ". Original apartment assets and gameplay anchors are retained. Traversal acceptance is separate.");
                return true;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(stage);
                throw;
            }
        }

        private static void ValidateManifest(Manifest plan)
        {
            Require(plan != null && plan.schema_version == 1 && plan.units == "m" && plan.export_scope == "neighborhood_only",
                "Unsupported neighborhood manifest contract.");
            Require(plan.immutable_apartment_sha256 != null && plan.immutable_apartment_sha256.Length == 64
                && plan.immutable_apartment_sha256.All(Uri.IsHexDigit), "Apartment source provenance is missing.");
            Require(plan.anchors != null && plan.anchors.Length >= 5 && plan.anchors.Length <= 256, "Invalid neighborhood anchors.");
            UniqueNames(plan.anchors.Select(item => item == null ? null : item.name), "anchor");
            foreach (Anchor item in plan.anchors) V(item.position_design_unity);
            for (int index = 0; index < ReferenceNames.Length; index++)
            {
                Anchor anchor = plan.anchors.SingleOrDefault(item => item.name == ReferenceNames[index]);
                Require(anchor != null && anchor.kind == "basis" && Vector3.Distance(V(anchor.position_design_unity), ReferencePoints[index]) < .00001f,
                    "Missing or changed calibration reference: " + ReferenceNames[index]);
            }
            Require(plan.aperture != null && Finite(plan.aperture.clear_width) && Finite(plan.aperture.clear_height)
                && plan.aperture.clear_width > Radius * 2f + .1f && plan.aperture.clear_height > Height + .1f,
                "The measured apartment aperture does not clear the player.");
            V(plan.aperture.floor_design_unity);
            Require(plan.moved_apartment_fixtures != null && plan.moved_apartment_fixtures.Length == 2,
                "Both corridor fixtures need measured relocation targets.");
            UniqueNames(plan.moved_apartment_fixtures.Select(item => item == null ? null : item.name), "fixture");
            foreach (Fixture item in plan.moved_apartment_fixtures)
            {
                Require(item.name == "Corridor_Fuse_Box" || item.name == "Corridor_Floor_Label", "An unapproved apartment fixture would be moved.");
                V(item.position_design_unity); Q(item.rotation_delta_design_xyzw);
            }
            Require(plan.colliders != null && plan.colliders.Length > 0 && plan.colliders.Length <= 5000, "Invalid neighborhood collision list.");
            UniqueNames(plan.colliders.Select(item => item == null ? null : item.name), "collider");
            UniqueNames(plan.colliders.Select(item => item.renderer_name), "collider renderer");
            foreach (Part item in plan.colliders)
            {
                Require(item.visible && !item.trigger, "A neighborhood collision must match a visible nontrigger surface: " + item.name);
                if (item.type == "box")
                {
                    V(item.center_design_unity); Vector3 size = V(item.size_unity);
                    Require(size.x > 0f && size.y > 0f && size.z > 0f, "Box dimensions must be positive: " + item.name);
                }
                else
                {
                    Require(item.type == "mesh" && item.vertices_xyz != null && item.vertices_xyz.Length >= 9
                        && item.vertices_xyz.Length % 3 == 0 && item.vertices_xyz.Length <= 300000
                        && item.triangles != null && item.triangles.Length >= 3 && item.triangles.Length % 3 == 0
                        && item.triangles.Length <= 300000, "Invalid mesh collision arrays: " + item.name);
                    Require(item.vertices_xyz.All(Finite) && item.triangles.All(value => value >= 0 && value < item.vertices_xyz.Length / 3),
                        "Invalid mesh collision vertex/index: " + item.name);
                    for (int i = 0; i < item.triangles.Length; i += 3)
                    {
                        Vector3 a = Vertex(item, item.triangles[i]), b = Vertex(item, item.triangles[i + 1]), c = Vertex(item, item.triangles[i + 2]);
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        Require(normal.sqrMagnitude > .000000001f && normal.normalized.y > .7f,
                            "A road/walk/ramp collision triangle is degenerate, too steep or faces down: " + item.name);
                    }
                }
            }
            Require(plan.materials != null && plan.materials.Length > 0 && plan.materials.Length <= 512 && plan.textures != null,
                "Portable material/texture manifests are required.");
            UniqueNames(plan.materials.Select(item => item == null ? null : item.name), "material");
            UniqueNames(plan.textures.Select(item => item == null ? null : item.path), "texture", true);
            foreach (TextureInfo item in plan.textures)
            {
                TexturePath(item.path);
                Require(item.role == "base_color" || item.role == "normal" || item.role == "metallic_smoothness" || item.role == "emission", "Unknown texture role.");
                Require(item.srgb == (item.role == "base_color" || item.role == "emission") && item.normal_map == (item.role == "normal"), "Texture color-space/normal role mismatch.");
                Require(item.physical_tile_size_m != null && item.physical_tile_size_m.Length == 2
                    && item.physical_tile_size_m.All(value => Finite(value) && value > 0f), "Texture physical scale is missing.");
            }
            foreach (SurfaceMaterial item in plan.materials)
            {
                ColorValue(item.base_color_rgba, 4, false); ColorValue(item.emission_rgb, 3, true);
                Require(Unit(item.metallic) && Unit(item.smoothness) && Unit(item.alpha_cutoff)
                    && (item.alpha_mode == "opaque" || item.alpha_mode == "transparent"), "Invalid Standard material values: " + item.name);
                ValidateTextureUse(item.base_color, "base_color", plan); ValidateTextureUse(item.normal, "normal", plan);
                ValidateTextureUse(item.metallic_smoothness, "metallic_smoothness", plan); ValidateTextureUse(item.emission, "emission", plan);
            }
            Require(plan.daylight != null, "Daylight settings are missing.");
            Vector3 forward = V(plan.daylight.forward_design_unity);
            Require(forward.sqrMagnitude > .01f && forward.normalized.y < -.05f, "Daylight must point down toward the neighborhood.");
            ColorValue(plan.daylight.color_srgb, 3, false); ColorValue(plan.daylight.ambient_sky_srgb, 3, false);
            ColorValue(plan.daylight.ambient_equator_srgb, 3, false); ColorValue(plan.daylight.ambient_ground_srgb, 3, false);
            Require(Finite(plan.daylight.intensity) && plan.daylight.intensity > 0f && plan.daylight.intensity <= 4f
                && Finite(plan.daylight.ambient_intensity) && plan.daylight.ambient_intensity > 0f && plan.daylight.ambient_intensity <= 2f,
                "Daylight intensities are out of range.");
        }

        private static void ValidateVisiblePart(Part part, MeshFilter[] meshes, Matrix4x4 map)
        {
            MeshFilter[] matches = meshes.Where(item => item.name == part.renderer_name).ToArray();
            Require(matches.Length == 1 && matches[0].sharedMesh != null && matches[0].GetComponent<MeshRenderer>() != null,
                "Collision does not identify exactly one visible mesh: " + part.name);
            MeshFilter mesh = matches[0];
            Bounds rendered = TransformBounds(mesh.sharedMesh.bounds, mesh.transform.localToWorldMatrix);
            Bounds collision = part.type == "box"
                ? TransformBounds(new Bounds(V(part.center_design_unity), V(part.size_unity)), map)
                : BoundsOf(Enumerable.Range(0, part.vertices_xyz.Length / 3).Select(index => map.MultiplyPoint3x4(Vertex(part, index))));
            Require(Vector3.Distance(rendered.min, collision.min) < Tolerance && Vector3.Distance(rendered.max, collision.max) < Tolerance,
                "Visible geometry and collision extents differ: " + part.name);
        }

        private static void ValidateApartmentSeam(GameObject apartment, Transform collisions, Manifest plan, Matrix4x4 map)
        {
            Matrix4x4 toDesign = map.inverse;
            Bounds floor = ColliderBounds(UniqueCollider(collisions, "Corridor_Floor Collider"), toDesign);
            Bounds cap = ColliderBounds(UniqueCollider(collisions, "Corridor_End Collider"), toDesign);
            Bounds west = ColliderBounds(UniqueCollider(collisions, "Corridor_West Collider"), toDesign);
            Bounds east = ColliderBounds(UniqueCollider(collisions, "Corridor_East Collider"), toDesign);
            Bounds ceiling = ColliderBounds(UniqueCollider(collisions, "Corridor_Ceiling Collider"), toDesign);
            foreach (string name in new[] { "Corridor_Floor", "Corridor_End" })
            {
                MeshFilter mesh = Unique(apartment.GetComponentsInChildren<Transform>(true), name).GetComponent<MeshFilter>();
                Require(mesh != null && mesh.sharedMesh != null, "Original corridor mesh is missing: " + name);
                Bounds actual = TransformBounds(mesh.sharedMesh.bounds, toDesign * mesh.transform.localToWorldMatrix);
                Bounds measured = name == "Corridor_Floor" ? floor : cap;
                Require(Vector3.Distance(actual.min, measured.min) < Tolerance && Vector3.Distance(actual.max, measured.max) < Tolerance,
                    "Original corridor mesh and manifest collider disagree: " + name);
            }
            Vector3 exit = V(plan.aperture.floor_design_unity);
            Require(Mathf.Abs(floor.max.y - exit.y) < .003f && Mathf.Abs(floor.max.z - exit.z) < .003f
                && Mathf.Abs(floor.center.x - exit.x) < .003f && Mathf.Abs(cap.center.z - exit.z) < .003f,
                "The neighborhood aperture does not meet the actual apartment floor/cap.");
            Bounds left = west.center.x < east.center.x ? west : east, right = west.center.x < east.center.x ? east : west;
            Require(Mathf.Abs(right.min.x - left.max.x - plan.aperture.clear_width) < .025f
                && ceiling.min.y - floor.max.y + .01f >= plan.aperture.clear_height, "Corridor dimensions disagree with the aperture.");
            Require(apartment.GetComponentsInChildren<MeshFilter>(true).Any(item => item.name.StartsWith("Window_Glass_", StringComparison.Ordinal)),
                "The original apartment window geometry is missing.");
            // The unchanged real window looks into this three-metre public passage.
            foreach (float z in new[] { -3f, -.15f, 2.8f }) RequireClear(plan, new Vector3(5f, .02f, z));
        }

        private static void ValidateSupport(Manifest plan)
        {
            foreach (Anchor anchor in plan.anchors.Where(item => item.kind == "feet"))
                Require(Supported(plan, V(anchor.position_design_unity)), "A foot anchor lacks its measured walking surface: " + anchor.name);
            Vector3 aperture = V(plan.aperture.floor_design_unity);
            foreach (float x in new[] { -plan.aperture.clear_width * .5f + Radius + .02f, 0f, plan.aperture.clear_width * .5f - Radius - .02f })
                foreach (float z in new[] { .01f, .10f, .35f }) RequireClear(plan, aperture + new Vector3(x, 0f, z));
            foreach (string name in new[] { "ApartmentStreetExit", "ShopEntrance", "ShopCustomerTalk", "CopyShopEntrance", "CopyCustomerWaiting", "PolicePublicEntrance", "PoliceReception", "HomeEastReturn", "HomeWestReturn", "DeliveryCommonWest", "DeliveryCommonEast", "DeliveryReturnStreet" })
            {
                Anchor anchor = plan.anchors.SingleOrDefault(item => item.name == name);
                Require(anchor != null, "Public route anchor is missing: " + name);
                RequireClear(plan, V(anchor.position_design_unity));
            }
        }

        private static void RequireClear(Manifest plan, Vector3 feet)
        {
            foreach (Vector3 offset in new[] { Vector3.zero, Vector3.right * Radius, Vector3.left * Radius, Vector3.forward * Radius, Vector3.back * Radius })
                Require(Supported(plan, feet + offset), "Public route lacks continuous floor support near " + feet);
            foreach (Part part in plan.colliders.Where(item => item.type == "box"))
            {
                Bounds bounds = new Bounds(V(part.center_design_unity), V(part.size_unity));
                if (bounds.max.y <= feet.y + .03f || bounds.min.y >= feet.y + Height) continue;
                float dx = Mathf.Max(bounds.min.x - feet.x, 0f, feet.x - bounds.max.x);
                float dz = Mathf.Max(bounds.min.z - feet.z, 0f, feet.z - bounds.max.z);
                Require(dx * dx + dz * dz >= Radius * Radius, "Public player clearance is blocked by " + part.name + " near " + feet);
            }
        }

        private static bool Supported(Manifest plan, Vector3 point)
        {
            foreach (Part part in plan.colliders)
            {
                if (part.type == "box")
                {
                    Bounds bounds = new Bounds(V(part.center_design_unity), V(part.size_unity));
                    if (Mathf.Abs(bounds.max.y - point.y) <= .025f && point.x >= bounds.min.x - .001f && point.x <= bounds.max.x + .001f
                        && point.z >= bounds.min.z - .001f && point.z <= bounds.max.z + .001f) return true;
                }
                else for (int index = 0; index < part.triangles.Length; index += 3)
                {
                    Vector3 a = Vertex(part, part.triangles[index]), b = Vertex(part, part.triangles[index + 1]), c = Vertex(part, part.triangles[index + 2]);
                    float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(denominator) < .0000001f) continue;
                    float u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.z - c.z)) / denominator;
                    float v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.z - c.z)) / denominator;
                    if (u >= -.001f && v >= -.001f && u + v <= 1.001f && Mathf.Abs(u * a.y + v * b.y + (1f - u - v) * c.y - point.y) <= .025f) return true;
                }
            }
            return false;
        }

        private static void AddCollider(Part part, Transform parent, Matrix4x4 map)
        {
            GameObject item = new GameObject(part.name + " Collider");
            item.transform.SetParent(parent, false); item.isStatic = true;
            if (part.type == "box")
            {
                item.transform.position = map.MultiplyPoint3x4(V(part.center_design_unity));
                item.transform.rotation = Quaternion.LookRotation(map.MultiplyVector(Vector3.forward), map.MultiplyVector(Vector3.up));
                item.AddComponent<BoxCollider>().size = V(part.size_unity);
            }
            else
            {
                var mesh = new Mesh { name = part.name + " collision" };
                if (part.vertices_xyz.Length / 3 > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = Enumerable.Range(0, part.vertices_xyz.Length / 3).Select(index => map.MultiplyPoint3x4(Vertex(part, index))).ToArray();
                int[] triangles = (int[])part.triangles.Clone();
                if (map.determinant < 0f)
                    for (int index = 0; index < triangles.Length; index += 3)
                    { int value = triangles[index + 1]; triangles[index + 1] = triangles[index + 2]; triangles[index + 2] = value; }
                mesh.triangles = triangles; mesh.RecalculateBounds(); mesh.RecalculateNormals();
                string path = GeneratedPath + "/" + SafeName(part.name) + "_Collision.asset";
                EnsureGeneratedFolder();
                Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                item.AddComponent<MeshCollider>().sharedMesh = saved;
            }
        }

        private static void ApplyMaterials(GameObject model, Manifest plan)
        {
            Require(GraphicsSettings.currentRenderPipeline == null, "Neighborhood materials target the Built-in render pipeline.");
            Shader shader = Shader.Find("Standard");
            Require(shader != null && shader.isSupported, "Built-in Standard shader is unavailable.");
            var descriptions = plan.materials.ToDictionary(item => item.name, StringComparer.Ordinal);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
                foreach (Material material in renderer.sharedMaterials)
                    Require(material != null && descriptions.ContainsKey(material.name), "Imported material lacks an explicit portable definition: " + (material == null ? "null" : material.name));
            foreach (TextureInfo texture in plan.textures)
            {
                string path = TexturePath(texture.path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer != null, "Texture failed to import: " + texture.path);
                TextureImporterType type = texture.normal_map ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.textureType != type || importer.sRGBTexture != texture.srgb || importer.wrapMode != TextureWrapMode.Repeat)
                {
                    importer.textureType = type; importer.sRGBTexture = texture.srgb; importer.wrapMode = TextureWrapMode.Repeat;
                    importer.SaveAndReimport();
                }
            }
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (SurfaceMaterial description in plan.materials)
            {
                Material material = SavedMaterial(SafeName(description.name), shader);
                material.shader = shader; material.shaderKeywords = new string[0];
                material.color = ColorValue(description.base_color_rgba, 4, false);
                material.SetFloat("_Metallic", description.metallic); material.SetFloat("_Glossiness", description.smoothness);
                material.SetFloat("_Cutoff", description.alpha_cutoff);
                SetTexture(material, "_MainTex", description.base_color);
                SetTexture(material, "_BumpMap", description.normal);
                SetTexture(material, "_MetallicGlossMap", description.metallic_smoothness);
                SetTexture(material, "_EmissionMap", description.emission);
                if (!string.IsNullOrEmpty(description.normal)) material.EnableKeyword("_NORMALMAP");
                if (!string.IsNullOrEmpty(description.metallic_smoothness)) material.EnableKeyword("_METALLICGLOSSMAP");
                Color emission = ColorValue(description.emission_rgb, 3, true);
                material.SetColor("_EmissionColor", emission);
                if (emission.maxColorComponent > 0f || !string.IsNullOrEmpty(description.emission)) material.EnableKeyword("_EMISSION");
                bool transparent = description.alpha_mode == "transparent";
                material.SetFloat("_Mode", transparent ? 3f : 0f);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
                material.SetInt("_ZWrite", transparent ? 0 : 1);
                material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
                if (transparent) material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
                EditorUtility.SetDirty(material); materials.Add(description.name, material);
            }
            foreach (Renderer renderer in renderers)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(item => materials[item.name]).ToArray();
        }

        private static Material CreateSky(Daylight light)
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            Require(shader != null && shader.isSupported, "Built-in procedural sky shader is unavailable.");
            Material sky = SavedMaterial("NeighborhoodSky", shader); sky.shader = shader;
            sky.SetColor("_SkyTint", ColorValue(light.ambient_sky_srgb, 3, false));
            sky.SetColor("_GroundColor", ColorValue(light.ambient_ground_srgb, 3, false));
            sky.SetFloat("_Exposure", 1f); sky.SetFloat("_AtmosphereThickness", 1f);
            EditorUtility.SetDirty(sky); return sky;
        }
        private static void ApplyDaylight(Daylight light, Matrix4x4 map, Material sky)
        {
            var sunObject = new GameObject("Neighborhood afternoon sun", typeof(Light));
            sunObject.transform.rotation = Quaternion.LookRotation(map.MultiplyVector(V(light.forward_design_unity)).normalized);
            Light sun = sunObject.GetComponent<Light>(); sun.type = LightType.Directional;
            sun.color = ColorValue(light.color_srgb, 3, false); sun.intensity = light.intensity; sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun; RenderSettings.skybox = sky; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ColorValue(light.ambient_sky_srgb, 3, false);
            RenderSettings.ambientEquatorColor = ColorValue(light.ambient_equator_srgb, 3, false);
            RenderSettings.ambientGroundColor = ColorValue(light.ambient_ground_srgb, 3, false);
            RenderSettings.ambientIntensity = light.ambient_intensity;
        }

        private static bool OldExterior(string name)
        {
            return name == "Exterior_Alley" || name.StartsWith("City_Building_", StringComparison.Ordinal)
                || name.StartsWith("Facade_Stringcourse_", StringComparison.Ordinal) || name.StartsWith("City_Pane_", StringComparison.Ordinal);
        }
        private static void ValidateTextureUse(string path, string role, Manifest plan)
        {
            if (string.IsNullOrEmpty(path)) return;
            Require(plan.textures.Any(item => item.path == path && item.role == role), "Material texture lacks matching import metadata: " + path);
        }
        private static string TexturePath(string relative)
        {
            Require(!string.IsNullOrEmpty(relative) && relative.StartsWith("Textures/", StringComparison.Ordinal)
                && !relative.Contains('\\') && !relative.Contains(':') && relative.Split('/').All(part => part != "" && part != "." && part != ".."), "Invalid neighborhood texture path.");
            string path = DirectoryPath + "/" + relative;
            Require(File.Exists(path), "Neighborhood texture is missing: " + relative); return path;
        }
        private static void SetTexture(Material material, string property, string relative)
        {
            Texture2D texture = string.IsNullOrEmpty(relative) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(relative));
            Require(string.IsNullOrEmpty(relative) || texture != null, "A required texture did not import."); material.SetTexture(property, texture);
        }
        private static Material SavedMaterial(string name, Shader shader)
        {
            EnsureGeneratedFolder(); string path = GeneratedPath + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            return material;
        }
        private static void EnsureGeneratedFolder()
        { if (!AssetDatabase.IsValidFolder(GeneratedPath)) AssetDatabase.CreateFolder(DirectoryPath, "Generated"); }
        private static string SafeName(string value)
        {
            Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 160
                && value.All(character => char.IsLetterOrDigit(character) || "_. -".IndexOf(character) >= 0), "Unsafe generated asset name."); return value;
        }
        private static void UniqueNames(IEnumerable<string> names, string kind, bool paths = false)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in names) Require(!string.IsNullOrWhiteSpace(name) && (paths || SafeName(name) == name) && seen.Add(name), "Missing or duplicate " + kind + " name.");
        }
        private static Transform Unique(Transform[] nodes, string name)
        {
            Transform[] matches = nodes.Where(item => item.name == name).ToArray();
            Require(matches.Length == 1, "Expected exactly one imported object: " + name); return matches[0];
        }
        private static BoxCollider UniqueCollider(Transform root, string name)
        {
            var matches = root.GetComponentsInChildren<BoxCollider>(true).Where(item => item.name == name).ToArray();
            Require(matches.Length == 1, "Expected exactly one original apartment collider: " + name); return matches[0];
        }
        private static Bounds ColliderBounds(BoxCollider collider, Matrix4x4 map)
        { return TransformBounds(new Bounds(collider.center, collider.size), map * collider.transform.localToWorldMatrix); }
        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            var corners = new List<Vector3>();
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                corners.Add(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
            return BoundsOf(corners);
        }
        private static Bounds BoundsOf(IEnumerable<Vector3> points)
        { Vector3[] values = points.ToArray(); var bounds = new Bounds(values[0], Vector3.zero); foreach (Vector3 point in values) bounds.Encapsulate(point); return bounds; }
        private static Matrix4x4 Basis(Vector3[] points)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            matrix.SetColumn(0, points[1] - points[0]); matrix.SetColumn(1, points[3] - points[0]); matrix.SetColumn(2, points[2] - points[0]);
            matrix.SetColumn(3, new Vector4(points[0].x, points[0].y, points[0].z, 1f)); return matrix;
        }
        private static Matrix4x4 InvertBasis(Matrix4x4 matrix)
        { Require(Finite(matrix.determinant) && Mathf.Abs(matrix.determinant) > .0001f, "Imported reference basis is singular."); return matrix.inverse; }
        private static Matrix4x4 Matrix(float[] values)
        {
            Require(values != null && values.Length == 16 && values.All(Finite), "Invalid affine matrix.");
            var matrix = new Matrix4x4(); for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++) matrix[row, column] = values[row * 4 + column]; return matrix;
        }
        private static void ValidateRigid(Matrix4x4 matrix, string name)
        {
            for (int i = 0; i < 16; i++) Require(Finite(matrix[i]), "Nonfinite " + name);
            Vector3 x = matrix.GetColumn(0), y = matrix.GetColumn(1), z = matrix.GetColumn(2);
            Require(Mathf.Abs(x.magnitude - 1f) < .001f && Mathf.Abs(y.magnitude - 1f) < .001f && Mathf.Abs(z.magnitude - 1f) < .001f
                && Mathf.Abs(Vector3.Dot(x, y)) < .001f && Mathf.Abs(Vector3.Dot(x, z)) < .001f && Mathf.Abs(Vector3.Dot(y, z)) < .001f
                && Mathf.Abs(matrix[3, 0]) < .0001f && Mathf.Abs(matrix[3, 1]) < .0001f && Mathf.Abs(matrix[3, 2]) < .0001f && Mathf.Abs(matrix[3, 3] - 1f) < .0001f,
                "Scaled/sheared or invalid " + name);
        }
        private static void SetWorldMatrix(Transform transform, Matrix4x4 matrix, bool requireUnitScale = true)
        {
            Matrix4x4 local = transform.parent == null ? matrix : transform.parent.worldToLocalMatrix * matrix;
            Vector3 x = local.GetColumn(0), y = local.GetColumn(1), z = local.GetColumn(2);
            Require(x.sqrMagnitude > .00000001f && y.sqrMagnitude > .00000001f && z.sqrMagnitude > .00000001f, "Degenerate imported transform.");
            Require(Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) < .001f && Mathf.Abs(Vector3.Dot(x.normalized, z.normalized)) < .001f
                && Mathf.Abs(Vector3.Dot(y.normalized, z.normalized)) < .001f, "Imported transform requires unsupported shear.");
            if (requireUnitScale) ValidateRigid(matrix, "calibrated model transform");
            transform.localPosition = local.GetColumn(3);
            transform.localRotation = Quaternion.LookRotation(z.normalized, y.normalized);
            transform.localScale = new Vector3(x.magnitude * (local.determinant < 0f ? -1f : 1f), y.magnitude, z.magnitude);
        }
        private static Vector3 Vertex(Part part, int index)
        { int offset = index * 3; return new Vector3(part.vertices_xyz[offset], part.vertices_xyz[offset + 1], part.vertices_xyz[offset + 2]); }
        private static Vector3 V(float[] values)
        { Require(values != null && values.Length == 3 && values.All(Finite), "Expected three finite coordinates."); return new Vector3(values[0], values[1], values[2]); }
        private static Quaternion Q(float[] values)
        {
            Require(values != null && values.Length == 4 && values.All(Finite), "Invalid fixture rotation.");
            var quaternion = new Quaternion(values[0], values[1], values[2], values[3]);
            Require(Mathf.Abs(Quaternion.Dot(quaternion, quaternion) - 1f) < .001f, "Fixture quaternion must be normalized."); return quaternion;
        }
        private static Color ColorValue(float[] values, int count, bool hdr)
        {
            Require(values != null && values.Length == count && values.All(value => Finite(value) && value >= 0f && value <= (hdr ? 64f : 1f)), "Invalid material/light color.");
            return new Color(values[0], values[1], values[2], count == 4 ? values[3] : 1f);
        }
        private static bool Unit(float value) { return Finite(value) && value >= 0f && value <= 1f; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static void Require(bool condition, string message) { if (!condition) throw Failure(message); }
        private static BuildFailedException Failure(string message) { return new BuildFailedException("Neighborhood: " + message); }
    }
}
#endif
