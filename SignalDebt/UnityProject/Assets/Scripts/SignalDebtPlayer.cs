using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalDebt
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SignalDebtPlayer : MonoBehaviour
    {
        private const float StandingHeight = 1.78f, CrouchedHeight = 1.24f;
        public Camera View { get; private set; }
        public bool IsSeated { get { return computerSession.IsAtDesk; } }
        public bool IsTransitioning { get { return computerSession.IsTransitioning; } }
        public bool CanUseComputer { get { return computerSession.CanUseDesktop; } }
        public event Action ComputerReady;
        public event Action ComputerClosed;
        public bool UiOpen { get; private set; }
        public float lookSensitivity = 1.7f;
        public string RigSummary { get; private set; }

        private sealed class Limb
        {
            public Transform upper, lower, end;
            public Quaternion upperRest, lowerRest, endRest, endPlayerRotation;
            public Vector3 restTarget, restEndDirection, restPalmNormal;
            public float upperLength, lowerLength;
            public void Reset()
            {
                upper.localRotation = upperRest;
                lower.localRotation = lowerRest;
                end.localRotation = endRest;
            }
        }

        private CharacterController controller;
        private Transform body, spine, head, eyeView;
        private Quaternion spineRest, headRest;
        private Vector3 bodyRestPosition;
        private Limb leftLeg, rightLeg, leftArm, rightArm;
        private float pitch, verticalSpeed, stride, gaitWeight, crouchBlend;
        private float standingEyeHeight = 1.61405f;
        private float cameraGaitCompensation;
        private Vector3 moveDirection, beforeSeatPosition, seatedLeftHand, seatedRightHand;
        private Quaternion beforeSeatRotation;
        private float beforeSeatPitch, beforeSeatHeight;
        private readonly SignalDebtComputerSession computerSession = new SignalDebtComputerSession();
        private Pose beforeSeatView, seatedView, focusedView, exitView, standStartView;
        private Pose seatedBody, standStartBody;
        private float seatBlend, standStartBlend;

        public void Initialize(Vector3 position, Quaternion rotation, GameObject bodyPrefab = null)
        {
            // Ignore Raycast still collides with the room; focus and headroom queries exclude it.
            gameObject.layer = 2;
            controller = GetComponent<CharacterController>();
            controller.height = StandingHeight;
            controller.radius = 0.23f;
            controller.center = Vector3.up * (StandingHeight * 0.5f);
            controller.stepOffset = 0.24f;
            controller.skinWidth = 0.025f;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
            controller.enabled = true;
            var cameraObject = new GameObject("Player Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            View = cameraObject.GetComponent<Camera>();
            View.nearClipPlane = 0.035f;
            View.farClipPlane = 120f;
            View.fieldOfView = 76f;
            View.backgroundColor = new Color(0.018f, 0.026f, 0.042f);
            cameraObject.tag = "MainCamera";
            BuildBody(bodyPrefab);
            bodyRestPosition = body.localPosition;
            if (spine != null) spineRest = spine.localRotation;
            if (head != null) headRest = head.localRotation;
            PoseBody();
            UpdateEyeView();
            SetUi(false);
        }

        private void Update()
        {
            if (View == null) return;
            if (IsSeated) { UpdateComputer(); return; }
            if (UiOpen) return;
            float yaw = Input.GetAxisRaw("Mouse X") * lookSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * lookSensitivity, -75f, 86f);
            transform.Rotate(0f, yaw, 0f);
            bool crouching = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            if (!crouching && controller.height < StandingHeight - 0.02f)
            {
                Vector3 bottom = transform.position + Vector3.up * (controller.height - controller.radius);
                Vector3 top = transform.position + Vector3.up * (StandingHeight - controller.radius);
                crouching = Physics.CheckCapsule(bottom, top, controller.radius, ~(1 << 2), QueryTriggerInteraction.Ignore);
            }
            float targetHeight = crouching ? CrouchedHeight : StandingHeight;
            controller.height = Mathf.MoveTowards(controller.height, targetHeight, Time.deltaTime * 2.8f);
            controller.center = Vector3.up * (controller.height * 0.5f);
            crouchBlend = Mathf.InverseLerp(StandingHeight, CrouchedHeight, controller.height);
            // The eye follows the actual posed head; no old fixed crouch camera offset is applied.
            Vector2 raw = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
            bool running = Input.GetKey(KeyCode.LeftShift) && !crouching;
            float speed = crouching ? 1.35f : running ? 3.9f : 2.55f;
            Vector3 motion = (transform.right * raw.x + transform.forward * raw.y) * speed;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed -= 18f * Time.deltaTime;
            controller.Move((motion + Vector3.up * verticalSpeed) * Time.deltaTime);
            Vector3 localVelocity = transform.InverseTransformDirection(controller.velocity); localVelocity.y = 0f;
            float moving = Mathf.Clamp01(localVelocity.magnitude / Mathf.Max(speed, 0.01f));
            gaitWeight = Mathf.MoveTowards(gaitWeight, moving, Time.deltaTime * 6f);
            if (moving > 0.05f) moveDirection = localVelocity.normalized;
            stride += Time.deltaTime * (crouching ? 9f : running ? 15f : 12f) * moving;
            PoseBody(running);
            UpdateEyeView();
            if (transform.position.y < -8f) controller.Move(Vector3.up * 10f);
        }

        public void SetUi(bool open)
        {
            if (open && IsTransitioning) return;
            UiOpen = open;
            bool releaseCursor = open || !Application.isFocused;
            Cursor.lockState = releaseCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = releaseCursor;
        }

        public bool Sit(Transform seat, Transform focus, SignalDebtComputerDisplay display = null)
        {
            if (seat == null || focus == null || View == null || !computerSession.Enter()) return false;
            beforeSeatPosition = transform.position; beforeSeatRotation = transform.rotation;
            beforeSeatPitch = pitch; beforeSeatHeight = controller.height;
            beforeSeatView = new Pose(View.transform.position, View.transform.rotation);
            SetUi(false);
            controller.enabled = false;
            transform.SetPositionAndRotation(seat.position, Quaternion.Euler(0f, seat.eulerAngles.y, 0f));
            Vector3 horizontal = focus.position - transform.position; horizontal.y = 0f;
            if (horizontal.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(horizontal, Vector3.up);
            seatedBody = new Pose(transform.position, transform.rotation);
            Vector3 screenCenter = display != null ? display.ScreenCenter : focus.position;
            Vector3 seatedEye = transform.TransformPoint(new Vector3(0f, 1.18f, 0.34f));
            seatedView = new Pose(seatedEye, Quaternion.LookRotation(screenCenter - seatedEye, Vector3.up));
            focusedView = display != null
                ? new Pose(display.GetFocusedPosition(View), display.ViewRotation)
                : new Pose(Vector3.Lerp(seatedEye, screenCenter, 0.7f), seatedView.rotation);
            // Focus is the display center. The keyboard front lies lower and toward the chair.
            Vector3 keyboard = focus.position - transform.forward * 0.26f - Vector3.up * 0.111f;
            Transform leftTarget = Find(focus.root, "KeyboardWristLeft"), rightTarget = Find(focus.root, "KeyboardWristRight");
            seatedLeftHand = transform.InverseTransformPoint(leftTarget != null ? leftTarget.position : keyboard - transform.right * 0.105f);
            seatedRightHand = transform.InverseTransformPoint(rightTarget != null ? rightTarget.position : keyboard + transform.right * 0.105f);
            gaitWeight = 0f; seatBlend = 0f;
            transform.SetPositionAndRotation(beforeSeatPosition, beforeSeatRotation);
            PoseBody();
            // Moving the seated body must not teleport its child camera. Start from
            // the exact first-person view, then sit before approaching the display.
            View.transform.SetPositionAndRotation(beforeSeatView.position, beforeSeatView.rotation);
            return true;
        }

        public void Stand()
        {
            bool wasSitting = computerSession.Phase == ComputerPhase.Sitting;
            if (!computerSession.Exit()) return;
            exitView = new Pose(View.transform.position, View.transform.rotation);
            standStartView = wasSitting ? exitView : seatedView;
            standStartBody = wasSitting ? new Pose(transform.position, transform.rotation) : seatedBody;
            standStartBlend = wasSitting ? seatBlend : 1f;
            SetUi(false);
        }

        private void UpdateComputer()
        {
            ComputerPhase previous = computerSession.Phase;
            computerSession.Advance(Time.unscaledDeltaTime);
            float t = computerSession.EasedProgress;
            if (computerSession.IsAtDesk && (computerSession.IsTransitioning || previous != ComputerPhase.Desktop))
            {
                if (computerSession.Phase == ComputerPhase.Sitting)
                {
                    seatBlend = t;
                    transform.SetPositionAndRotation(Vector3.Lerp(beforeSeatPosition, seatedBody.position, t),
                        Quaternion.Slerp(beforeSeatRotation, seatedBody.rotation, t));
                }
                else if (computerSession.Phase == ComputerPhase.Standing)
                {
                    seatBlend = standStartBlend * (1f - t);
                    transform.SetPositionAndRotation(Vector3.Lerp(standStartBody.position, beforeSeatPosition, t),
                        Quaternion.Slerp(standStartBody.rotation, beforeSeatRotation, t));
                }
                else
                {
                    seatBlend = 1f;
                    transform.SetPositionAndRotation(seatedBody.position, seatedBody.rotation);
                }
                PoseBody();
            }
            switch (computerSession.Phase)
            {
                case ComputerPhase.Sitting: BlendView(beforeSeatView, seatedView, t); break;
                case ComputerPhase.Focusing: BlendView(seatedView, focusedView, t); break;
                case ComputerPhase.Desktop:
                    BlendView(focusedView, focusedView, 1f);
                    if (previous != ComputerPhase.Desktop)
                    {
                        SetUi(true);
                        ComputerReady?.Invoke();
                    }
                    break;
                case ComputerPhase.Unfocusing: BlendView(exitView, seatedView, t); break;
                case ComputerPhase.Standing: BlendView(standStartView, beforeSeatView, t); break;
                case ComputerPhase.Walking: FinishStanding(); break;
            }
        }

        private void BlendView(Pose from, Pose to, float t)
        {
            View.transform.SetPositionAndRotation(Vector3.Lerp(from.position, to.position, t),
                Quaternion.Slerp(from.rotation, to.rotation, t));
        }

        private void FinishStanding()
        {
            computerSession.Reset();
            seatBlend = 0f;
            transform.SetPositionAndRotation(beforeSeatPosition, beforeSeatRotation);
            pitch = beforeSeatPitch;
            controller.height = beforeSeatHeight; controller.center = Vector3.up * (beforeSeatHeight * 0.5f);
            crouchBlend = Mathf.InverseLerp(StandingHeight, CrouchedHeight, beforeSeatHeight);
            controller.enabled = true; verticalSpeed = -2f;
            PoseBody(); UpdateEyeView(); SetUi(false);
            ComputerClosed?.Invoke();
        }

        private void OnDisable()
        {
            if (controller != null && IsSeated) FinishStanding();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CancelComputerImmediately()
        {
            if (controller != null && IsSeated) FinishStanding();
        }

        private void OnEnable()
        {
            if (View != null) SetUi(UiOpen);
        }

        private void PoseBody(bool running = false)
        {
            if (body == null) return;
            leftArm.Reset(); rightArm.Reset(); leftLeg.Reset(); rightLeg.Reset();
            if (spine != null) spine.localRotation = spineRest;
            if (head != null) head.localRotation = headRest;
            cameraGaitCompensation = 0f;
            float pelvisDrop = Mathf.Lerp(crouchBlend * 0.42f, 0.40f, seatBlend);
            float pelvisForward = Mathf.Lerp(-crouchBlend * 0.07f, 0.25f, seatBlend);
            body.localPosition = bodyRestPosition + new Vector3(0f, -pelvisDrop, pelvisForward);
            if (spine != null)
            {
                Vector3 axis = spine.parent.InverseTransformDirection(transform.right);
                spine.localRotation = Quaternion.AngleAxis(Mathf.Lerp(crouchBlend * 15f, 26f, seatBlend), axis) * spineRest;
            }
            if (IsSeated)
            {
                Vector3 handRestOffset = new Vector3(0f, -crouchBlend * 0.42f, crouchBlend * 0.13f);
                Solve(leftLeg, leftLeg.restTarget + Vector3.forward * (0.48f * seatBlend), Vector3.forward, false);
                Solve(rightLeg, rightLeg.restTarget + Vector3.forward * (0.48f * seatBlend), Vector3.forward, false);
                Solve(leftArm, Vector3.Lerp(leftArm.restTarget + handRestOffset, seatedLeftHand, seatBlend), new Vector3(-0.7f, -0.3f, -0.3f), true);
                Solve(rightArm, Vector3.Lerp(rightArm.restTarget + handRestOffset, seatedRightHand, seatBlend), new Vector3(0.7f, -0.3f, -0.3f), true);
                return;
            }
            float length = Mathf.Lerp(running ? 0.37f : 0.31f, 0.21f, crouchBlend) * gaitWeight;
            float lift = Mathf.Lerp(running ? 0.105f : 0.065f, 0.045f, crouchBlend) * gaitWeight;
            Vector3 leftFootTarget = StepTarget(leftLeg, stride, length, lift);
            Vector3 rightFootTarget = StepTarget(rightLeg, stride + Mathf.PI, length, lift);
            // A nearly straight standing leg cannot reach a long step without lowering the pelvis.
            // Compute enough drop for both targets before IK, preserving planted sole height.
            float reachDrop = Mathf.Max(RequiredPelvisDrop(leftLeg, leftFootTarget), RequiredPelvisDrop(rightLeg, rightFootTarget));
            body.position -= Vector3.up * reachDrop;
            cameraGaitCompensation = reachDrop;
            Solve(leftLeg, leftFootTarget, Vector3.forward, false);
            Solve(rightLeg, rightFootTarget, Vector3.forward, false);
            float handSwing = Mathf.Sin(stride) * (running ? 0.15f : 0.095f) * gaitWeight;
            Vector3 handPosture = new Vector3(0f, -pelvisDrop - reachDrop + crouchBlend * 0.06f, crouchBlend * 0.13f);
            Solve(leftArm, leftArm.restTarget + handPosture - Vector3.forward * handSwing, new Vector3(-0.6f, -0.15f, -0.8f), false);
            Solve(rightArm, rightArm.restTarget + handPosture + Vector3.forward * handSwing, new Vector3(0.6f, -0.15f, -0.8f), false);
        }

        private void UpdateEyeView()
        {
            if (IsSeated || View == null || head == null || eyeView == null) return;
            // Pitch at the measured head joint. Counter the crouched spine lean so view pitch stays exact.
            Vector3 parentAxis = head.parent.InverseTransformDirection(transform.right);
            head.localRotation = Quaternion.AngleAxis(pitch - crouchBlend * 15f, parentAxis) * headRest;
            View.transform.position = eyeView.position + Vector3.up * cameraGaitCompensation;
            View.transform.rotation = transform.rotation * Quaternion.Euler(pitch, 0f, 0f);
            // Cancel only the procedural stride reach-drop to avoid forced camera bob; crouch and look anatomy remain.
        }

        private Vector3 StepTarget(Limb leg, float phase, float distance, float lift)
        {
            Vector3 target = leg.restTarget - moveDirection * (Mathf.Cos(phase) * distance);
            target.y += Mathf.Max(0f, Mathf.Sin(phase)) * lift;
            return target;
        }

        private float RequiredPelvisDrop(Limb limb, Vector3 localTarget)
        {
            Vector3 offset = transform.TransformPoint(localTarget) - limb.upper.position;
            float horizontalSquared = offset.x * offset.x + offset.z * offset.z;
            float reach = limb.upperLength + limb.lowerLength - 0.004f;
            float allowedVertical = Mathf.Sqrt(Mathf.Max(0f, reach * reach - horizontalSquared));
            return Mathf.Max(0f, -offset.y - allowedVertical);
        }

        private void Solve(Limb limb, Vector3 localTarget, Vector3 localBendDirection, bool handOnDesk)
        {
            Vector3 root = limb.upper.position, target = transform.TransformPoint(localTarget);
            Vector3 offset = target - root;
            float rawDistance = offset.magnitude;
            Vector3 direction = rawDistance > 0.0001f ? offset / rawDistance : -transform.up;
            float distance = Mathf.Clamp(rawDistance, Mathf.Abs(limb.upperLength - limb.lowerLength) + 0.002f,
                limb.upperLength + limb.lowerLength - 0.002f);
            Vector3 pole = Vector3.ProjectOnPlane(transform.TransformDirection(localBendDirection), direction).normalized;
            if (pole.sqrMagnitude < 0.01f) pole = Vector3.ProjectOnPlane(transform.forward, direction).normalized;
            if (pole.sqrMagnitude < 0.01f) pole = Vector3.ProjectOnPlane(transform.right, direction).normalized;
            float along = (limb.upperLength * limb.upperLength - limb.lowerLength * limb.lowerLength + distance * distance) / (2f * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0f, limb.upperLength * limb.upperLength - along * along));
            Vector3 joint = root + direction * along + pole * bend;
            Vector3 reachableTarget = root + direction * distance;
            limb.upper.rotation = Quaternion.FromToRotation(limb.lower.position - root, joint - root) * limb.upper.rotation;
            limb.lower.rotation = Quaternion.FromToRotation(limb.end.position - limb.lower.position, reachableTarget - limb.lower.position) * limb.lower.rotation;
            // Counter-rotate the ankle/wrist rather than allowing a thigh/shoulder swing to tip it.
            Quaternion endRotation = transform.rotation * limb.endPlayerRotation;
            if (handOnDesk)
            {
                Quaternion restFrame = Quaternion.LookRotation(transform.TransformDirection(limb.restEndDirection), transform.TransformDirection(limb.restPalmNormal));
                Quaternion typingFrame = Quaternion.LookRotation(transform.forward, Vector3.up);
                endRotation = Quaternion.Slerp(endRotation, typingFrame * Quaternion.Inverse(restFrame) * endRotation, seatBlend);
            }
            limb.end.rotation = endRotation;
        }

        private Limb Capture(Transform upper, Transform lower, Transform end)
        {
            if (upper == null || lower == null || end == null) return null;
            var limb = new Limb
            {
                upper = upper, lower = lower, end = end,
                upperRest = upper.localRotation, lowerRest = lower.localRotation, endRest = end.localRotation,
                restTarget = transform.InverseTransformPoint(end.position),
                restEndDirection = transform.InverseTransformDirection(end.position - lower.position).normalized,
                restPalmNormal = Vector3.forward,
                endPlayerRotation = Quaternion.Inverse(transform.rotation) * end.rotation,
                upperLength = Vector3.Distance(upper.position, lower.position),
                lowerLength = Vector3.Distance(lower.position, end.position)
            };
            return limb.upperLength > 0.05f && limb.lowerLength > 0.05f ? limb : null;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name || child.name.EndsWith("_" + name, StringComparison.Ordinal)) return child;
            return null;
        }

        private void BuildBody(GameObject prefab)
        {
            if (prefab != null && TryImportedBody(prefab)) return;
            standingEyeHeight = 1.61405f;
            body = new GameObject("Articulated first person fallback").transform; body.SetParent(transform, false);
            Transform hips = Joint("Hips", body, new Vector3(0f, 0.89f, 0f));
            spine = Joint("Spine", hips, new Vector3(0f, 0.14f, 0f));
            head = Joint("Head", spine, new Vector3(0f, 0.452f, 0f));
            eyeView = Joint("EyeView", head, new Vector3(0f, 0.13205f, 0.09937f));
            Material sleeve = MakeMaterial("Hoodie sleeves", new Color(0.09f, 0.15f, 0.19f));
            Material trousers = MakeMaterial("Trousers", new Color(0.055f, 0.065f, 0.08f));
            Material skin = MakeMaterial("Hands", new Color(0.52f, 0.31f, 0.21f));
            Material shoes = MakeMaterial("Shoes", new Color(0.022f, 0.027f, 0.035f));
            leftLeg = FallbackLimb(hips, "Left", true, -1f, trousers, shoes);
            rightLeg = FallbackLimb(hips, "Right", true, 1f, trousers, shoes);
            leftArm = FallbackLimb(spine, "Left", false, -1f, sleeve, skin);
            rightArm = FallbackLimb(spine, "Right", false, 1f, sleeve, skin);
            RigSummary = "Articulated fallback; ankle height 0.11 m; two segments per limb; body scale 1";
        }

        private bool TryImportedBody(GameObject prefab)
        {
            GameObject wrapper = new GameObject("First person articulated body"); wrapper.transform.SetParent(transform, false);
            GameObject model = Instantiate(prefab, wrapper.transform, false);
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            Transform origin = Find(model.transform, "BodyOrigin"), forwardAnchor = Find(model.transform, "BodyForward"), upAnchor = Find(model.transform, "BodyUp");
            Vector3 originPoint = origin != null ? origin.position : model.transform.position;
            Vector3 up = upAnchor != null ? upAnchor.position - originPoint : transform.up;
            Vector3 forward = forwardAnchor != null ? forwardAnchor.position - originPoint : Vector3.zero;
            if (forward.sqrMagnitude < 0.001f)
            {
                Transform foot = Find(model.transform, "LeftFoot"), toes = Find(model.transform, "LeftToes");
                if (foot != null && toes != null) forward = Vector3.ProjectOnPlane(toes.position - foot.position, up);
            }
            if (forward.sqrMagnitude > 0.001f && up.sqrMagnitude > 0.001f)
                wrapper.transform.rotation = transform.rotation * Quaternion.Inverse(Quaternion.LookRotation(forward, up)) * wrapper.transform.rotation;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { Destroy(wrapper); return false; }
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float height = bounds.size.y;
            if (height < 1.55f || height > 1.95f)
            {
                Debug.LogWarning("PlayerBody height " + height.ToString("F3") + " m violates the 1.55–1.95 m rig contract; using fallback.");
                wrapper.SetActive(false); Destroy(wrapper); return false;
            }
            // Ground the measured shoe sole, keeping the artist's uniform scale unchanged.
            wrapper.transform.position += Vector3.up * (transform.position.y - bounds.min.y);
            Transform root = model.transform;
            leftArm = Capture(Find(root, "LeftUpperArm"), Find(root, "LeftLowerArm"), Find(root, "LeftHand"));
            rightArm = Capture(Find(root, "RightUpperArm"), Find(root, "RightLowerArm"), Find(root, "RightHand"));
            leftLeg = Capture(Find(root, "LeftUpperLeg"), Find(root, "LeftLowerLeg"), Find(root, "LeftFoot"));
            rightLeg = Capture(Find(root, "RightUpperLeg"), Find(root, "RightLowerLeg"), Find(root, "RightFoot"));
            if (leftArm == null || rightArm == null || leftLeg == null || rightLeg == null)
            {
                wrapper.SetActive(false); Destroy(wrapper);
                Debug.LogWarning("PlayerBody needs upper/lower/end joints for all four limbs; using articulated fallback."); return false;
            }
            if (transform.InverseTransformPoint(leftArm.upper.position).x >= 0f || transform.InverseTransformPoint(rightArm.upper.position).x <= 0f
                || transform.InverseTransformPoint(leftLeg.upper.position).x >= 0f || transform.InverseTransformPoint(rightLeg.upper.position).x <= 0f)
            {
                wrapper.SetActive(false); Destroy(wrapper);
                Debug.LogWarning("PlayerBody anatomical left/right contract failed after forward alignment; re-export the rig. Using fallback."); return false;
            }
            CapturePalmDirection(leftArm, Find(root, "PalmTip_L"), Find(root, "PalmBack_L"));
            CapturePalmDirection(rightArm, Find(root, "PalmTip_R"), Find(root, "PalmBack_R"));
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 2;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) { collider.enabled = false; Destroy(collider); }
            foreach (Renderer renderer in renderers)
            {
                // Keep the clothed waist so the trouser sections do not become open tubes.
                // Player-only sleeve closures are exported within the visible arm regions.
                bool visible = renderer.name == "Player_Hips"
                    || renderer.name.StartsWith("Player_Arm_", StringComparison.Ordinal) || renderer.name.StartsWith("Player_Leg_", StringComparison.Ordinal)
                    || renderer.transform.IsChildOf(leftArm.upper) || renderer.transform.IsChildOf(rightArm.upper)
                    || renderer.transform.IsChildOf(leftLeg.upper) || renderer.transform.IsChildOf(rightLeg.upper);
                renderer.shadowCastingMode = visible ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            body = wrapper.transform; spine = Find(root, "Spine"); head = Find(root, "Head");
            eyeView = Find(root, "EyeView");
            if (head == null)
            {
                wrapper.SetActive(false); Destroy(wrapper);
                Debug.LogWarning("PlayerBody requires its measured Head pivot; using articulated fallback."); return false;
            }
            if (eyeView == null || Vector3.Distance(head.position, eyeView.position) < 0.08f || Vector3.Distance(head.position, eyeView.position) > 0.28f)
            {
                // Backward-compatible source measurement: Head z1.56, pupil z1.699/y−.1046, player scale .95.
                eyeView = new GameObject("EyeView measured fallback").transform;
                eyeView.SetParent(head, true);
                eyeView.position = head.position + transform.up * 0.13205f + transform.forward * 0.09937f;
            }
            else if (!eyeView.IsChildOf(head)) eyeView.SetParent(head, true);
            standingEyeHeight = transform.InverseTransformPoint(eyeView.position).y;
            RigSummary = string.Format("PlayerBody height {0:F3} m; grounded soles; eye {1:F3} m; arm {2:F3}+{3:F3} m; leg {4:F3}+{5:F3} m; uniform scale preserved",
                height, standingEyeHeight, leftArm.upperLength, leftArm.lowerLength, leftLeg.upperLength, leftLeg.lowerLength);
            Debug.Log(RigSummary); return true;
        }

        private void CapturePalmDirection(Limb arm, Transform marker, Transform backMarker)
        {
            if (marker == null) return;
            Vector3 direction = marker.position - arm.end.position;
            // Invalid/export-stale empties must not twist the wrist toward the body origin.
            if (direction.magnitude >= 0.04f && direction.magnitude <= 0.18f)
                arm.restEndDirection = transform.InverseTransformDirection(direction).normalized;
            if (backMarker == null) return;
            Vector3 normal = backMarker.position - arm.end.position;
            if (normal.magnitude >= 0.025f && normal.magnitude <= 0.15f)
            {
                Vector3 candidate = Vector3.ProjectOnPlane(transform.InverseTransformDirection(normal), arm.restEndDirection).normalized;
                if (candidate.sqrMagnitude > 0.5f) arm.restPalmNormal = candidate;
            }
        }

        private Limb FallbackLimb(Transform parent, string side, bool leg, float sign, Material cloth, Material endMaterial)
        {
            float upperLength = leg ? 0.39f : 0.25f, lowerLength = leg ? 0.39f : 0.25f;
            Transform upper = Joint(side + (leg ? "UpperLeg" : "UpperArm"), parent,
                leg ? new Vector3(sign * 0.105f, 0f, 0f) : new Vector3(sign * 0.25f, 0.32f, 0f));
            Transform lower = Joint(side + (leg ? "LowerLeg" : "LowerArm"), upper, Vector3.down * upperLength);
            Transform end = Joint(side + (leg ? "Foot" : "Hand"), lower, Vector3.down * lowerLength);
            Part(side + " upper segment", upper, Vector3.down * upperLength * 0.5f, new Vector3(leg ? 0.15f : 0.105f, upperLength, leg ? 0.17f : 0.105f), cloth);
            Part(side + " lower segment", lower, Vector3.down * lowerLength * 0.5f, new Vector3(leg ? 0.13f : 0.09f, lowerLength, leg ? 0.15f : 0.09f), cloth);
            Part(side + (leg ? " shoe" : " hand"), end, leg ? new Vector3(0f, -0.05f, 0.065f) : Vector3.down * 0.045f,
                leg ? new Vector3(0.16f, 0.12f, 0.28f) : new Vector3(0.085f, 0.10f, 0.045f), endMaterial);
            return Capture(upper, lower, end);
        }

        private static Transform Joint(string name, Transform parent, Vector3 localPosition)
        {
            Transform joint = new GameObject(name).transform; joint.SetParent(parent, false); joint.localPosition = localPosition; return joint;
        }

        private static void Part(string name, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = at; part.transform.localScale = size;
            var collider = part.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material; part.layer = 2;
        }

        public static Material MakeMaterial(string name, Color color)
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            Shader shader = (srp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard")) ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = name }; material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            return material;
        }
    }
}
