#pragma warning disable CA1822, CA1852, IDE0060
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

namespace GameLoop
{
    internal static class Program
    {
        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        private static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 01 Game Loop"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            int frameCount = 0;
            bool taskDone = false;

            IEnumerator<TaskContract> FrameCounterTask()
            {
                for (int i = 1; i <= 10; i++)
                {
                    frameCount = i;
                    yield return TaskContract.Yield.It;
                }
                taskDone = true;
            }

            using (var runner = new SteppableRunner("GameLoopRunner"))
            {
                FrameCounterTask().RunOn(runner);

                var spinner = new[] { '|', '/', '─', '\\' };
                int spinIdx = 0;
                int frame = 0;

                while (runner.hasTasks)
                {
                    DrawGear(spinIdx, frame, frameCount);
                    runner.Step();
                    spinIdx = (spinIdx + 1) % spinner.Length;
                    frame++;
                    Thread.Sleep(200);
                }

                DrawGear(0, frame, frameCount, done: true);
            }

            SafeSetCursor(0, 16);
            Console.WriteLine("  ┌──────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │  ✅ Task complete! Counted 10 frames across 10 steps.    │");
            Console.WriteLine("  │  💡 Each runner.Step() advanced the task by one Yield.It  │");
            Console.WriteLine("  └──────────────────────────────────────────────────────────┘");
            Console.WriteLine();
        }

        private static void DrawGear(int spinIdx, int frame, int frameCount, bool done = false)
        {
            var spinner = new[] { '|', '/', '─', '\\' };
            int total = 10;
            int filled = done ? total : Math.Min(frameCount, total);
            string bar = new string('█', filled) + new string('░', total - filled);

            SafeSetCursor(0, 12);
            Console.WriteLine("  ╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  🎮 GAME LOOP — SteppableRunner ticking each frame             ║");
            Console.WriteLine("  ╠═══════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"  ║                                                                 ║");
            Console.WriteLine($"  ║   ⚙  {spinner[spinIdx]}   Frame {frameCount,2}/{total}   [{bar}]  {(done ? "✓ DONE" : "     ")}       ║");
            Console.WriteLine($"  ║                                                                 ║");
            Console.WriteLine("  ╚═══════════════════════════════════════════════════════════════╝");
        }

        private static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  01 · GAME LOOP  ·  Lean Task + SteppableRunner             ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  A simulated game loop ticks a SteppableRunner each frame.   ║");
            Console.WriteLine("  ║  A task counts to 10, yielding TaskContract.Yield.It between ║");
            Console.WriteLine("  ║  each count. The spinner shows the runner being stepped.     ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("   ┌─────────┐     ┌──────────┐     ┌─────────────┐");
            Console.WriteLine("   │  Main   │────▶│ Step()   │────▶│  Task:      │");
            Console.WriteLine("   │  Loop   │     │ tick #N  │     │  count++    │");
            Console.WriteLine("   │  200ms  │◀────│ yield    │◀────│  Yield.It   │");
            Console.WriteLine("   └─────────┘     └──────────┘     └─────────────┘");
            Console.WriteLine();
        }
    }
}