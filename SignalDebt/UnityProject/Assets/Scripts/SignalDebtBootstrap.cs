using System;
using System.Collections;
using UnityEngine;

namespace SignalDebt
{
    public sealed class SignalDebtBootstrap : MonoBehaviour
    {
        public static SignalDebtBootstrap Instance { get; private set; }
        public Transform playerSpawn;
        public Transform deskSeat;
        public Transform laptopFocus;
        public MeshFilter laptopScreen;
        public Transform laptopArtworkRoot;
        public GameObject playerBodyPrefab;
        public SignalDebtSaveData State { get; private set; }
        public int WorldRevision { get { return State == null ? 0 : State.revision; } }
        public event Action<SignalDebtLedgerEvent> GameEventCommitted;
        public SignalDebtPlayer Player { get; private set; }
        public SignalDebtUI Ui { get; private set; }
        public SignalDebtComputerDisplay ComputerDisplay { get; private set; }
        public string SaveStatus { get { return store == null ? "" : store.LastStatus; } }
        public string ClientWorkspaceFeedback { get; private set; }
        public string ProofWorkspaceFeedback { get; private set; }
        private SignalDebtSaveStore store;
        private GameObject startupFailure;
        private GameObject mainMenu, aiSettings;
        private bool pauseQueued;
        public string MenuStatus { get; private set; }
        public bool HasSavedCampaign
        {
            get { return SignalDebtLocalFiles.HasCampaign(SignalDebtLocalFiles.SaveDirectory)
                || SignalDebtLocalFiles.HasCampaign(Application.persistentDataPath); }
        }
        private float notificationTimer;
        private Light notificationLight;
        private bool ownerTaskSaveBlocked;
        private bool proofTaskSaveBlocked;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            store = new SignalDebtSaveStore(SignalDebtLocalFiles.SaveDirectory);
        }

        private void Start()
        {
            ShowMainMenu();
        }

        private static void RemoveOwned(ref GameObject root)
        {
            if (root == null) return;
            root.SetActive(false);
            Destroy(root);
            root = null;
        }

        private void ShowMainMenu()
        {
            RemoveOwned(ref mainMenu);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            mainMenu = SignalDebtMainMenu.Show(this);
        }

        public void ContinueGame()
        {
            if (Player != null) return;
            bool local = SignalDebtLocalFiles.HasCampaign(SignalDebtLocalFiles.SaveDirectory);
            string source = local ? SignalDebtLocalFiles.SaveDirectory : Application.persistentDataPath;
            if (!SignalDebtLocalFiles.HasCampaign(source))
            { MenuStatus = "No saved game yet. Choose New Game."; ShowMainMenu(); return; }
            var reader = new SignalDebtSaveStore(source);
            SignalDebtSaveData loaded = reader.Load();
            if (loaded == null)
            { MenuStatus = reader.LastStatus; ShowMainMenu(); return; }
            store = new SignalDebtSaveStore(SignalDebtLocalFiles.SaveDirectory);
            if (!store.Save(loaded))
            { MenuStatus = "The game could not save to UserData. Use a writable folder for the extracted game."; ShowMainMenu(); return; }
            BeginCampaign(loaded);
        }

        public void StartNewGame()
        {
            if (Player != null) return;
            if (!SignalDebtLocalFiles.ArchiveCampaign(out string error))
            { MenuStatus = error; ShowMainMenu(); return; }
            store = new SignalDebtSaveStore(SignalDebtLocalFiles.SaveDirectory);
            var fresh = new SignalDebtSaveData();
            if (!store.Save(fresh))
            {
                MenuStatus = "The new game could not be saved. Use a writable game folder. Backed-up campaigns are in UserData/Backups.";
                ShowMainMenu(); return;
            }
            BeginCampaign(fresh);
        }

        private void BeginCampaign(SignalDebtSaveData loaded)
        {
            RemoveOwned(ref mainMenu);
            RemoveOwned(ref startupFailure);
            State = loaded;
            ownerTaskSaveBlocked = false;
            proofTaskSaveBlocked = false;
            ClientWorkspaceFeedback = "";
            ProofWorkspaceFeedback = "";
            MenuStatus = "";
            Time.timeScale = 1f;
            if (playerSpawn == null) playerSpawn = MakeAnchor("PlayerSpawn fallback", new Vector3(0f, 0.08f, -1.8f));
            if (deskSeat == null) deskSeat = MakeAnchor("DeskSeat fallback", new Vector3(-1.8f, 0.08f, 1.1f));
            if (laptopFocus == null) laptopFocus = MakeAnchor("LaptopFocus fallback", new Vector3(-1.8f, 1.05f, 1.9f));
            Player = new GameObject("First person player").AddComponent<SignalDebtPlayer>();
            Player.Initialize(playerSpawn.position, playerSpawn.rotation, playerBodyPrefab);
            ComputerDisplay = new GameObject("Laptop live display").AddComponent<SignalDebtComputerDisplay>();
            ComputerDisplay.Initialize(laptopFocus, deskSeat, laptopScreen, laptopArtworkRoot);
            Ui = new GameObject("Signal Debt interface").AddComponent<SignalDebtUI>();
            Ui.Initialize(this);
            Player.ComputerReady += OnComputerReady;
            Player.ComputerClosed += OnComputerClosed;
            var glow = new GameObject("Laptop notification glow", typeof(Light));
            glow.transform.position = laptopFocus.position + Vector3.up * 0.1f;
            notificationLight = glow.GetComponent<Light>();
            notificationLight.type = LightType.Point;
            notificationLight.color = new Color(0.12f, 0.75f, 0.9f);
            notificationLight.range = 1.8f;
            notificationLight.intensity = 1.15f;
            ShowToast(State.jobPaid ? "Welcome back." : "New message · Southbank Copy Shop");
            if (!State.jobAccepted) PlayNotification();
        }

        public void OpenAiSettings()
        {
            if (aiSettings != null) return;
            SendMessage("CancelAllRequests", SendMessageOptions.DontRequireReceiver);
            if (Player != null && Ui != null && !Ui.IsPauseOpen) OpenPause();
            GameObject opened = null;
            opened = SignalDebtAiSettings.Show(this, () => { if (aiSettings == opened) aiSettings = null; });
            aiSettings = opened;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OpenPause()
        {
            if (Player == null || Ui == null) return;
            Player.SetUi(true);
            Ui.Open("Pause");
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            if (Player == null || Ui == null) return;
            Ui.CloseWorldModal();
            Time.timeScale = 1f;
            Player.SetUi(Player.IsSeated);
        }

        private IEnumerator PauseWhenStanding()
        {
            if (pauseQueued) yield break;
            pauseQueued = true;
            SignalDebtPlayer current = Player;
            CloseUi();
            while (current != null && current == Player && current.IsTransitioning) yield return null;
            pauseQueued = false;
            if (isActiveAndEnabled && current != null && current == Player && aiSettings == null) OpenPause();
        }

        public void ReturnToMainMenu()
        {
            if (State != null && !store.Save(State))
            { ShowToast("Could not save. Your last saved game is unchanged."); return; }
            SendMessage("CancelAllRequests", SendMessageOptions.DontRequireReceiver);
            if (Player != null)
            {
                Player.ComputerReady -= OnComputerReady;
                Player.ComputerClosed -= OnComputerClosed;
                Player.CancelComputerImmediately();
                Player.gameObject.SetActive(false);
                Destroy(Player.gameObject); Player = null;
            }
            if (Ui != null) { Ui.Close(); Ui.gameObject.SetActive(false); Destroy(Ui.gameObject); Ui = null; }
            if (ComputerDisplay != null) { Destroy(ComputerDisplay.gameObject); ComputerDisplay = null; }
            if (notificationLight != null) { Destroy(notificationLight.gameObject); notificationLight = null; }
            State = null;
            ShowMainMenu();
        }

        public void QuitGame()
        {
            if (store != null && State != null) store.Save(State);
            SendMessage("CancelAllRequests", SendMessageOptions.DontRequireReceiver);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static Transform MakeAnchor(string name, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.position = position;
            return anchor;
        }

        private void Update()
        {
            if (aiSettings != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) aiSettings.GetComponent<SignalDebtAiSettings>().Close();
                return;
            }
            if (Ui == null || Player == null) return;
            if (Time.timeScale > 0f && !ownerTaskSaveBlocked && State != null
                && State.clientDevice != null && State.clientDevice.ownerCheckQueued)
            {
                // One saved local work action; it does not depend on watching the
                // client window or waiting for a cloud conversation response.
                ownerTaskSaveBlocked = !Commit(state => state.StepClientTask(), true);
                if (!ownerTaskSaveBlocked)
                    ShowToast(State.clientDevice.CurrentOwnerVerified
                        ? "Southbank opened the folder and prepared order 1042."
                        : "Southbank couldn't open the folder. Check the workstation result.");
            }
            if (Time.timeScale > 0f && !proofTaskSaveBlocked && State != null
                && State.proofDocument != null && State.proofDocument.ownerCheckQueued)
            {
                proofTaskSaveBlocked = !Commit(state => state.StepProofTask(), false, true);
                if (!proofTaskSaveBlocked)
                    ShowToast(State.proofDocument.CurrentOwnerVerified(State.clientDevice)
                        ? "Southbank exported the recovered notice."
                        : "Southbank couldn't export the notice. Open Proofs for the result.");
            }
            notificationTimer += Time.deltaTime;
            if (notificationLight != null)
                notificationLight.intensity = State.jobAccepted ? 0.35f : 0.8f + Mathf.Sin(notificationTimer * 2.3f) * 0.3f;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Player.IsTransitioning) StartCoroutine(PauseWhenStanding());
                else if (Ui.IsPauseOpen) ResumeGame();
                else OpenPause();
                return;
            }
            if (Player.UiOpen || Player.IsSeated) { Ui.SetFocus(""); return; }
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
            if (Player.UiOpen || Player.IsSeated) return;
            if (Player.Sit(deskSeat, laptopFocus, ComputerDisplay)) Ui.PrepareComputer();
        }

        private void OnComputerReady() { if (Ui != null) Ui.FocusComputer(); }
        private void OnComputerClosed() { if (Ui != null) Ui.ComputerExited(); }

        public void OpenBills() { Player.SetUi(true); Ui.Open("EXODISS"); }
        public void OpenBed() { Player.SetUi(true); Ui.Open("Rest"); }

        public void CloseUi()
        {
            Time.timeScale = 1f;
            SendMessage("CancelAllRequests", SendMessageOptions.DontRequireReceiver);
            Ui.Close();
            if (Player.IsSeated) Player.Stand(); else Player.SetUi(false);
            store.Save(State);
        }

        public void AcceptJob()
        {
            if (State.jobAccepted) return;
            if (!Commit(state => state.AcceptRepair())) return;
            Ui.Open("Service");
        }

        public void ClaimPayment()
        {
            if (State.jobPaid || State.HasEvent("repair-job-payment"))
            { ShowToast("Payment already received. Check your EXODISS receipt."); return; }
            if (!State.CanClaimRepairPayment) { ShowToast("Have the client verify the current connection before collecting payment."); return; }
            if (!Commit(state => state.ClaimRepairPayment(), true)) return;
            ShowToast("+120 credits received");
        }

        public bool BeginClientWorkspace() { return Commit(state => state.BeginClientSession(), true); }
        public bool EndClientWorkspace() { return Commit(state => state.EndClientSession(), true); }
        public bool OpenClientFolder(string folderId) { return Commit(state => state.OpenClientFolder(folderId), true); }
        public bool InspectClientConnection()
        {
            return State != null && (State.serviceInspected || Commit(state => state.InspectRepair(), true));
        }
        public bool ConnectClientEndpoint(string endpointId) { return Commit(state => state.ConnectRepair(endpointId), true); }
        public bool UndoClientConnection() { return Commit(state => state.UndoClientConnection(), true); }
        public bool RequestClientCheck()
        {
            if (State == null || State.clientDevice == null) return false;
            if (State.clientDevice.ownerCheckQueued) { ownerTaskSaveBlocked = false; return true; }
            bool queued = Commit(state => state.TestRepair(), true);
            if (queued) ownerTaskSaveBlocked = false;
            return queued;
        }

        public void AcceptProofJob()
        {
            if (State == null || State.proofAccepted) return;
            if (Commit(state => state.AcceptProofRecovery(), false, true)) Ui.Open("Proofs");
        }

        public bool BeginProofWorkspace()
        {
            return State != null && (State.clientDevice.sessionOpen
                || Commit(state => state.BeginClientSession(), false, true));
        }
        public bool RestoreProofRevision(string revisionId)
        { return Commit(state => state.RestoreProofRevision(revisionId), false, true); }
        public bool UndoProofRestore()
        { return Commit(state => state.UndoProofRestore(), false, true); }
        public bool RequestProofCheck()
        {
            if (State == null || State.proofDocument == null) return false;
            if (State.proofDocument.ownerCheckQueued) { proofTaskSaveBlocked = false; return true; }
            bool queued = Commit(state => state.RequestProofCheck(), false, true);
            if (queued) proofTaskSaveBlocked = false;
            return queued;
        }
        public bool ClaimProofPayment()
        {
            if (State == null || !State.CanClaimProofPayment) return false;
            if (!Commit(state => state.ClaimProofPayment(), false, true)) return false;
            ShowToast("+160 credits received");
            return true;
        }

        public void PayRent(int requested)
        {
            int amount = Mathf.Min(Mathf.Max(0, requested), State.balance, State.rentDue);
            if (amount <= 0) { ShowToast(State.rentDue == 0 ? "Rent arrears are cleared." : "No available funds for that payment."); return; }
            if (!Commit(state => state.ApplyRentPayment(amount))) return;
            ShowToast(State.rentDue == 0 ? "Arrears cleared. A little breathing room." : "Payment received. " + State.rentDue + " credits remain overdue.");
        }

        public bool PurchaseUsedGpu(SignalDebtShopQuote quote)
        {
            if (State == null || store == null) return false;
            if (!State.ConfirmUsedGpuPurchase(quote, store.Save, out SignalDebtLedgerEvent entry, out string reason))
            { ShowToast(reason); return false; }
            if (Ui != null) Ui.Refresh();
            // A sale is visible to the world only after its full snapshot reached disk.
            try { GameEventCommitted?.Invoke(entry); }
            catch (Exception ex) { Debug.LogException(ex); }
            ShowToast("Purchase saved · " + SignalDebtShopCatalog.ProductName + " and receipt are yours.");
            return true;
        }

        public void RequestExtension()
        {
            if (State.extensionUsed || State.rentDue == 0) return;
            if (!Commit(state => state.ExtendRentDeadline())) return;
            ShowToast("Two-day extension recorded. The outstanding balance is unchanged.");
        }

        public void Rest()
        {
            if (!Commit(state => state.AdvanceDay())) return;
            CloseUi();
            ShowToast(State.day >= State.rentDeadlineDay && State.rentDue > 0
                ? "Rent deadline reached. Review the notice and request an arrangement. This prototype remains playable."
                : "Day " + State.day + " · your saved commitments remain");
        }

        private bool Commit(Func<SignalDebtSaveData, SignalDebtLedgerEvent> change, bool clientWorkspace = false,
            bool proofWorkspace = false)
        {
            if (State == null || store == null) return false;
            if (!State.TryCommitChange(change, store.Save, out SignalDebtLedgerEvent entry, out string reason))
            {
                if (clientWorkspace) ClientWorkspaceFeedback = reason;
                if (proofWorkspace) ProofWorkspaceFeedback = reason;
                if ((clientWorkspace || proofWorkspace) && Ui != null) Ui.Refresh();
                ShowToast(reason); return false;
            }
            if (clientWorkspace) ClientWorkspaceFeedback = entry.summary;
            if (proofWorkspace) ProofWorkspaceFeedback = entry.summary;
            if (Ui != null) Ui.Refresh();
            try { GameEventCommitted?.Invoke(entry); }
            catch (Exception ex) { Debug.LogException(ex); }
            return true;
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

        private void OnApplicationFocus(bool focused)
        {
            // Regain control safely after Alt-Tab; a hidden desktop must never
            // leave the player's movement or cursor permanently captured.
            if (!focused && aiSettings == null && Player != null && Ui != null && !Ui.IsPauseOpen && Player.IsSeated) CloseUi();
        }
        private void OnApplicationPause(bool paused) { if (paused && store != null && State != null) store.Save(State); }
        private void OnApplicationQuit() { if (store != null && State != null) store.Save(State); }
        private void OnEnable()
        {
            if (Instance != this) return;
            if (mainMenu != null)
            {
                mainMenu.SetActive(true);
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Player != null) { Time.timeScale = 1f; Player.enabled = true; }
        }
        private void OnDisable()
        {
            if (Instance != this) return;
            // Disabling a component alone does not stop its coroutines. A queued
            // pause must not reopen a modal after this owner has released input.
            StopAllCoroutines();
            pauseQueued = false;
            Time.timeScale = 1f;
            SendMessage("CancelAllRequests", SendMessageOptions.DontRequireReceiver);
            RemoveOwned(ref aiSettings);
            if (mainMenu != null) mainMenu.SetActive(false);
            if (Ui != null) Ui.Close();
            if (Player != null)
            {
                Player.CancelComputerImmediately();
                Player.SetUi(false);
                Player.enabled = false;
            }
        }
        private void OnDestroy()
        {
            if (Instance != this) return;
            Time.timeScale = 1f;
            RemoveOwned(ref mainMenu);
            RemoveOwned(ref aiSettings);
            if (Player != null)
            {
                Player.ComputerReady -= OnComputerReady;
                Player.ComputerClosed -= OnComputerClosed;
                Destroy(Player.gameObject);
            }
            if (Ui != null) Destroy(Ui.gameObject);
            if (ComputerDisplay != null) Destroy(ComputerDisplay.gameObject);
            if (notificationLight != null) Destroy(notificationLight.gameObject);
            if (startupFailure != null) Destroy(startupFailure);
            Instance = null;
        }
    }
}
