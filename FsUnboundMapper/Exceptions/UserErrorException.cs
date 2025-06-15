using System;

namespace FsUnboundMapper.Exceptions
{
    internal class UserErrorException(string message) : Exception(message) { }
}
