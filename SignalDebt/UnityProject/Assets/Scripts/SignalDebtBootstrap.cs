using System;
using UnityEngine;

namespace SignalDebt
{
    public sealed class SignalDebtBootstrap : MonoBehaviour
    {
        public static SignalDebtBootstrap Instance { get; private set; }
        public Transform playerSpawn;
        public Transform deskSeat;
        public Transform laptopFocus;
        public GameObject playerBodyPrefab;
        public SignalDebtSaveData State { get; private set; }
        public int WorldRevision { get { return State == null ? 0 : State.revision; } }
        public event Action<SignalDebtLedgerEvent> GameEventCommitted;
        public SignalDebtPlayer Player { get; private set; }
        public SignalDebtUI Ui { get; private set; }
        public string SaveStatus { get { return store == null ? "" : store.LastStatus; } }
        private SignalDebtSaveStore store;
        private float notificationTimer;
        private Light notificationLight;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            store = new SignalDebtSaveStore();
            State = store.Load();
        }

        private void Start()
        {
            if (playerSpawn == null) playerSpawn = MakeAnchor("PlayerSpawn fallback", new Vector3(0f, 0.08f, -1.8f));
            if (deskSeat == null) deskSeat = MakeAnchor("DeskSeat fallback", new Vector3(-1.8f, 0.08f, 1.1f));
            if (laptopFocus == null) laptopFocus = MakeAnchor("LaptopFocus fallback", new Vector3(-1.8f, 1.05f, 1.9f));
            Player = new GameObject("First person player").AddComponent<SignalDebtPlayer>();
            Player.Initialize(playerSpawn.position, playerSpawn.rotation, playerBodyPrefab);
            Ui = new GameObject("Signal Debt interface").AddComponent<SignalDebtUI>();
            Ui.Initialize(this);
            var glow = new GameObject("Laptop notification glow", typeof(Light));
            glow.transform.position = laptopFocus.position + Vector3.up * 0.1f;
            notificationLight = glow.GetComponent<Light>();
            notificationLight.type = LightType.Point;
            notificationLight.color = new Color(0.12f, 0.75f, 0.9f);
            notificationLight.range = 1.8f;
            notificationLight.intensity = 1.15f;
            store.Save(State);
            ShowToast(State.jobPaid ? "Welcome back. Your repair history and receipt are restored." : "New message · Southbank Copy Shop · can pay today if u fix it");
            if (!State.jobAccepted) PlayNotification();
        }

        private static Transform MakeAnchor(string name, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.position = position;
            return anchor;
        }

        private void Update()
        {
            if (Ui == null || Player == null) return;
            notificationTimer += Time.deltaTime;
            if (notificationLight != null)
                notificationLight.intensity = State.jobAccepted ? 0.35f : 0.8f + Mathf.Sin(notificationTimer * 2.3f) * 0.3f;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Player.UiOpen) CloseUi();
                else { Player.SetUi(true); Ui.Open("Pause"); }
                return;
            }
            if (Player.UiOpen) { Ui.SetFocus(""); return; }
            Ray ray = new Ray(Player.View.transform.position, Player.View.transform.forward);
            SignalDebtInteractable target = null;
            if (Physics.Raycast(ray, out RaycastHit hit, 3.1f, ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                target = hit.collider.GetComponentInParent<SignalDebtInteractable>();
                if (target == null)
                {
                    SignalDebtDoorProxy proxy = hit.collider.GetComponentInParent<SignalDebtDoorProxy>();
                    if (proxy != null) target = proxy.controller;
                }
            }
            Ui.SetFocus(target == null ? "" : target.prompt);
            if (target != null && Input.GetKeyDown(KeyCode.E)) target.Use(this);
        }

        public void OpenLaptop()
        {
            Player.Sit(deskSeat, laptopFocus);
            Ui.Open(State.jobAccepted && !State.jobPaid ? "Service" : "Inbox");
        }

        public void OpenBills() { Player.SetUi(true); Ui.Open("EXODISS"); }
        public void OpenBed() { Player.SetUi(true); Ui.Open("Rest"); }

        public void CloseUi()
        {
            SendMessage("CancelAllRequests", SendMessageOptions.DontRequireReceiver);
            Ui.Close();
            if (Player.IsSeated) Player.Stand(); else Player.SetUi(false);
            store.Save(State);
        }

        public void AcceptJob()
        {
            if (State.jobAccepted) return;
            Commit(State.AcceptRepair());
            Ui.Open("Service");
        }

        public void InspectService()
        {
            if (!State.jobAccepted || State.serviceInspected) return;
            Commit(State.InspectRepair());
            ShowToast("Diagnosis: Order Folder has no service connection. It needs Order Index.");
        }

        public void ConnectService(bool correct)
        {
            if (!State.jobAccepted || !State.serviceInspected)
            { ShowToast("Inspect the status first. It tells you which service the folder needs."); return; }
            if (State.serviceConnected) return;
            if (!correct) { ShowToast("That resource does not provide order records. No changes applied. Try Order Index."); return; }
            Commit(State.ConnectRepair(true));
            ShowToast("Connection restored. Run the test to check the client's actual folder.");
        }

        public void TestService()
        {
            if (!State.serviceConnected) { ShowToast("Test failed: the folder still has no Order Index connection."); return; }
            if (State.serviceTestPassed) { ShowToast("Test already passed. The client's order folder is working."); return; }
            Commit(State.TestRepair());
            ShowToast("CLIENT · yeah its opening now. orders are there. thank u");
        }

        public void ClaimPayment()
        {
            if (!State.serviceTestPassed) { ShowToast("Run a successful test before closing the job."); return; }
            if (State.jobPaid || State.HasEvent("repair-job-payment"))
            { ShowToast("Payment already received. Check your EXODISS receipt."); return; }
            Commit(State.ClaimRepairPayment());
            ShowToast("+120 credits settled · receipt and client memory recorded");
        }

        public void PayRent(int requested)
        {
            int amount = Mathf.Min(Mathf.Max(0, requested), State.balance, State.rentDue);
            if (amount <= 0) { ShowToast(State.rentDue == 0 ? "Rent arrears are cleared." : "No available funds for that payment."); return; }
            Commit(State.ApplyRentPayment(amount));
            ShowToast(State.rentDue == 0 ? "Arrears cleared. A little breathing room." : "Payment received. " + State.rentDue + " credits remain overdue.");
        }

        public void RequestExtension()
        {
            if (State.extensionUsed || State.rentDue == 0) return;
            Commit(State.ExtendRentDeadline());
            ShowToast("Two-day extension recorded. The outstanding balance is unchanged.");
        }

        public void Rest()
        {
            Commit(State.AdvanceDay());
            CloseUi();
            ShowToast(State.day >= State.rentDeadlineDay && State.rentDue > 0
                ? "Rent deadline reached. Review the notice and request an arrangement. This prototype remains playable."
                : "Day " + State.day + " · your saved commitments remain");
        }

        private void Commit(SignalDebtLedgerEvent entry)
        {
            if (entry == null) return;
            bool saved = store.Save(State);
            if (Ui != null) Ui.Refresh();
            try { GameEventCommitted?.Invoke(entry); }
            catch (Exception ex) { Debug.LogException(ex); }
            if (!saved) ShowToast(store.LastStatus);
        }

        public void ShowToast(string message)
        {
            if (Ui == null) return;
            Ui.ShowToast(store != null && store.HasSaveError ? "NOT SAVED · " + store.LastStatus + "\n" + message : message);
        }

        private void PlayNotification()
        {
            const int sampleRate = 22050;
            float[] samples = new float[(int)(sampleRate * 0.22f)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * i / samples.Length) * Mathf.Exp(-t * 12f);
                samples[i] = (Mathf.Sin(t * 2f * Mathf.PI * 740f) + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 1110f)) * envelope * 0.18f;
            }
            AudioClip clip = AudioClip.Create("Incoming message", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0f; source.playOnAwake = false;
            source.PlayOneShot(clip, 0.55f);
            Destroy(clip, 1f);
        }

        private void OnApplicationPause(bool paused) { if (paused && store != null && State != null) store.Save(State); }
        private void OnApplicationQuit() { if (store != null && State != null) store.Save(State); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
