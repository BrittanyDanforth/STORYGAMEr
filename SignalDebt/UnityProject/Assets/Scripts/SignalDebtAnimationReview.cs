using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SignalDebt
{
    // A separate art-review scene. This component has no reference to game saves, police AI, or doors.
    public sealed class SignalDebtAnimationReview : MonoBehaviour
    {
        [Serializable]
        public sealed class Actor
        {
            public string label;
            public Animator animator;
            public AnimationClip[] clips;
            [NonSerialized] public PlayableGraph graph;
            [NonSerialized] public AnimationMixerPlayable mixer;
            [NonSerialized] public AnimationClipPlayable[] players;
            [NonSerialized] public int selected;
            [NonSerialized] public float[] weights;
            [NonSerialized] public bool valid;
        }

        public Actor swat = new Actor(), patrol = new Actor();
        public Camera reviewCamera;
        private bool paused;
        private float speed = 1f;
        private GUIStyle titleStyle, textStyle, buttonStyle;
        private Vector2 panelScroll;
        private int focusedActor;
        private Vector3 cameraTarget = new Vector3(0f, 0.95f, 0f);
        private float orbitYaw = 0f, orbitPitch = 10f, orbitDistance = 6.5f;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Setup(swat); Setup(patrol);
            Play(patrol, 0);
            UpdateCamera();
        }

        private void Setup(Actor actor)
        {
            if (actor == null || actor.animator == null || actor.clips == null || actor.clips.Length == 0) return;
            foreach (AnimationClip clip in actor.clips) if (clip == null) return;
            actor.animator.applyRootMotion = false;
            actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            actor.animator.runtimeAnimatorController = null;
            actor.graph = PlayableGraph.Create("Animation review / " + actor.label);
            actor.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            actor.mixer = AnimationMixerPlayable.Create(actor.graph, actor.clips.Length);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(actor.graph, actor.label, actor.animator);
            output.SetSourcePlayable(actor.mixer);
            actor.players = new AnimationClipPlayable[actor.clips.Length];
            actor.weights = new float[actor.clips.Length];
            for (int i = 0; i < actor.clips.Length; i++)
            {
                actor.players[i] = AnimationClipPlayable.Create(actor.graph, actor.clips[i]);
                actor.players[i].SetApplyFootIK(false);
                actor.players[i].SetApplyPlayableIK(false);
                actor.graph.Connect(actor.players[i], 0, actor.mixer, i);
                actor.weights[i] = i == 0 ? 1f : 0f;
                actor.mixer.SetInputWeight(i, actor.weights[i]);
                actor.players[i].SetTime(0d);
                actor.players[i].SetSpeed(0d);
            }
            actor.selected = 0; actor.valid = true;
            actor.graph.Play(); actor.graph.Evaluate(0f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) SetPaused(!paused);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Play(swat, 0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Play(patrol, 1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Play(patrol, 2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Play(patrol, 0);
            if (Input.GetMouseButton(1))
            {
                orbitYaw += Input.GetAxisRaw("Mouse X") * 3f;
                orbitPitch = Mathf.Clamp(orbitPitch - Input.GetAxisRaw("Mouse Y") * 2f, -5f, 65f);
            }
            float uiScale = Mathf.Clamp(Screen.height / 900f, 0.65f, 1.6f);
            if (Input.mousePosition.x > 440f * uiScale)
                orbitDistance = Mathf.Clamp(orbitDistance - Input.GetAxisRaw("Mouse ScrollWheel") * 3f, 2f, 11f);
            UpdateCamera();
            Tick(swat); Tick(patrol);
        }

        private void UpdateCamera()
        {
            if (reviewCamera == null) return;
            Quaternion orbit = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            reviewCamera.transform.position = cameraTarget + orbit * Vector3.back * orbitDistance;
            reviewCamera.transform.LookAt(cameraTarget);
        }

        private void Tick(Actor actor)
        {
            if (actor == null || !actor.valid) return;
            float dt = paused ? 0f : Time.unscaledDeltaTime * speed;
            float sum = 0f;
            for (int i = 0; i < actor.clips.Length; i++)
            {
                actor.weights[i] = Mathf.MoveTowards(actor.weights[i], i == actor.selected ? 1f : 0f, dt / 0.2f);
                sum += actor.weights[i];
                double time = actor.players[i].GetTime();
                bool active = actor.weights[i] > 0f || i == actor.selected;
                if (active && actor.players[i].GetSpeed() > 0d)
                {
                    double end = Math.Max(0d, actor.clips[i].length - 0.0001d);
                    if (!actor.clips[i].isLooping && time + dt >= end)
                    { actor.players[i].SetTime(end); actor.players[i].SetSpeed(0d); }
                }
                if (!active) actor.players[i].SetSpeed(0d);
            }
            for (int i = 0; i < actor.clips.Length; i++) actor.mixer.SetInputWeight(i, actor.weights[i] / Mathf.Max(sum, 0.001f));
            actor.graph.Evaluate(dt);
        }

        private void Play(Actor actor, int index)
        {
            if (actor == null || !actor.valid || index < 0 || index >= actor.clips.Length) return;
            actor.selected = index;
            actor.players[index].SetTime(0d); actor.players[index].SetSpeed(1d);
            paused = false;
        }

        private void Scrub(Actor actor, float normalized)
        {
            if (!actor.valid) return;
            paused = true;
            for (int i = 0; i < actor.clips.Length; i++)
            {
                actor.weights[i] = i == actor.selected ? 1f : 0f;
                actor.mixer.SetInputWeight(i, actor.weights[i]);
            }
            actor.players[actor.selected].SetTime(normalized * Math.Max(0d, actor.clips[actor.selected].length - 0.0001d));
            actor.players[actor.selected].SetSpeed(1d);
            actor.graph.Evaluate(0f);
        }

        private void SetPaused(bool value)
        {
            paused = value;
            if (paused) return;
            ResumeSelected(swat); ResumeSelected(patrol);
        }

        private static void ResumeSelected(Actor actor)
        {
            if (actor == null || !actor.valid) return;
            int index = actor.selected;
            if (!actor.clips[index].isLooping && actor.players[index].GetTime() >= actor.clips[index].length - 0.0002d)
                actor.players[index].SetTime(0d);
            actor.players[index].SetSpeed(1d);
        }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold, wordWrap = true };
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 17, fixedHeight = 35f };
            }
            float scale = Mathf.Clamp(Screen.height / 900f, 0.65f, 1.6f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(20f, 20f, 420f, Screen.height / scale - 40f), GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            GUILayout.Label("SIGNAL DEBT / ANIMATION REVIEW", titleStyle);
            GUILayout.Label("Visual clip playback only. No police decisions or apartment door changes. Right-drag orbits; wheel zooms; Space pauses.", textStyle);
            GUILayout.Space(8f);
            DrawActor(swat);
            DrawActor(patrol);
            if (GUILayout.Button(paused ? "Resume playback" : "Pause playback", buttonStyle)) SetPaused(!paused);
            GUILayout.Label("Playback speed " + speed.ToString("F2") + "×", textStyle);
            speed = GUILayout.HorizontalSlider(speed, 0.1f, 1.5f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Focus SWAT", buttonStyle)) FocusActor(swat, 1);
            if (GUILayout.Button("Focus patrol", buttonStyle)) FocusActor(patrol, 2);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Both actors", buttonStyle))
            { focusedActor = 0; cameraTarget = new Vector3(0f, 0.95f, 0f); orbitYaw = 0f; orbitPitch = 10f; orbitDistance = 6.5f; }
            if (GUILayout.Button("Front view", buttonStyle)) { orbitYaw = 0f; orbitPitch = 10f; }
            if (GUILayout.Button("Side view", buttonStyle))
            {
                if (focusedActor == 0) FocusActor(swat, 1);
                // Inspect from the outside of the pair so one officer cannot obscure the other.
                orbitYaw = focusedActor == 2 ? -90f : 90f; orbitPitch = 10f;
            }
            GUILayout.Label("Watch knee/elbow volume, sole placement, grip alignment, contact, recovery, and cross-fade. Imported assets still need Play Mode validation.", textStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private void FocusActor(Actor actor, int index)
        {
            if (actor == null || actor.animator == null) return;
            focusedActor = index;
            cameraTarget = actor.animator.transform.position + Vector3.up * 0.95f;
            orbitYaw = 0f; orbitPitch = 10f; orbitDistance = 3.8f;
        }

        private void DrawActor(Actor actor)
        {
            GUILayout.Space(8f);
            GUILayout.Label(actor.label ?? "Actor", titleStyle);
            if (!actor.valid) { GUILayout.Label("Missing Animator or clips. Rebuild the review scene after importing assets.", textStyle); return; }
            for (int i = 0; i < actor.clips.Length; i++)
            {
                int index = i;
                if (GUILayout.Button(actor.clips[i].name.Replace('_', ' '), buttonStyle)) Play(actor, index);
            }
            float length = Mathf.Max(actor.clips[actor.selected].length, 0.001f);
            float time = (float)actor.players[actor.selected].GetTime();
            float progress = actor.clips[actor.selected].isLooping ? Mathf.Repeat(time / length, 1f) : Mathf.Clamp01(time / length);
            GUILayout.Label("Time " + (progress * length).ToString("F2") + " / " + length.ToString("F2") + " s · 0.20 s blend", textStyle);
            float scrubbed = GUILayout.HorizontalSlider(progress, 0f, 1f);
            if (Mathf.Abs(scrubbed - progress) > 0.002f) Scrub(actor, scrubbed);
        }

        private void OnDestroy()
        {
            if (swat != null && swat.graph.IsValid()) swat.graph.Destroy();
            if (patrol != null && patrol.graph.IsValid()) patrol.graph.Destroy();
        }
    }
}
