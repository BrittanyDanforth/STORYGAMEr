#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SignalDebt.Editor
{
    // Real engine checks, run only in Play Mode. No test-framework package, save
    // store, bootstrap fixture, imported asset, or synthetic Unity API is used.
    public static class SignalDebtComputerPlayModeChecks
    {
        private const string Menu = "Signal Debt/Validation/Run Computer Play Mode Checks";
        private static readonly List<Behaviour> paused = new List<Behaviour>();
        private static IEnumerator sequence;
        private static Await pending;
        private static GameObject fixture;
        private static SignalDebtPlayer player;
        private static SignalDebtComputerDisplay display;
        private static Canvas desktop;
        private static Transform seat, focus;
        private static CursorLockMode previousLock;
        private static bool previousCursor;
        private static int assertions, readyEvents, closedEvents, lastFrame;
        private static double suiteDeadline;

        private sealed class Await
        {
            public Func<bool> done;
            public string description;
            public double deadline;
        }
        [Serializable] private sealed class Report
        {
            public bool passed, unity_runtime_exercised = true;
            public int assertions;
            public string failure, unity_version;
            public string scope = "Real production player/display on primitive fixture; not imported-apartment visual acceptance.";
        }

        [MenuItem(Menu)]
        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Enter Play Mode, then run the computer checks. No checks ran."); return; }
            if (EditorApplication.isPaused) { Debug.LogWarning("Resume Play Mode before running checks. No checks ran."); return; }
            if (sequence != null) return;
            foreach (SignalDebtPlayer existing in Object.FindObjectsByType<SignalDebtPlayer>(FindObjectsSortMode.None))
                if (existing.IsSeated || existing.UiOpen)
                { Debug.LogWarning("Leave the laptop and close menus before running checks. No checks ran."); return; }
            previousLock = Cursor.lockState; previousCursor = Cursor.visible;
            assertions = readyEvents = closedEvents = 0; lastFrame = -1;
            try
            {
                foreach (Behaviour behaviour in Object.FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
                    if (behaviour.enabled && (behaviour is SignalDebtBootstrap || behaviour is SignalDebtPlayer
                        || behaviour is Camera || behaviour is AudioListener))
                    { paused.Add(behaviour); behaviour.enabled = false; }
                BuildFixture();
                sequence = Checks();
                suiteDeadline = EditorApplication.timeSinceStartup + 30d;
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += PlayModeChanged;
                // The editor releases cursor capture outside its Game view.
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                Debug.Log("Running computer Play Mode checks. Keep the Game view focused and leave movement controls alone.");
            }
            catch (Exception error) { Finish(error.ToString()); }
        }

        [MenuItem(Menu, true)]
        private static bool Available() { return sequence == null && !EditorApplication.isCompiling; }

        private static void BuildFixture()
        {
            fixture = new GameObject("Computer regression fixture (temporary)");
            fixture.transform.position = new Vector3(5000f, 0f, 5000f);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(fixture.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.2f, 8f);
            seat = Anchor("Seat", new Vector3(0f, 0.035f, 0.8f));
            focus = Anchor("LaptopFocus", new Vector3(0f, 1.04f, 1.72f));
            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "Laptop_Screen"; lid.transform.SetParent(fixture.transform, false);
            lid.transform.position = focus.position;
            lid.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            lid.transform.localScale = new Vector3(0.397f, 0.229f, 0.015f);
            display = fixture.AddComponent<SignalDebtComputerDisplay>();
            display.Initialize(focus, seat, lid.GetComponent<MeshFilter>(), fixture.transform);
            var actor = new GameObject("Test player", typeof(CharacterController));
            actor.transform.SetParent(fixture.transform, false);
            player = actor.AddComponent<SignalDebtPlayer>();
            player.Initialize(fixture.transform.position + new Vector3(0f, 0.035f, -1.4f), Quaternion.identity);
            desktop = new GameObject("Test desktop", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            desktop.renderMode = RenderMode.ScreenSpaceOverlay;
            player.ComputerReady += delegate { readyEvents++; display.Focus(desktop); };
            player.ComputerClosed += delegate { closedEvents++; display.Mount(desktop); };
            display.Mount(desktop);
            Physics.SyncTransforms();
        }

        private static Transform Anchor(string name, Vector3 local)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(fixture.transform, false); anchor.localPosition = local;
            return anchor;
        }

        private static IEnumerator Checks()
        {
            // Let the real CharacterController settle on the fixture floor first.
            yield return Delay(0.1f);
            Vector3 startPosition = player.transform.position, eyePosition = player.View.transform.position;
            Quaternion startRotation = player.transform.rotation, eyeRotation = player.View.transform.rotation;
            var controller = player.GetComponent<CharacterController>();
            var raycaster = desktop.GetComponent<GraphicRaycaster>();
            Check(desktop.renderMode == RenderMode.WorldSpace && !raycaster.enabled, "mounted display rejects clicks");
            Check(player.Sit(seat, focus, display), "enter from walking");
            Check(Vector3.Distance(player.transform.position, startPosition) < 0.001f,
                "sit does not teleport the visible body");
            Check(Vector3.Distance(player.View.transform.position, eyePosition) < 0.001f, "sit does not snap camera position");
            Check(Quaternion.Angle(player.View.transform.rotation, eyeRotation) < 0.1f, "sit does not snap camera rotation");
            Check(!controller.enabled && !player.CanUseComputer && !player.UiOpen, "movement and desktop blocked during sit");
            Check(!player.Sit(seat, focus, display), "duplicate entry rejected");
            yield return Delay(0.2f);
            Check(player.IsTransitioning && !player.CanUseComputer, "intermediate sit remains gated");
            Check(Vector3.Distance(player.View.transform.position, eyePosition) > 0.01f, "camera actually moves during sit");
            Check(Vector3.Distance(player.transform.position, startPosition) > 0.01f
                && Vector3.Distance(player.transform.position, seat.position) > 0.01f,
                "visible body moves between standing and seated poses");
            Check(!Cursor.visible && Cursor.lockState == CursorLockMode.Locked, "cursor locked during approach");
            yield return Until(() => player.CanUseComputer, "desktop becomes ready");
            Check(readyEvents == 1 && player.UiOpen && !player.IsTransitioning, "one ready event opens desktop");
            Check(!controller.enabled && Cursor.visible && Cursor.lockState == CursorLockMode.None, "desktop cursor and movement gates");
            Check(Vector3.Distance(player.View.transform.position, display.GetFocusedPosition(player.View)) < 0.002f,
                "camera reaches real display framing position");
            Check(Quaternion.Angle(player.View.transform.rotation, display.ViewRotation) < 0.1f, "camera faces real display");
            Check(desktop.renderMode == RenderMode.ScreenSpaceOverlay && raycaster.enabled, "focused desktop accepts pointer input");
            display.Mount(desktop); player.Stand(); player.Stand();
            Check(!player.CanUseComputer && !player.UiOpen && !raycaster.enabled, "exit immediately stops desktop input");
            yield return Until(() => !player.IsSeated, "normal exit returns to first person");
            Check(closedEvents == 1 && controller.enabled, "one close event restores movement");
            Check(Vector3.Distance(player.transform.position, startPosition) < 0.003f
                && Quaternion.Angle(player.transform.rotation, startRotation) < 0.1f, "standing restores body pose");
            Check(Vector3.Distance(player.View.transform.position, eyePosition) < 0.003f
                && Quaternion.Angle(player.View.transform.rotation, eyeRotation) < 0.1f, "standing restores first-person eye pose");
            Check(player.View.transform.parent == player.transform && !player.UiOpen && !Cursor.visible,
                "exit remains first person with captured cursor");

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Check(player.Sit(seat, focus, display), "enter interruption case");
                yield return Delay(attempt == 0 ? 0.15f : 0.85f);
                Check(player.IsTransitioning && !player.CanUseComputer, "interrupt before desktop is active");
                Vector3 beforeExit = player.View.transform.position;
                Quaternion beforeExitRotation = player.View.transform.rotation;
                player.Stand(); player.Stand();
                Check(Vector3.Distance(player.View.transform.position, beforeExit) < 0.001f
                    && Quaternion.Angle(player.View.transform.rotation, beforeExitRotation) < 0.1f,
                    "interrupted exit starts from current camera pose");
                yield return Until(() => !player.IsSeated, "interrupted exit completes");
                Check(controller.enabled && !player.CanUseComputer && !player.UiOpen, "interrupted exit restores walking gates");
                Check(readyEvents == 1 && closedEvents == attempt + 2, "interrupt cannot open desktop or duplicate close event");
            }

            Check(player.Sit(seat, focus, display), "enter teardown case");
            yield return Delay(0.15f);
            player.enabled = false;
            Check(!player.IsSeated && controller.enabled && !player.CanUseComputer, "disable during approach restores controller");
            Check(readyEvents == 1 && closedEvents == 4, "teardown closes the active session once");
            Check(Vector3.Distance(player.View.transform.position, eyePosition) < 0.01f, "teardown restores first-person camera");
            player.enabled = true;
            Check(!player.IsSeated && !player.UiOpen && !Cursor.visible && Cursor.lockState == CursorLockMode.Locked,
                "reenabling restores first-person cursor capture");
            Check(assertions > 0, "checks actually executed");
        }

        private static Await Until(Func<bool> done, string description)
        { return new Await { done = done, description = description, deadline = EditorApplication.timeSinceStartup + 5d }; }
        private static Await Delay(float seconds)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            return Until(() => EditorApplication.timeSinceStartup >= end, "timed camera sample");
        }
        private static void Check(bool valid, string description)
        { assertions++; if (!valid) throw new Exception("FAIL: " + description); }

        private static void Tick()
        {
            if (!Application.isPlaying) { Finish("Play Mode ended before checks completed."); return; }
            if (EditorApplication.timeSinceStartup > suiteDeadline)
            { Finish("Computer checks timed out; resume Play Mode and keep the Game view focused."); return; }
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (pending != null)
                {
                    if (!pending.done())
                    {
                        if (EditorApplication.timeSinceStartup > pending.deadline) throw new TimeoutException(pending.description);
                        return;
                    }
                    pending = null;
                }
                if (!sequence.MoveNext()) { Finish(assertions == 0 ? "No assertions ran." : null); return; }
                pending = sequence.Current as Await;
            }
            catch (Exception error) { Finish(error.ToString()); }
        }

        private static void PlayModeChanged(PlayModeStateChange change)
        { if (change == PlayModeStateChange.ExitingPlayMode) Finish("Play Mode ended before checks completed."); }

        private static void Finish(string failure)
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            sequence = null; pending = null;
            if (fixture != null) fixture.SetActive(false);
            if (desktop != null) { desktop.gameObject.SetActive(false); Object.Destroy(desktop.gameObject); }
            if (fixture != null) Object.Destroy(fixture);
            foreach (Behaviour behaviour in paused) if (behaviour != null) behaviour.enabled = true;
            paused.Clear();
            Cursor.lockState = previousLock; Cursor.visible = previousCursor;
            var report = new Report { passed = failure == null, assertions = assertions,
                failure = failure ?? "", unity_version = Application.unityVersion };
            string path = Path.Combine(Application.temporaryCachePath, "SignalDebtComputerPlayModeChecks.json");
            try { File.WriteAllText(path, JsonUtility.ToJson(report, true)); }
            catch (Exception error) { Debug.LogWarning("Could not write computer-check report: " + error.Message); }
            string result = (report.passed ? "PASS" : "FAIL") + ": " + assertions
                + " real Unity computer checks. Report: " + path;
            if (report.passed) Debug.Log(result); else Debug.LogError(result + "\n" + failure);
        }
    }
}
#endif
