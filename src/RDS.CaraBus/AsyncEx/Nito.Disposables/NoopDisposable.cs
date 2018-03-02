using System;

namespace RDS.CaraBus.AsyncEx.Nito.Disposables
{
    /// <summary>
    /// A singleton disposable that does nothing when disposed.
    /// </summary>
    internal sealed class NoopDisposable: IDisposable
    {
        private NoopDisposable()
        {
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Gets the instance of <see cref="NoopDisposable"/>.
        /// </summary>
        public static NoopDisposable Instance { get; } = new NoopDisposable();
    }
}
