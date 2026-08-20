using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

namespace ConcurrentAnimations
{
    internal static class Program
    {
        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        private const int Ticks = 10;

        private static int _hp, _mp, _xp;

        private static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 10 Concurrent Animations"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            IEnumerator<TaskContract> HealthBar()
            {
                for (int i = 0; i < Ticks; i++)
                {
                    _hp = i + 1;
                    yield return TaskContract.Yield.It;
                }
            }

            IEnumerator<TaskContract> ManaBar()
            {
                for (int i = 0; i < Ticks; i++)
                {
                    _mp = i + 1;
                    yield return TaskContract.Yield.It;
                }
            }

            IEnumerator<TaskContract> XpBar()
            {
                for (int i = 0; i < Ticks; i++)
                {
                    _xp = i + 1;
                    yield return TaskContract.Yield.It;
                }
            }

            var parallel = new ParallelTaskCollection("UIAnimations");
            parallel.Add(HealthBar());
            parallel.Add(ManaBar());
            parallel.Add(XpBar());

            Console.WriteLine("  All three bars tween SIMULTANEOUSLY — each MoveNext advances ALL.");
            Console.WriteLine();

            DrawFrame(0);

            int step = 0;
            while (parallel.MoveNext())
            {
                step++;
                DrawFrame(step);
                Thread.Sleep(120);
            }

            DrawFrame(Ticks);
            SafeSetCursor(0, 17);
            Console.WriteLine("  ╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  ✅ All animations completed together                  ║");
            Console.WriteLine("  ╠════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  Each MoveNext advanced ALL three bars by one tick.     ║");
            Console.WriteLine("  ║  They started together and finished together.          ║");
            Console.WriteLine("  ╚════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  💡 ParallelTaskCollection: all tasks are in-flight at once (round-robin stepping).");
            Console.WriteLine();
        }

        private static void DrawFrame(int tick)
        {
            string hpBar = BarString(_hp, Ticks, "█", "░");
            string mpBar = BarString(_mp, Ticks, "█", "░");
            string xpBar = BarString(_xp, Ticks, "█", "░");

            SafeSetCursor(0, 10);
            Console.WriteLine($"  tick {tick,2}/{Ticks}                          ");
            Console.WriteLine();
            Console.WriteLine($"   HP  ❤  [{hpBar}] {_hp * 10,3}%");
            Console.WriteLine($"   MP  🔷  [{mpBar}] {_mp * 10,3}%");
            Console.WriteLine($"   XP  ⭐  [{xpBar}] {_xp * 10,3}%");
            Console.WriteLine();
            Console.WriteLine("   ┌────────────────────────────────────────────┐");
            Console.WriteLine("   │  ⏱  tween in progress...                  │");
            Console.WriteLine("   └────────────────────────────────────────────┘");
        }

        private static string BarString(int filled, int total, string on, string off)
        {
            var s = new char[total];
            for (int i = 0; i < total; i++)
                s[i] = i < filled ? '█' : '░';
            return new string(s);
        }

        private static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  10 · CONCURRENT ANIMATIONS  ·  ParallelTaskCollection      ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  Multiple UI bars (HP, MP, XP) tween simultaneously.        ║");
            Console.WriteLine("  ║  Each MoveNext advances ALL tasks by one step (round-robin). ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }
    }
}