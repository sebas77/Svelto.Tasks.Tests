using System;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Parallelism;

#pragma warning disable CS0436

namespace Example13_BatchPathfinding
{
    struct PathfindingJob : ISveltoJob
    {
        public int[] results;
        public int[] threadAssign;

        public void Update(int index)
        {
            results[index] = index;
            threadAssign[index] = Thread.CurrentThread.ManagedThreadId;
        }

        public void Dispose() { }
    }

    static class Program
    {
        const int TotalUnits = 1000;
        const int ThreadCount = 4;
        static readonly char[] _spin = { '|', '/', '-', '\\' };

        static void SafeClear() { try { Console.Clear(); } catch { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch { } }
        static void SafeTitle(string title) { try { Console.Title = title; } catch { } }
        static void SafeReadKey() { try { Console.ReadKey(); } catch { } }

        static void Main()
        {
            SafeTitle("Svelto.Tasks — Parallel Job Collection");
            SafeCursorVisible(false);

            PrintBanner();

            var results = new int[TotalUnits];
            var threadAssign = new int[TotalUnits];

            for (int i = 0; i < TotalUnits; i++)
                results[i] = -1;

            var job = new PathfindingJob
            {
                results = results,
                threadAssign = threadAssign
            };

            using var collection = new MultiThreadedParallelJobCollection<PathfindingJob>(
                "PathfindingBatch", ThreadCount, false);

            ShowInitializing();

            collection.Add(job, TotalUnits);
            collection.Complete();

            int done = 0;
            for (int i = 0; i < TotalUnits; i++)
                if (results[i] == i) done++;

            var distinctThreads = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < TotalUnits; i++)
                distinctThreads.Add(threadAssign[i]);

            ShowGrid(results, threadAssign, distinctThreads);

            Console.WriteLine();
            Console.WriteLine("  ──────────────────────────────────────────────────────");
            Console.WriteLine("  📊 Results:");
            Console.WriteLine("     Units pathfound : {0}/{1}  {2}", done, TotalUnits,
                done == TotalUnits ? "✅ ALL DONE" : "❌ INCOMPLETE");
            Console.WriteLine("     Threads used     : {0}", distinctThreads.Count);
            foreach (int tid in distinctThreads)
            {
                int count = 0;
                for (int i = 0; i < TotalUnits; i++)
                    if (threadAssign[i] == tid) count++;
                Console.WriteLine("       └─ Thread {0,2}: processed {1,4} units", tid, count);
            }
            Console.WriteLine("  ──────────────────────────────────────────────────────");

            Console.WriteLine();
            Console.WriteLine("  ✅ Done. Press any key to exit.");
            SafeCursorVisible(true);
            SafeReadKey();
        }

        static void ShowInitializing()
        {
            Console.WriteLine();
            for (int frame = 0; frame < 6; frame++)
            {
                SafeSetCursor(0, 8);
                string spinner = _spin[frame % 4].ToString();
                Console.Write("  🔧 Initializing pathfinding batch: {0}  {1}    \n",
                    spinner, new string('░', frame * 4));
                Thread.Sleep(150);
            }
            SafeSetCursor(0, 8);
            Console.Write("  🔧 Pathfinding batch ready! {0} units × {1} threads \n\n",
                TotalUnits, ThreadCount);
        }

        static void ShowGrid(int[] results, int[] threadAssign, System.Collections.Generic.HashSet<int> threads)
        {
            int[] threadIds = new int[threads.Count];
            threads.CopyTo(threadIds);
            Array.Sort(threadIds);

            int[,] threadMap = new int[10, 10];
            for (int i = 0; i < 100; i++)
            {
                int row = i / 10;
                int col = i % 10;
                int tid = threadAssign[i * 10];
                int tIndex = Array.IndexOf(threadIds, tid);
                threadMap[row, col] = tIndex;
            }

            char[] cellChars = { '█', '▓', '▒', '░' };
            ConsoleColor[] cellColors =
            {
                ConsoleColor.Red, ConsoleColor.Green,
                ConsoleColor.Cyan, ConsoleColor.Yellow
            };

            Console.WriteLine("  🗺️  Pathfinding Grid (10×10, each cell = 10 units):");
            Console.WriteLine("  ┌──────────────────────────────────────────┐");

            for (int row = 0; row < 10; row++)
            {
                Console.Write("  │ ");
                for (int col = 0; col < 10; col++)
                {
                    int tIdx = threadMap[row, col];
                    Console.ForegroundColor = cellColors[tIdx % cellColors.Length];
                    Console.Write(cellChars[tIdx % cellChars.Length]);
                    Console.Write(cellChars[tIdx % cellChars.Length]);
                }
                Console.ResetColor();
                Console.WriteLine(" │");
            }

            Console.WriteLine("  └──────────────────────────────────────────┘");

            Console.WriteLine();
            for (int t = 0; t < threadIds.Length; t++)
            {
                int count = 0;
                for (int i = 0; i < TotalUnits; i++)
                    if (threadAssign[i] == threadIds[t]) count++;

                Console.ForegroundColor = cellColors[t % cellColors.Length];
                Console.Write("  {0}", cellChars[t % cellChars.Length]);
                Console.ResetColor();
                Console.Write(" Thread {0,2}: [", threadIds[t]);
                int barLen = count / 10;
                Console.ForegroundColor = cellColors[t % cellColors.Length];
                Console.Write(new string('█', barLen));
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(new string('░', 30 - barLen));
                Console.ResetColor();
                Console.WriteLine("] {0,4}/{1} done  ✓", count, TotalUnits / ThreadCount);
            }
        }

        static void PrintBanner()
        {
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║   🗺️  Svelto.Tasks Example 13 — Batch Pathfinding          ║");
            Console.WriteLine("  ║   {0} units, {1} threads, ISveltoJob + ParallelJobCollection  ║",
                TotalUnits, ThreadCount);
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }
    }
}