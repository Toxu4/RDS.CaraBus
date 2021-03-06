﻿using System;
using System.Threading.Tasks;

namespace RDS.CaraBus.AsyncEx.Nito.AsyncEx.Tasks
{
    /// <summary>
    /// Helper methods for working with tasks.
    /// </summary>
    internal static class TaskHelper
    {
        /// <summary>
        /// Executes a delegate synchronously, and captures its result in a task. The returned task is already completed.
        /// </summary>
        /// <param name="func">The delegate to execute synchronously.</param>
#pragma warning disable 1998
        public static async Task ExecuteAsTask(Action func)
#pragma warning restore 1998
        {
            func();
        }

        /// <summary>
        /// Executes a delegate synchronously, and captures its result in a task. The returned task is already completed.
        /// </summary>
        /// <param name="func">The delegate to execute synchronously.</param>
#pragma warning disable 1998
        public static async Task<T> ExecuteAsTask<T>(Func<T> func)
#pragma warning restore 1998
        {
            return func();
        }
    }
}
