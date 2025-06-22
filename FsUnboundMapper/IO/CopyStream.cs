using System;
using System.IO;

namespace FsUnboundMapper.IO
{
    public class CopyStream : Stream
    {
        private readonly Stream BaseStream;
        private readonly long Origin;
        private long Offset;
        private readonly long Size;

        public override bool CanRead
            => true;

        public override bool CanSeek
            => false;

        public override bool CanWrite
            => false;

        public override long Length
            => Size;

        public override long Position
        {
            get => Offset;
            set => throw new NotSupportedException();
        }

        public long Remaining
            => Size - Offset;

        public CopyStream(Stream baseStream, long length)
        {
            BaseStream = baseStream;
            Origin = baseStream.Position;
            if (baseStream.Position != Origin)
            {
                BaseStream.Seek(Origin, SeekOrigin.Begin);
            }

            Offset = 0;
            Size = length;
        }

        public override void Flush()
            => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (BaseStream)
            {
                long oldPos = BaseStream.Position;
                long offsetPos = Origin + Offset;
                if (BaseStream.Position != offsetPos)
                {
                    BaseStream.Seek(offsetPos, SeekOrigin.Begin);
                }

                int length = count;
                long remaining = Remaining;
                if ((ulong)count > (ulong)remaining)
                {
                    if (remaining < 1)
                    {
                        return 0;
                    }

                    length = (int)remaining;
                }

                int read = BaseStream.Read(buffer, offset, length);
                Offset += read;
                BaseStream.Seek(oldPos, SeekOrigin.Begin);
                return read;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}
