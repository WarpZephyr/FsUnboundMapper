using System;

namespace FsUnboundMapper.Exceptions
{
    /// <summary>
    /// Reports when something is a duplicate.
    /// </summary>
    public class DuplicateException : Exception
    {
        public DuplicateException() { }
        public DuplicateException(string message) : base(message) { }
        public DuplicateException(string message, Exception inner) : base(message, inner) { }
    }
}
