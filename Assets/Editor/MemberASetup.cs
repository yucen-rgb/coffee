using System;
using System.Collections.Generic;
using System.Linq;
using CoffeeVR.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace CoffeeVR.Editor
{
    /// <summary>Idempotent, scene-scoped setup for Member A's interaction layer.</summary>
    public static class MemberASetup
    {
        private const string ScenePath = "Assets/Scenes/Coffee_Scene.unity";
        private const string OriginPrefabPath = "Assets/Samples/XR Interaction Toolkit/2.5.4/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string SimulatorPrefabPath = "Assets/Samples/XR Interaction Toolkit/2.5.4/XR Device Simulator/XR Device Simulator.prefab";
        private const string InputActionsPath = "Assets/Samples/XR Interaction Toolkit/2.5.4/Starter Assets/XRI Default Input Actions.inputactions";

        [MenuItem("Coffee VR/Member A/Configure Coffee Scene Interaction")]
        public static void ConfigureAndSave()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Could not open " + ScenePath);

            ConfigurePlayerRig(scene);
            ConfigureInteractionManager(scene);
            ConfigureGrabItems(scene);
            ConfigureStations(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + ScenePath);

            Debug.Log("Member A setup saved. Scene: " + ScenePath);
        }

        private static void ConfigurePlayerRig(Scene scene)
        {
            var oldPlayer = GameObject.Find("Player");
            var origin = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject)
                .FirstOrDefault(go => go.name == "XR Origin (XR Rig)");

            if (origin == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OriginPrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException("Missing XRI Starter Assets prefab: " + OriginPrefabPath);

                origin = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(origin, "Add XR Origin");
                if (oldPlayer != null)
                {
                    origin.transform.SetPositionAndRotation(oldPlayer.transform.position, oldPlayer.transform.rotation);
                    oldPlayer.SetActive(false); // Retained in the scene for easy rollback; disables legacy movement and camera.
                    EditorUtility.SetDirty(oldPlayer);
                }
            }

            if (Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, "Player") >= 0)
                origin.tag = "Player";

            var actionManager = origin.GetComponentInChildren<InputActionManager>(true);
            if (actionManager == null)
                actionManager = Undo.AddComponent<InputActionManager>(origin);

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
                throw new InvalidOperationException("Missing XRI Input Actions: " + InputActionsPath);
            actionManager.actionAssets = new List<InputActionAsset> { actions };
            EditorUtility.SetDirty(actionManager);

            var simulator = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject)
                .FirstOrDefault(go => go.name == "XR Device Simulator");
            if (simulator == null)
            {
                var simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath);
                if (simulatorPrefab == null)
                    throw new InvalidOperationException("Missing XRI XR Device Simulator prefab: " + SimulatorPrefabPath);
                simulator = (GameObject)PrefabUtility.InstantiatePrefab(simulatorPrefab, scene);
                Undo.RegisterCreatedObjectUndo(simulator, "Add XR Device Simulator");
            }
        }

        private static void ConfigureInteractionManager(Scene scene)
        {
            var manager = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<XRInteractionManager>(true))
                .FirstOrDefault();
            if (manager == null)
            {
                var go = new GameObject("XR Interaction Manager");
                Undo.RegisterCreatedObjectUndo(go, "Add XR Interaction Manager");
                SceneManager.MoveGameObjectToScene(go, scene);
                manager = Undo.AddComponent<XRInteractionManager>(go);
            }
        }

        private static void ConfigureGrabItems(Scene scene)
        {
            var movableRoot = GameObject.Find("Movable Objects");
            if (movableRoot == null)
                throw new InvalidOperationException("Movable Objects root not found in Coffee_Scene.");

            var transforms = movableRoot.GetComponentsInChildren<Transform>(true);
            var validGrabRoots = new HashSet<GameObject>(transforms
                .Where(t => IsGrabCandidate(t, movableRoot.transform))
                .Select(t => t.gameObject));
            var configured = new HashSet<GrabInteractable>();
            foreach (var oldBridge in movableRoot.GetComponentsInChildren<GrabInteractable>(true))
            {
                if (validGrabRoots.Contains(oldBridge.gameObject))
                    continue;

                Undo.DestroyObjectImmediate(oldBridge);
            }

            foreach (var oldGrab in movableRoot.GetComponentsInChildren<XRGrabInteractable>(true))
            {
                if (validGrabRoots.Contains(oldGrab.gameObject))
                    continue;

                // Remove stale interactables left by earlier setup runs, respecting dependencies.
                if (oldGrab != null)
                    Undo.DestroyObjectImmediate(oldGrab);
            }

            foreach (var oldBody in movableRoot.GetComponentsInChildren<Rigidbody>(true))
            {
                var isSceneAddedBody = PrefabUtility.IsAddedComponentOverride(oldBody) ||
                                       PrefabUtility.GetCorrespondingObjectFromSource(oldBody) == null;
                if (validGrabRoots.Contains(oldBody.gameObject) || !isSceneAddedBody)
                    continue;

                // Remove bodies from non-item nodes when they are scene additions. The original
                // scene backup contained no Movable Objects Rigidbody, so this does not strip
                // inherited Rigidbody components from source Prefabs.
                Undo.DestroyObjectImmediate(oldBody);
            }

            foreach (var transform in transforms)
            {
                if (!validGrabRoots.Contains(transform.gameObject) ||
                    !TryGetItemId(transform, movableRoot.transform, out var itemId))
                {
                    continue;
                }

                if (transform.GetComponentInChildren<Renderer>(true) == null)
                    continue;

                EnsureUsableCollider(transform.gameObject);
                var body = transform.GetComponent<Rigidbody>();
                if (body == null)
                    body = Undo.AddComponent<Rigidbody>(transform.gameObject);
                body.mass = Mathf.Clamp(body.mass, 0.05f, 3f);
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.isKinematic = false;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
                body.isKinematic = true;
                EditorUtility.SetDirty(body);

                var grab = transform.GetComponent<XRGrabInteractable>();
                if (grab == null)
                    grab = Undo.AddComponent<XRGrabInteractable>(transform.gameObject);
                grab.selectMode = InteractableSelectMode.Single;
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                grab.trackScale = false;
                grab.throwOnDetach = false;

                var bridge = transform.GetComponent<GrabInteractable>();
                if (bridge == null)
                    bridge = Undo.AddComponent<GrabInteractable>(transform.gameObject);
                bridge.EditorSetItemId(itemId);
                EditorUtility.SetDirty(grab);
                EditorUtility.SetDirty(bridge);
                configured.Add(bridge);
            }

            // Assign each collider to only its nearest grab interactable. This prevents nested items
            // (for example a cup sitting on a plate) from registering the same collider twice.
            foreach (var bridge in configured)
            {
                var grab = bridge.GetComponent<XRGrabInteractable>();
                grab.colliders.Clear();
                grab.colliders.AddRange(bridge.GetComponentsInChildren<Collider>(true)
                    .Where(c => c.enabled && !c.isTrigger && c.GetComponentInParent<XRGrabInteractable>() == grab));
                if (grab.colliders.Count == 0)
                {
                    var collider = bridge.GetComponent<Collider>();
                    if (collider != null && !collider.isTrigger)
                        grab.colliders.Add(collider);
                }
                EditorUtility.SetDirty(grab);
            }

            Debug.Log("Configured grab candidates: " + configured.Count);
        }

        private static bool IsSceneItemRoot(Transform item, Transform movableRoot)
        {
            var path = GetRelativePath(item, movableRoot);
            if (path.StartsWith("01_Equipment/", StringComparison.Ordinal) ||
                path == "01_Equipment/oven" ||
                path == "refrigerator" ||
                path == "glasses" ||
                path.StartsWith("05_Decoration/", StringComparison.Ordinal))
                return false;

            var parentPath = item.parent == null ? string.Empty : GetRelativePath(item.parent, movableRoot);
            if (parentPath == "02_CoffeeTools" || parentPath == "03_Ingedients" ||
                parentPath == "03_Ingedients/milk" || parentPath == "03_Ingedients/Flour")
                return TryMapName(item.name, out _);
            if (parentPath == "03_Ingedients/Items_3")
                return item.name.StartsWith("Cafe_Coffee_Bag_", StringComparison.Ordinal) ||
                       item.name == "Cafe_Chemex_1" || item.name == "Cafe_Chemex_2";
            if (parentPath == "glasses")
                return HasOwnRenderer(item.gameObject);
            return false;
        }

        private static bool IsGrabCandidate(Transform item, Transform movableRoot)
        {
            if (item == movableRoot || !TryGetItemId(item, movableRoot, out _))
                return false;
            if ((item.name == "milk" || item.name == "Flour") && item.childCount > 0 && !HasOwnRenderer(item.gameObject))
                return false;
            if (IsUnderNamedBranch(item, movableRoot, "glasses"))
                return item.parent == movableRoot.Find("glasses") && HasOwnRenderer(item.gameObject);
            return IsSceneItemRoot(item, movableRoot);
        }

        private static bool TryGetItemId(Transform item, Transform movableRoot, out string itemId)
        {
            itemId = null;
            if (item == movableRoot || !TryMapName(item.name, out itemId))
                return false;
            if (item.name == "oven" || item.name == "refrigerator" || item.name == "Movable Objects")
                return false;

            if (IsUnderNamedBranch(item, movableRoot, "Items_3"))
            {
                // Decorative objects in Items_3 are not ingredients or tools.
                if (!item.name.StartsWith("Cafe_Coffee_Bag_", StringComparison.Ordinal) &&
                    item.name != "Cafe_Chemex_1" && item.name != "Cafe_Chemex_2")
                    return false;
            }

            return true;
        }

        private static bool TryMapName(string objectName, out string itemId)
        {
            var name = objectName;
            var suffixStart = name.IndexOf(" (", StringComparison.Ordinal);
            if (suffixStart >= 0)
                name = name.Substring(0, suffixStart);

            if (name.Equals("milk", StringComparison.OrdinalIgnoreCase)) itemId = "ingredient.milk";
            else if (name.Equals("flour_008", StringComparison.OrdinalIgnoreCase) || name.Equals("Flour", StringComparison.OrdinalIgnoreCase)) itemId = "ingredient.flour";
            else if (name.Equals("eggs", StringComparison.OrdinalIgnoreCase) || name.Equals("egg", StringComparison.OrdinalIgnoreCase)) itemId = "ingredient.egg";
            else if (name.StartsWith("Cafe_Coffee_Bag_", StringComparison.Ordinal))
            {
                var suffix = name.Substring("Cafe_Coffee_Bag_".Length);
                var typeNumber = new string(suffix.TakeWhile(char.IsDigit).ToArray());
                itemId = string.IsNullOrEmpty(typeNumber) ? "prop.cafe_coffee_bag" : "prop.cafe_coffee_bag_" + typeNumber;
            }
            else if (name.StartsWith("Cafe_Cup_", StringComparison.Ordinal) || name.Equals("Cup_Without_Coffee", StringComparison.Ordinal)) itemId = "container.cup";
            else if (name.StartsWith("Coffee_Plate", StringComparison.Ordinal)) itemId = "container.plate";
            else if (name.StartsWith("Tray", StringComparison.Ordinal)) itemId = "container.tray";
            else if (name.StartsWith("Cafe_Chemex", StringComparison.Ordinal)) itemId = "tool.chemex";
            else if (name.StartsWith("Cafe_Bottle_with_Coffee", StringComparison.Ordinal)) itemId = "container.coffee_bottle";
            else if (name.StartsWith("pump-bottle", StringComparison.Ordinal)) itemId = "tool.pump_bottle";
            else if (name.Equals("jar", StringComparison.OrdinalIgnoreCase)) itemId = "container.jar";
            else if (name.Equals("strawberry", StringComparison.OrdinalIgnoreCase)) itemId = "ingredient.strawberry";
            else if (name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0 && name != "glasses") itemId = "container.glass";
            else { itemId = null; return false; }

            return true;
        }

        private static void EnsureUsableCollider(GameObject target)
        {
            foreach (var box in target.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!box.enabled || (!HasNegativeAxis(box.transform.lossyScale)))
                    continue;

                var owner = box.GetComponentInParent<XRGrabInteractable>();
                if (owner != null && owner.gameObject != target)
                    continue;

                // Unity reports undefined BoxCollider geometry under mirrored model transforms.
                // Keep the existing component for rollback, disable it, and use an equivalent
                // capsule collider on this scene instance without changing its transform or mesh.
                var replacement = Undo.AddComponent<CapsuleCollider>(box.gameObject);
                replacement.center = box.center;
                replacement.direction = 1;
                replacement.height = Mathf.Max(0.01f, box.size.y);
                replacement.radius = Mathf.Max(0.01f, Mathf.Max(box.size.x, box.size.z) * 0.5f);
                box.enabled = false;
                EditorUtility.SetDirty(box);
                EditorUtility.SetDirty(replacement);
            }

            // A root collider is required so this object does not borrow a nested item's collider.
            var colliders = target.GetComponents<Collider>().Where(c => !c.isTrigger && c.enabled).ToArray();
            if (colliders.Length == 0)
            {
                var renderers = target.GetComponentsInChildren<Renderer>(true);
                var bounds = new Bounds(target.transform.position, Vector3.one * 0.1f);
                if (renderers.Length > 0)
                {
                    bounds = renderers[0].bounds;
                    for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                }
                var localSize = target.transform.InverseTransformVector(bounds.size);
                localSize = new Vector3(Mathf.Max(0.01f, Mathf.Abs(localSize.x)), Mathf.Max(0.01f, Mathf.Abs(localSize.y)), Mathf.Max(0.01f, Mathf.Abs(localSize.z)));
                if (target.transform.lossyScale.x < 0f || target.transform.lossyScale.y < 0f || target.transform.lossyScale.z < 0f)
                {
                    // CapsuleCollider handles mirrored café asset transforms without BoxCollider warnings.
                    var capsule = Undo.AddComponent<CapsuleCollider>(target);
                    capsule.center = target.transform.InverseTransformPoint(bounds.center);
                    capsule.direction = 1;
                    capsule.height = localSize.y;
                    capsule.radius = Mathf.Max(localSize.x, localSize.z) * 0.5f;
                    EditorUtility.SetDirty(capsule);
                }
                else
                {
                    var box = Undo.AddComponent<BoxCollider>(target);
                    box.center = target.transform.InverseTransformPoint(bounds.center);
                    box.size = localSize;
                    EditorUtility.SetDirty(box);
                }
            }

            foreach (var meshCollider in target.GetComponents<MeshCollider>())
            {
                if (!meshCollider.convex)
                {
                    meshCollider.convex = true;
                    EditorUtility.SetDirty(meshCollider);
                }
            }
        }

        private static bool HasNegativeAxis(Vector3 scale) => scale.x < 0f || scale.y < 0f || scale.z < 0f;

        private static void ConfigureStations(Scene scene)
        {
            ConfigureStation(GameObject.Find("oven"), "station.oven");
            ConfigureStation(GameObject.Find("refrigerator"), "station.refrigerator");
        }

        private static void ConfigureStation(GameObject station, string stationId)
        {
            if (station == null)
            {
                Debug.LogWarning("Station not found: " + stationId);
                return;
            }

            EnsureUsableCollider(station);
            var simple = station.GetComponent<XRSimpleInteractable>();
            if (simple == null)
                simple = Undo.AddComponent<XRSimpleInteractable>(station);
            simple.colliders.Clear();
            simple.colliders.AddRange(station.GetComponents<Collider>().Where(c => c.enabled && !c.isTrigger));

            var adapter = station.GetComponent<StationInteractable>();
            if (adapter == null)
                adapter = Undo.AddComponent<StationInteractable>(station);
            adapter.EditorSetStationId(stationId);
            EditorUtility.SetDirty(simple);
            EditorUtility.SetDirty(adapter);
        }

        private static bool HasOwnRenderer(GameObject go) =>
            go.GetComponent<Renderer>() != null || go.GetComponent<Collider>() != null;

        private static bool IsUnderNamedBranch(Transform item, Transform root, string branchName)
        {
            for (var current = item; current != null && current != root; current = current.parent)
            {
                if (current.name == branchName)
                    return true;
            }
            return false;
        }

        private static string GetRelativePath(Transform item, Transform root)
        {
            var parts = new Stack<string>();
            for (var current = item; current != null && current != root; current = current.parent)
                parts.Push(current.name);
            return string.Join("/", parts.ToArray());
        }
    }
}





