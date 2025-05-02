/*
*	 File: PipeStream.cs
*	 Website: https://github.com/0xeeeeeeeeeeee/AudioCopy
*	 Copyright 2024-2025 (C) 0xeeeeeeeeeeee (0x12e)
*
*   This file is part of AudioCopy
*	 
*	 AudioCopy is free software: you can redistribute it and/or modify
*	 it under the terms of the GNU General Public License as published by
*	 the Free Software Foundation, either version 2 of the License, or
*	 (at your option) any later version.
*	 
*	 AudioCopy is distributed in the hope that it will be useful,
*	 but WITHOUT ANY WARRANTY; without even the implied warranty of
*	 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*	 GNU General Public License for more details.
*	 
*	 You should have received a copy of the GNU General Public License
*	 along with AudioCopy. If not, see <http://www.gnu.org/licenses/>.
*/




using System.Diagnostics;

namespace libAudioCopy.Audio
{
    [DebuggerNonUserCode]
    public class PipeStream : Stream
    {
        private readonly Queue<byte> mBuffer = new();
        private bool mFlushed;
        private long mMaxBufferLength = 200 * MB;
        private bool mBlockLastRead;

        public const long KB = 1024;
        public const long MB = KB * 1024;

        public long MaxBufferLength
        {
            get => mMaxBufferLength;
            set => mMaxBufferLength = value;
        }

        public bool BlockLastReadBuffer
        {
            get => mBlockLastRead;
            set
            {
                mBlockLastRead = value;
                if (!mBlockLastRead)
                    lock (mBuffer) Monitor.Pulse(mBuffer);
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => mBuffer.Count;
        public override long Position { get => 0; set => throw new NotImplementedException(); }

        public override void Flush()
        {
            mFlushed = true;
            lock (mBuffer) Monitor.Pulse(mBuffer);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentException("buffer is null");
            if (offset < 0 || count < 0) throw new ArgumentOutOfRangeException();
            if (offset + count > buffer.Length) throw new ArgumentException();
            if (count == 0) return;

            lock (mBuffer)
            {
                while (Length >= mMaxBufferLength)
                    Monitor.Wait(mBuffer);

                mFlushed = false;
                for (int i = offset; i < offset + count; i++)
                    mBuffer.Enqueue(buffer[i]);
                Monitor.Pulse(mBuffer);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentException("buffer is null");
            if (offset != 0)
                throw new NotImplementedException("Offsets with value of non-zero are not supported");
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException();

            if (count == 0) return 0;

            int read = 0;
            lock (mBuffer)
            {
                while (!ReadAvailable(count))
                    Monitor.Wait(mBuffer);

                while (read < count && Length > 0)
                {
                    buffer[read++] = mBuffer.Dequeue();
                }
                Monitor.Pulse(mBuffer);
            }
            return read;
        }

        private bool ReadAvailable(int count)
        {
            return (Length >= count || mFlushed)
                   && (Length >= (count + 1) || !mBlockLastRead);
        }

        // ���� �����첽 Memory ������ ���� 
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // ����һ����ʱ����������ͬ�� Read��offset ��Ϊ 0��
            byte[] temp = new byte[buffer.Length];
            int n = Read(temp, 0, temp.Length);
            if (n > 0)
                temp.AsMemory(0, n).CopyTo(buffer);
            return await Task.FromResult(n);
        }

        #region ��֧�ֵķ���
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
        #endregion
    }
}
