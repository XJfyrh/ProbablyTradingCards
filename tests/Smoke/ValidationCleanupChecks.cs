using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ProbablyTradingCards;

internal static class ValidationCleanupChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var destroyed = new List<int>();
        bool restored = false;
        var failure = new InvalidOperationException("scenario assertion");
        AggregateException result = null;
        try
        {
            ValidationCleanup.Run(() => throw failure, cleanup =>
            {
                cleanup.Each("destroy fixture", new[] { 0, 1, 2 }, item =>
                {
                    destroyed.Add(item);
                    if (item < 2) throw new InvalidOperationException("remove callback " + item);
                });
                cleanup.Attempt("restore state", () => restored = true);
            });
        }
        catch (AggregateException error) { result = error; }
        check(destroyed.SequenceEqual(new[] { 0, 1, 2 }) && restored,
            "cleanup attempts every resource and restores state after multiple failures");
        check(result != null && ReferenceEquals(result.InnerExceptions[0], failure) &&
            result.InnerExceptions.Count == 3 && result.ToString().Contains("remove callback 0") &&
            result.ToString().Contains("destroy fixture [1]"),
            "cleanup reports both the original assertion and each named cleanup failure");

        restored = false;
        Exception observed = null;
        try { ValidationCleanup.Run(() => ThrowScenario(failure), cleanup =>
            cleanup.Attempt("restore", () => restored = true)); }
        catch (Exception error) { observed = error; }
        check(restored && ReferenceEquals(observed, failure) && observed.StackTrace.Contains(nameof(ThrowScenario)),
            "successful cleanup preserves the original failure and its throw site");

        result = null;
        try { ValidationCleanup.Run(() => { }, cleanup =>
            cleanup.Attempt("close window", () => throw new InvalidOperationException("window failure"))); }
        catch (AggregateException error) { result = error; }
        check(result?.InnerExceptions.Count == 1 && result.ToString().Contains("window failure"),
            "a passing scenario with failed cleanup cannot report success");

        restored = false;
        ValidationCleanup.Run(() => { }, cleanup => cleanup.Attempt("restore", () => restored = true));
        check(restored, "a passing scenario still restores state");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowScenario(Exception error) => throw error;
}
