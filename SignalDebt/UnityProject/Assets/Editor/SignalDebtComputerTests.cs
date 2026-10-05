#if SIGNAL_DEBT_COMPUTER_TEST
using System;
using SignalDebt;

// Portable behavioral tests of the computer input/timing policy. These do not
// exercise Unity transforms, rendering, collision, UI layout, or camera visuals.
internal static class SignalDebtComputerTests
{
    private static int assertions;

    private static void Check(bool condition, string description)
    {
        assertions++;
        if (!condition) throw new Exception("FAIL: " + description);
    }

    private static void Near(float actual, float expected, string description)
    {
        Check(Math.Abs(actual - expected) < 0.00001f, description);
    }

    private static void CheckPhase(SignalDebtComputerSession session, ComputerPhase phase)
    {
        Check(session.Phase == phase, "expected phase " + phase + ", got " + session.Phase);
        Check(session.IsAtDesk == (phase != ComputerPhase.Walking), "desk occupancy in " + phase);
        Check(session.CanUseDesktop == (phase == ComputerPhase.Desktop), "desktop input gate in " + phase);
        Check(session.IsTransitioning == (phase != ComputerPhase.Walking && phase != ComputerPhase.Desktop),
            "transition gate in " + phase);
    }

    private static SignalDebtComputerSession AtPhase(ComputerPhase phase)
    {
        var session = new SignalDebtComputerSession();
        if (phase == ComputerPhase.Walking) return session;
        session.Enter();
        if (phase == ComputerPhase.Sitting) return session;
        session.Advance(0.65f);
        if (phase == ComputerPhase.Focusing) return session;
        session.Advance(0.8f);
        if (phase == ComputerPhase.Desktop) return session;
        session.Exit();
        if (phase == ComputerPhase.Unfocusing) return session;
        session.Advance(0.55f);
        return session;
    }

    private static void NormalSession()
    {
        var session = new SignalDebtComputerSession();
        CheckPhase(session, ComputerPhase.Walking);
        Check(!session.Exit(), "exit while walking is harmless");
        session.Advance(100f);
        CheckPhase(session, ComputerPhase.Walking);
        Check(session.Enter(), "enter from first-person walking");
        CheckPhase(session, ComputerPhase.Sitting);
        Near(session.Progress, 0f, "sit starts at zero progress");
        session.Advance(0f);
        CheckPhase(session, ComputerPhase.Sitting);
        session.Advance(0.649f);
        CheckPhase(session, ComputerPhase.Sitting);
        session.Advance(0.0011f);
        CheckPhase(session, ComputerPhase.Focusing);
        Check(session.Elapsed > 0f && session.Elapsed < 0.0002f, "carry leftover frame time into focus");
        session.Advance(0.799f);
        CheckPhase(session, ComputerPhase.Focusing);
        session.Advance(0.001f);
        CheckPhase(session, ComputerPhase.Desktop);
        session.Advance(999f);
        CheckPhase(session, ComputerPhase.Desktop);
        Check(session.Exit(), "exit desktop is accepted");
        CheckPhase(session, ComputerPhase.Unfocusing);
        session.Advance(0.55f);
        CheckPhase(session, ComputerPhase.Standing);
        Near(session.Elapsed, 0f, "standing starts at exact phase boundary");
        session.Advance(0.65f);
        CheckPhase(session, ComputerPhase.Walking);
    }

    private static void ExactPhaseBoundaries()
    {
        var session = AtPhase(ComputerPhase.Sitting);
        session.Advance(0.65f);
        CheckPhase(session, ComputerPhase.Focusing);
        Near(session.Progress, 0f, "focus begins at zero on exact sitting boundary");
        session.Advance(0.8f);
        CheckPhase(session, ComputerPhase.Desktop);
        session.Exit();
        session.Advance(0.55f);
        CheckPhase(session, ComputerPhase.Standing);
        Near(session.Progress, 0f, "stand begins at zero on exact unfocus boundary");
        session.Advance(0.65f);
        CheckPhase(session, ComputerPhase.Walking);
    }

    private static void InterruptionsAndRepeatedInput()
    {
        foreach (ComputerPhase phase in Enum.GetValues(typeof(ComputerPhase)))
        {
            var session = AtPhase(phase);
            if (session.IsTransitioning) session.Advance(0.1f);
            float elapsed = session.Elapsed;
            if (phase != ComputerPhase.Walking)
            {
                for (int i = 0; i < 10; i++) Check(!session.Enter(), "repeated enter ignored in " + phase);
                CheckPhase(session, phase);
                Near(session.Elapsed, elapsed, "repeated enter cannot restart animation in " + phase);
            }
            bool expectedExit = phase == ComputerPhase.Sitting || phase == ComputerPhase.Focusing
                || phase == ComputerPhase.Desktop;
            Check(session.Exit() == expectedExit, "escape availability in " + phase);
            ComputerPhase next = phase == ComputerPhase.Sitting ? ComputerPhase.Standing
                : phase == ComputerPhase.Focusing || phase == ComputerPhase.Desktop ? ComputerPhase.Unfocusing : phase;
            CheckPhase(session, next);
            float exitElapsed = session.Elapsed;
            for (int i = 0; i < 10; i++) Check(!session.Exit(), "repeated escape ignored in " + next);
            Near(session.Elapsed, exitElapsed, "escape cannot restart exit animation");
            if (phase == ComputerPhase.Sitting)
            {
                session.Advance(0.65f);
                CheckPhase(session, ComputerPhase.Walking);
            }
            else if (phase == ComputerPhase.Focusing || phase == ComputerPhase.Desktop)
            {
                session.Advance(0.55f);
                CheckPhase(session, ComputerPhase.Standing);
                session.Advance(0.65f);
                CheckPhase(session, ComputerPhase.Walking);
            }
        }
    }

    private static void ProgressAndLongFrames()
    {
        foreach (ComputerPhase phase in new[] { ComputerPhase.Sitting, ComputerPhase.Focusing,
            ComputerPhase.Unfocusing, ComputerPhase.Standing })
        {
            var session = AtPhase(phase);
            float lastProgress = session.Progress;
            float lastEased = session.EasedProgress;
            for (int i = 0; i < 20; i++)
            {
                session.Advance(0.02f);
                CheckPhase(session, phase);
                Check(session.Progress > lastProgress && session.Progress <= 1f,
                    "progress rises within bounds during " + phase);
                Check(session.EasedProgress > lastEased && session.EasedProgress <= 1f,
                    "camera easing rises within bounds during " + phase);
                lastProgress = session.Progress;
                lastEased = session.EasedProgress;
            }
        }
        var half = AtPhase(ComputerPhase.Focusing);
        half.Advance(0.4f);
        Near(half.Progress, 0.5f, "focus halfway progress");
        Near(half.EasedProgress, 0.5f, "focus easing halfway");

        var longFrame = AtPhase(ComputerPhase.Sitting);
        longFrame.Advance(1f);
        CheckPhase(longFrame, ComputerPhase.Focusing);
        Near(longFrame.Elapsed, 0.35f, "long frame preserves time after sitting");
        longFrame.Advance(float.MaxValue);
        CheckPhase(longFrame, ComputerPhase.Desktop);
        longFrame.Exit();
        longFrame.Advance(float.MaxValue);
        CheckPhase(longFrame, ComputerPhase.Walking);
    }

    private static void InvalidTimeAndReset()
    {
        foreach (ComputerPhase phase in Enum.GetValues(typeof(ComputerPhase)))
        {
            var session = AtPhase(phase);
            if (session.IsTransitioning) session.Advance(0.1f);
            float elapsed = session.Elapsed;
            foreach (float invalid in new[] { -0.001f, float.MinValue, float.NaN,
                float.PositiveInfinity, float.NegativeInfinity })
            {
                bool rejected = false;
                try { session.Advance(invalid); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "invalid time rejected in " + phase);
                CheckPhase(session, phase);
                Near(session.Elapsed, elapsed, "invalid time cannot mutate elapsed time");
            }
            session.Reset();
            CheckPhase(session, ComputerPhase.Walking);
            Near(session.Elapsed, 0f, "reset clears elapsed time in " + phase);
            Check(session.Enter(), "reset allows entering again from " + phase);
            CheckPhase(session, ComputerPhase.Sitting);
        }
    }

    private static void RepeatedSessions()
    {
        var session = new SignalDebtComputerSession();
        for (int i = 0; i < 100; i++)
        {
            Check(session.Enter(), "enter repeat session " + i);
            CheckPhase(session, ComputerPhase.Sitting);
            if (i % 3 == 0) session.Advance(0.2f);
            else if (i % 3 == 1) session.Advance(1f);
            else
            {
                session.Advance(2f);
                CheckPhase(session, ComputerPhase.Desktop);
            }
            Check(session.Exit(), "exit repeat session " + i);
            Check(!session.CanUseDesktop, "desktop input disabled immediately on exit " + i);
            session.Advance(2f);
            CheckPhase(session, ComputerPhase.Walking);
            Near(session.Elapsed, 0f, "no elapsed time leaks between sessions");
        }
    }

    private static int Main()
    {
        try
        {
            NormalSession();
            ExactPhaseBoundaries();
            InterruptionsAndRepeatedInput();
            ProgressAndLongFrames();
            InvalidTimeAndReset();
            RepeatedSessions();
            if (assertions == 0) throw new Exception("FAIL: no computer-session assertions ran");
            Console.WriteLine("PASS: " + assertions + " computer-session assertions; no Unity runtime exercised.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
#endif
