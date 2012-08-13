using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;

namespace ModMaker.Lua.Parser
{
    [DebuggerDisplay("Peek = {_stream == null ? _string[cur] : buffer[cur]}")]
    class CharDecorator : IDisposable
    {
        bool _disposed = false;
        BinaryReader _stream;
        char[] buffer;
        string _string;
        int cur = 0, _end = -1;
        long pos = 0;

        public CharDecorator(Stream backing)
        {
            if (backing == null)
                throw new ArgumentNullException("backing");

            this._stream = new BinaryReader(backing);
            this._string = null;
            this.buffer = _stream.ReadChars(4096);
        }
        public CharDecorator(Stream backing, Encoding encoding)
        {
            if (backing == null)
                throw new ArgumentNullException("backing");

            this._stream = new BinaryReader(backing, encoding);
            this._string = null;
            this.buffer = _stream.ReadChars(4096);
        }
        public CharDecorator(string backing)
        {
            if (backing == null)
                throw new ArgumentNullException("backing");
            backing = backing.Replace("\r\n", "\n").Replace("\r", "\n");

            this._stream = null;
            this._string = backing;
        }
        ~CharDecorator()
        {
            Dispose(false);
        }

        public int BufferLen { get { return _stream == null ? -1 : buffer.Length; } }
        public long Position  { get { return (_stream == null ? cur : pos); } }
        public bool CanRead
        {
            get
            {
                if (_stream == null)
                    return cur < _string.Length;
                else if (_end == -1)
                {
                    UpdateBuffer(1);
                    return cur < buffer.Length;
                }
                else
                    return cur < _end;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (_stream != null)
                _stream.Close();
            _stream = null;
            _string = null;
            buffer = null;

            _disposed = true;
        }

        public char? ReadChar()
        {
            if (_stream == null)
            {
                if (cur >= _string.Length || cur < 0)
                    return null;
                else
                    return _string[cur++];
            }
            else
            {
                if (_end != -1 && cur >= _end)
                    return null;

                UpdateBuffer(1);
                pos++;
                return buffer[cur++];
            }
        }
        public char? PeekChar(int i = 0)
        {
            if (_stream == null)
            {
                if (cur + i >= _string.Length || cur < 0)
                    return null;
                else
                    return _string[cur + i];
            }
            else
            {
                if (_end != -1 && cur + i >= _end)
                    return null;

                UpdateBuffer(i);
                return buffer[cur + i];
            }
        }
        public string Read(int count)
        {
            if (_stream == null)
            {
                if (cur >= _string.Length)
                    return null;
                int c = cur;
                cur += count;
                return _string.Substring(c, Math.Min(count, _string.Length - c));
            }
            else
            {
                if (_end != -1 && cur >= _end)
                    return null;
                if (count >= buffer.Length)
                    throw new ArgumentException("Count cannot be greater than the buffer length.");

                UpdateBuffer(count);
                int c = cur;
                cur += count;
                pos += count;
                if (_end != -1)
                    return new string(buffer, c, Math.Min(count, _end - c));
                else
                    return new string(buffer, c, count);
            }
        }
        void UpdateBuffer(int c)
        {
            if (cur < 0)
                throw new InvalidOperationException("Current position is before the start of the buffer.");
            if (_stream == null || _end != -1)
                return;

            if (cur + c >= 0.95 * buffer.Length)
            {
                int n = buffer.Length / 2;
                char[] temp = new char[buffer.Length];
                if (c * 2 > buffer.Length)
                {
                    n = n - c / 2;
                }
                for (int i = 0; (cur - n + i) < buffer.Length; i++)
                {
                    temp[i] = buffer[cur - n + i];
                }
                char[] read = _stream.ReadChars(cur - n);
                if (read.Length < cur - n)
                    _end = buffer.Length - cur + n + read.Length;
                for (int i = 0; i < read.Length; i++)
                {
                    temp[buffer.Length - cur + n + i] = read[i];
                }
                cur = n;
                buffer = temp;
            }
        }

        public void Move(long i)
        {
            cur += (int)i;
            pos += i;
            if (cur < 0)
                throw new ArgumentException("Cannot move before the start of the buffer.");
            if (_stream != null)
                UpdateBuffer(0);
        }

        public byte[] GetHash()
        {
            if (_stream == null)
            {
                using( SHA512 p = SHA512.Create())
                    return p.ComputeHash(Encoding.Unicode.GetBytes(_string));
            }
            else
            {
                if (_stream.BaseStream.CanSeek)
                {
                    using (SHA512 p = SHA512.Create())
                    {
                        byte[] ret = p.ComputeHash(_stream.BaseStream);
                        return ret;
                    }
                }
                else
                    return null;
            }
        }
    }
}
