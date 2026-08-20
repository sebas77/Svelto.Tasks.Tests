using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

namespace CancellableChain
{
    internal static class Program
    {
        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        private static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 08 Cancellable Chain"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            bool loadOk = true;
            bool validateOk = false;
            bool processReached = false;
            bool parentFinalReached = false;

            IEnumerator<TaskContract> LoadStep()
            {
                SafeSetCursor(0, 12);
                Console.Write("  [LOAD]    ▓▓▓▓▓▓▓▓▓▓ 100% ✅  asset bundle downloaded");
                for (int i = 0; i <= 10; i++)
                {
                    Bar(12, "LOAD", i, 10);
                    Thread.Sleep(40);
                }
                yield return TaskContract.Yield.It;
                SafeSetCursor(0, 13);
                Console.WriteLine("  └─ LOAD completed (loadOk = true)");
            }

            IEnumerator<TaskContract> ValidateStep()
            {
                Bar(14, "VALIDATE", 0, 10);
                Thread.Sleep(200);
                for (int i = 0; i <= 4; i++)
                {
                    Bar(14, "VALIDATE", i, 10);
                    Thread.Sleep(80);
                }
                SafeSetCursor(0, 15);
                Console.WriteLine("  └─ ❌ VALIDATION FAILED: checksum mismatch!");
                yield return TaskContract.Break.AndStop;
            }

            IEnumerator<TaskContract> ProcessStep()
            {
                processReached = true;
                SafeSetCursor(0, 16);
                Console.WriteLine("  [PROCESS] this should NEVER run");
                yield return TaskContract.Yield.It;
            }

            IEnumerator<TaskContract> Chain()
            {
                yield return LoadStep().Continue();
                yield return ValidateStep().Continue();
                yield return ProcessStep().Continue();
            }

            IEnumerator<TaskContract> Parent()
            {
                yield return Chain().Continue();
                parentFinalReached = true;
                SafeSetCursor(0, 18);
                Console.WriteLine("  [PARENT] final step reached — this should NEVER happen");
                yield return 42;
            }

            DrawChain();

            Parent().Complete(5000);

            SafeSetCursor(0, 20);
            Console.WriteLine("  ╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  💥 CHAIN SNAPPED — Break.AndStop propagated to parent  ║");
            Console.WriteLine("  ╠════════════════════════════════════════════════════════╣");
            Console.Write  ("  ║  LOAD reached?      ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("✅ YES");
            Console.ResetColor();
            Console.WriteLine("                          ║");
            Console.Write  ("  ║  PROCESS reached?    ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("❌ NO (skipped)");
            Console.ResetColor();
            Console.WriteLine("                   ║");
            Console.Write  ("  ║  PARENT final step?  ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("❌ NO (cancelled)");
            Console.ResetColor();
            Console.WriteLine("                   ║");
            Console.WriteLine("  ╚════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  💡 Break.AndStop stops the child AND propagates up to the parent that");
            Console.WriteLine("     used .Continue(). The parent never reaches its final step.");
            Console.WriteLine();
        }

        private static void DrawChain()
        {
            SafeSetCursor(0, 10);
            Console.WriteLine("   ┌────────┐    ┌──────────┐    ┌─────────┐");
            Console.WriteLine("   │  LOAD  │───▶│ VALIDATE │───▶│ PROCESS │");
            Console.WriteLine("   └────────┘    └──────────┘    └─────────┘");
            Console.WriteLine();
        }

        private static void Bar(int row, string label, int filled, int total)
        {
            SafeSetCursor(0, row);
            Console.Write($"  [{label,-8}] ");
            for (int i = 0; i < total; i++)
                Console.Write(i < filled ? "█" : "░");
            int pct = filled * 100 / total;
            Console.Write($" {pct,3}%");
        }

        private static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  08 · CANCELLABLE CHAIN  ·  Break.AndStop + .Continue()     ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  An operation chain: LOAD → VALIDATE → PROCESS.             ║");
            Console.WriteLine("  ║  Validation fails and Break.AndStop cancels the WHOLE chain ║");
            Console.WriteLine("  ║  including the parent. The parent never reaches its end.    ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }
    }
}