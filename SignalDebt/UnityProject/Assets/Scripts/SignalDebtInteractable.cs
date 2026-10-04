using UnityEngine;

namespace SignalDebt
{
    public enum SignalDebtInteraction { Desk, Bills, Bed, Door }

    public sealed class SignalDebtInteractable : MonoBehaviour
    {
        public SignalDebtInteraction interaction;
        public string prompt;
        public Transform doorPivot;
        public float openDegrees = -92f;
        private Quaternion closedRotation;
        private bool isOpen;

        private void Awake()
        {
            if (doorPivot != null) closedRotation = doorPivot.rotation;
        }

        private void Update()
        {
            if (doorPivot == null) return;
            Quaternion destination = Quaternion.AngleAxis(isOpen ? openDegrees : 0f, Vector3.up) * closedRotation;
            doorPivot.rotation = Quaternion.RotateTowards(doorPivot.rotation, destination, Time.deltaTime * 110f);
        }

        public void Use(SignalDebtBootstrap game)
        {
            switch (interaction)
            {
                case SignalDebtInteraction.Desk: game.OpenLaptop(); break;
                case SignalDebtInteraction.Bills: game.OpenBills(); break;
                case SignalDebtInteraction.Bed: game.OpenBed(); break;
                case SignalDebtInteraction.Door:
                    isOpen = !isOpen;
                    prompt = isOpen ? "E  Close apartment door" : "E  Open apartment door";
                    game.ShowToast(isOpen ? "Hallway access · apartment prototype boundary" : "Door closed");
                    break;
            }
        }
    }
}
