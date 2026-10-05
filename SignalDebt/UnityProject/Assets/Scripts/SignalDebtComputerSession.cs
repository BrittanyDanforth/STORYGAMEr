using System;

namespace SignalDebt
{
    public enum ComputerPhase { Walking, Sitting, Focusing, Desktop, Unfocusing, Standing }

    // Input and timing policy shared by the player and portable regression tests.
    // The desktop cannot receive input until the physical camera reaches the screen.
    public sealed class SignalDebtComputerSession
    {
        public ComputerPhase Phase { get; private set; }
        public float Elapsed { get; private set; }
        public bool IsAtDesk { get { return Phase != ComputerPhase.Walking; } }
        public bool CanUseDesktop { get { return Phase == ComputerPhase.Desktop; } }
        public bool IsTransitioning { get { return IsAtDesk && !CanUseDesktop; } }
        public float Progress { get { return Duration <= 0f ? 1f : Math.Min(1f, Elapsed / Duration); } }
        public float EasedProgress { get { float t = Progress; return t * t * (3f - 2f * t); } }
        private float Duration
        {
            get
            {
                switch (Phase)
                {
                    case ComputerPhase.Sitting: return 0.65f;
                    case ComputerPhase.Focusing: return 0.8f;
                    case ComputerPhase.Unfocusing: return 0.55f;
                    case ComputerPhase.Standing: return 0.65f;
                    default: return 0f;
                }
            }
        }

        public bool Enter()
        {
            if (IsAtDesk) return false;
            SetPhase(ComputerPhase.Sitting);
            return true;
        }

        public bool Exit()
        {
            if (!IsAtDesk || Phase == ComputerPhase.Unfocusing || Phase == ComputerPhase.Standing) return false;
            SetPhase(Phase == ComputerPhase.Sitting ? ComputerPhase.Standing : ComputerPhase.Unfocusing);
            return true;
        }

        public void Advance(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            while (IsTransitioning)
            {
                float remaining = Duration - Elapsed;
                if (seconds < remaining) { Elapsed += seconds; return; }
                seconds -= remaining;
                switch (Phase)
                {
                    case ComputerPhase.Sitting: SetPhase(ComputerPhase.Focusing); break;
                    case ComputerPhase.Focusing: SetPhase(ComputerPhase.Desktop); break;
                    case ComputerPhase.Unfocusing: SetPhase(ComputerPhase.Standing); break;
                    case ComputerPhase.Standing: SetPhase(ComputerPhase.Walking); break;
                }
            }
        }

        public void Reset() { SetPhase(ComputerPhase.Walking); }
        private void SetPhase(ComputerPhase phase) { Phase = phase; Elapsed = 0f; }
    }
}
