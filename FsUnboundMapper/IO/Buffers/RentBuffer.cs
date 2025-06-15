using System;
using System.Buffers;

namespace FsUnboundMapper.IO.Buffers
{
    public class RentBuffer<T> : IDisposable
    {
        public T[] Buffer;
        public int Length;
        private bool disposedValue;

        public RentBuffer(int length)
        {
            if (length < 1)
            {
                Buffer = [];
                Length = 0;
            }

            Buffer = ArrayPool<T>.Shared.Rent(length);
            Length = length;
        }

        public void Return()
        {
            if (Length > 0)
            {
                ArrayPool<T>.Shared.Return(Buffer);
            }

            disposedValue = true;
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (Length > 0)
                    {
                        ArrayPool<T>.Shared.Return(Buffer);
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
