using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Timers;

namespace FsUnboundMapper.Logging
{
    internal class TimedLogger : IDisposable
    {
        private readonly StringBuilder Buffer;
        private readonly Timer Timer;
        private bool TimerStopped;
        private bool TimerLocked;
        private bool BufferDumped;
        private bool disposedValue;
        private int IntervalSecondsField;
        private int WriteCount;
        private int WriteSkipCount;

        public int WriteThreshold { get; set; }
        public int IntervalSeconds
        {
            get => IntervalSecondsField;
            set
            {
                // Set new timer interval
                Timer.Interval = TimeSpan.FromSeconds(value).TotalMilliseconds;
                IntervalSecondsField = value;
            }
        }

        public int ForceWriteThreshold { get; set; }

        public bool IsDisposed
            => disposedValue;

        public TimedLogger(int writeThreshold, int intervalMilliseconds, int forceWriteThreshold, bool start)
        {
            WriteThreshold = writeThreshold;

            // Avoid setting new timer interval here as we are constructing
            IntervalSecondsField = intervalMilliseconds;
            ForceWriteThreshold = forceWriteThreshold;

            Buffer = new StringBuilder();
            Timer = new Timer(TimeSpan.FromMilliseconds(intervalMilliseconds));
            Timer.Elapsed += TimedBufferDump;
            BufferDumped = true;
            TimerStopped = true;

            if (start)
            {
                Start();
            }
        }

        private void TimedBufferDump(object? sender, ElapsedEventArgs e)
        {
            // Pause the timer to save on resources when there's nothing to write.
            if (BufferDumped)
            {
                StopTimer();
                return;
            }

            ThresholdBufferDump();
        }

        private void ThresholdBufferDump()
        {
            // Don't dump unless we have the specified number of writes done
            if (WriteCount < WriteThreshold)
            {
                // Force write after skipping this many attempts
                if (WriteSkipCount < ForceWriteThreshold)
                {
                    WriteSkipCount++;
                    return;
                }
                else
                {
                    WriteSkipCount = 0;
                }
            }

            DumpBuffer();
            WriteCount = 0;
        }

        private void DumpBuffer()
        {
            Console.Write(Buffer);
            Buffer.Clear();
            BufferDumped = true;
        }

        private void UpdateBufferState()
        {
            WriteCount++;
            BufferDumped = false;
            StartTimer();
        }

        public void Write(string value)
        {
            Buffer.Append(value);
            UpdateBufferState();
        }

        public void WriteLine(string value)
        {
            Buffer.AppendLine(value);
            UpdateBufferState();
        }

        public void WriteLine()
        {
            Buffer.AppendLine();
            UpdateBufferState();
        }

        public void DirectWrite(string value)
        {
            Flush();
            Console.Write(value);
        }

        public void DirectWriteLine(string value)
        {
            Flush();
            Console.WriteLine(value);
        }

        public void DirectWriteLine()
        {
            Flush();
            Console.WriteLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
            => DumpBuffer();

        public void Clear()
        {
            BufferDumped = true;
            Buffer.Clear();
            WriteCount = 0;
        }

        public void LockTimer()
        {
            TimerLocked = true;
        }

        public void UnlockTimer()
        {
            TimerLocked = false;
        }

        private void StartTimer()
        {
            if (!TimerLocked)
            {
                Start();
            }
        }

        private void StopTimer()
        {
            if (!TimerLocked)
            {
                Stop();
            }
        }

        public void Start()
        {
            if (TimerStopped)
            {
                Timer.Start();
                TimerStopped = false;
            }
        }

        public void Stop()
        {
            if (!TimerStopped)
            {
                Timer.Stop();
                TimerStopped = true;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Write anything remaining before disposing
                    Flush();

                    // Stop and dispose the timer
                    Stop();
                    Timer.Dispose();

                    // Clear the buffer
                    Clear();
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
    }
}
