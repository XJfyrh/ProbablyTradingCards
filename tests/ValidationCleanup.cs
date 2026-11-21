using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace ProbablyTradingCards
{
    internal sealed class ValidationCleanup
    {
        private readonly List<Exception> errors = new List<Exception>();

        internal void Attempt(string operation, Action action)
        {
            try { action(); }
            catch (Exception error) { errors.Add(new InvalidOperationException(operation, error)); }
        }

        internal void Each<T>(string operation, IEnumerable<T> items, Action<T> action)
        {
            T[] snapshot = null;
            Attempt(operation + " enumeration", () => snapshot = items.ToArray());
            if (snapshot == null) return;
            for (int index = 0; index < snapshot.Length; index++)
            {
                T item = snapshot[index];
                Attempt(operation + " [" + index + "]", () => action(item));
            }
        }

        internal static void Run(Action scenario, Action<ValidationCleanup> finish)
        {
            Exception failure = null;
            try { scenario(); }
            catch (Exception error) { failure = error; }

            var cleanup = new ValidationCleanup();
            cleanup.Attempt("validation cleanup", () => finish(cleanup));
            if (cleanup.errors.Count > 0)
            {
                if (failure != null) cleanup.errors.Insert(0, failure);
                throw new AggregateException("Native validation failed during cleanup.", cleanup.errors);
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
