using System;

namespace FsUnboundMapper.Exceptions
{
    /// <summary>
    /// Reports when a calculated hash is the same as another hash calculated from a different input.
    /// </summary>
    public class HashCollisionException : Exception
    {
        public HashCollisionException() { }
        public HashCollisionException(string message) : base(message) { }
        public HashCollisionException(string message, Exception inner) : base(message, inner) { }
    }
}
