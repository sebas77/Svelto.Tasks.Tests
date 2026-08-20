using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

#pragma warning disable

class Program
{
    static bool _responseReceived;
    static SteppableRunner _runner;

    static async Task SimulateHttpRequest()
    {
        await Task.Delay(800).RunOn(_runner);
        _responseReceived = true;
    }

    static void Main()
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch {}
        try { Console.CursorVisible = false; } catch {}

        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                    ASYNC HTTP AWAITER                      │");
        Console.WriteLine("│             Svelto.Tasks Awaiter Interop                   │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Scenario: Simulate an async HTTP request using the Svelto");
        Console.WriteLine("  awaiter. The SteppableRunner ticks while awaiting Task.Delay.");
        Console.WriteLine();

        _runner = new SteppableRunner("HttpRunner");
        _responseReceived = false;

        Console.WriteLine("  ┌─────────┐                  ┌─────────┐");
        Console.WriteLine("  │ CLIENT  │                  │ SERVER  │");
        Console.WriteLine("  └────┬────┘                  └────┬────┘");
        Console.WriteLine("       │                            │");
        Console.WriteLine("       │  >>>  HTTP GET /api  >>>   │");
        Console.WriteLine("       │                            │");
        Console.WriteLine("       v                            v");
        Console.WriteLine();
        Console.Write("  Sending request... ");

        var task = SimulateHttpRequest();
        Thread.Sleep(10);

        var spinners = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        var sw = Stopwatch.StartNew();
        int frame = 0;
        const int barWidth = 36;

        while (!_responseReceived)
        {
            _runner.Step();
            frame++;

            double progress = Math.Min(1.0, sw.Elapsed.TotalMilliseconds / 800.0);
            int filled = (int)(progress * barWidth);
            string bar = new string('█', filled) + new string('░', barWidth - filled);
            string spinner = spinners[frame % spinners.Length];

            Console.Write("\r  {0} Runner ticking  [{1}] {2,3:0}%  step #{3,3}  ", spinner, bar, progress * 100, frame);

            Thread.Sleep(20);
        }

        sw.Stop();

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("  ┌─────────┐                  ┌─────────┐");
        Console.WriteLine("  │ CLIENT  │                  │ SERVER  │");
        Console.WriteLine("  └─────────┘                  └─────────┘");
        Console.WriteLine();
        Console.WriteLine("  ╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  ✅  HTTP RESPONSE RECEIVED!                               ║");
        Console.WriteLine("  ║  Status: 200 OK                                           ║");
        Console.WriteLine("  ║  Body: {{ \"players\": [\"Alice\", \"Bob\", \"Charlie\"] }}        ║");
        Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("  Runner stepped {0} times in {1:0}ms", frame, sw.Elapsed.TotalMilliseconds);
        Console.WriteLine();
        Console.WriteLine("  ┌─ How it worked ────────────────────────────────────────────┐");
        Console.WriteLine("  │ 1. SimulateHttpRequest() runs until the first await        │");
        Console.WriteLine("  │ 2. Task.Delay(800).RunOn(runner) creates a TaskRunnerAwaiter│");
        Console.WriteLine("  │ 3. The awaiter posts the async continuation to the runner   │");
        Console.WriteLine("  │ 4. Each Step() runs the continuation → polls Task.IsComplete│");
        Console.WriteLine("  │ 5. After 800ms Task.Delay completes → next Step continues   │");
        Console.WriteLine("  │ 6. The continuation sets _responseReceived = true           │");
        Console.WriteLine("  └────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Key: .RunOn(runner) on a Task returns a TaskRunnerAwaiter that");
        Console.WriteLine("  bridges async/await into Svelto.Tasks — continuations run on");
        Console.WriteLine("  the Svelto runner, NOT the default sync context.");
        Console.WriteLine();

        try { Console.CursorVisible = true; } catch {}
        _runner.Dispose();
    }
}