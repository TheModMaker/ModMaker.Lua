// Copyright 2018 Jacob Trimble
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Parser
{
    /// <summary>
    /// This reads from an input Stream and outputs strings from it.  This
    /// maintains an internal buffer from the stream and allows peeking on
    /// the value.  This also handles Unicode surrogates and combining
    /// characters.
    ///
    /// All sizes are given as number of displayed characters, i.e. a grapheme.
    /// </summary>
    public class BufferedStringReader
    {
        /// <summary>
        /// The size of the reads that happen.
        /// </summary>
        const int kReadSize = 1024 * 32;

        Stream source_;
        Decoder decoder_;
        string buffer_;
        int bufferPos_;

        /// <summary>
        /// Gets the column offset of the current position.
        /// </summary>
        public int Column { get; private set; }
        /// <summary>
        /// Gets the line number of the current position.
        /// </summary>
        public int Line { get; private set; }

        /// <summary>
        /// Creates a new reader that reads from the given stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="encoding">
        /// The encoding to use when reading.  If null, this will auto-detect
        /// based on byte-order-marks, or default to UTF8.
        /// </param>
        public BufferedStringReader(Stream stream, Encoding encoding)
        {
            source_ = stream;
            buffer_ = "";
            bufferPos_ = 0;
            Column = 1;
            Line = 1;

            if (encoding == null)
            {
                byte[] buffer;
                int offset, length;
                encoding = DetectEncoding(stream, out buffer, out offset,
                                          out length) ??
                           Encoding.UTF8;
                decoder_ = encoding.GetDecoder();

                char[] chars = new char[4];
                int charCount =
                    decoder_.GetChars(buffer, offset, length, chars, 0);
                buffer_ = new string(chars, 0, charCount);
            }
            else
            {
                decoder_ = encoding.GetDecoder();
            }
        }


        /// <summary>
        /// Looks at the given number of characters from the buffer.
        /// </summary>
        /// <param name="count">The number of characters to look at.</param>
        /// <returns>The read string.</returns>
        public string Peek(int count)
        {
            string ret = "";
            while (count > 0)
            {
                ret += NextTextElement(ret.Length);
                count--;
            }
            return ret;
        }

        /// <summary>
        /// Reads the given number of characters.
        /// </summary>
        /// <param name="count">The number of characters to read.</param>
        /// <returns>The string that was read.</returns>
        public string Read(int count)
        {
            return MovePosition(Peek(count));
        }

        /// <summary>
        /// Looks at the buffer until the given substring is found.
        /// </summary>
        /// <param name="str">
        /// The string to look for.  Can be multiple characters.
        /// </param>
        /// <returns>The read string.</returns>
        public string PeekUntil(string str)
        {
            string ret = "";
            while (!ret.Contains(str))
            {
                string temp = NextTextElement(ret.Length);
                if (temp.Length == 0)
                    break;
                ret += temp;
            }
            return ret;
        }

        /// <summary>
        /// Reads until the given substring is found.
        /// </summary>
        /// <param name="str">
        /// The string to look for.  Can be multiple characters.
        /// </param>
        /// <returns>The read string.</returns>
        public string ReadUntil(string str)
        {
            return MovePosition(PeekUntil(str));
        }

        /// <summary>
        /// Reads until the given predicate returns false.
        /// </summary>
        /// <param name="pred">
        /// The predicate to check.  This will be given a "text element", which
        /// is a single grapheme.
        /// </param>
        /// <returns>The read string.</returns>
        public string ReadWhile(Predicate<string> pred)
        {
            string ret = "";
            while (true)
            {
                string temp = NextTextElement(ret.Length);
                if (temp.Length == 0 || !pred(temp))
                    break;
                ret += temp;
            }
            return MovePosition(ret);
        }


        /// <summary>
        /// Moves the current position based on the given string that was read.
        /// </summary>
        /// <param name="portion">The string that was read.</param>
        /// <returns>The string that was read.</returns>
        string MovePosition(string portion)
        {
            bufferPos_ += portion.Length;
            int index = portion.LastIndexOf('\n');
            if (index >= 0)
            {
                Column = new StringInfo(portion.Substring(index + 1))
                             .LengthInTextElements + 1;
                Line += portion.Count((ch) => ch == '\n');
            }
            else
            {
                Column += new StringInfo(portion).LengthInTextElements;
            }
            return portion;
        }

        /// <summary>
        /// Reads from the given stream and tries to detect the encoding used.
        /// </summary>
        /// <param name="source">The stream to read from.</param>
        /// <param name="buffer">Will contain a buffer that was read.</param>
        /// <param name="offset">
        /// Will contain the offset in the buffer to start at.
        /// </param>
        /// <param name="length">Will contain the number bytes read.</param>
        /// <returns>The detected encoding, or null.</returns>
        Encoding DetectEncoding(Stream source, out byte[] buffer,
                                out int offset, out int length)
        {
            // Order by length so if the source matches multiple, we use the
            // longer.  For example, UTF32 has the same first two bytes as
            // UTF16, so if it matches, we want to assume UTF32.
            Predicate<byte[]> empty = (arr) => arr == null || arr.Length == 0;
            var encodings = Encoding.GetEncodings()
                .Select((info) => info.GetEncoding())
                .Where((e) => !empty(e.GetPreamble()))
                .OrderByDescending((e) => e.GetPreamble().Length)
                .ToArray();

            int maxSize =
                encodings.Length == 0 ? 0 : encodings[0].GetPreamble().Length;
            buffer = new byte[maxSize];
            length = source.Read(buffer, 0, buffer.Length);
            foreach (var encoding in encodings)
            {
                var prefix = encoding.GetPreamble();
                if (prefix.SequenceEqual(buffer.Take(prefix.Length)))
                {
                    offset = prefix.Length;
                    length -= offset;
                    return encoding;
                }
            }

            offset = 0;
            return null;
        }

        /// <summary>
        /// Gets the next text element at the given offset.
        /// </summary>
        /// <param name="offset">
        /// The offset, in characters, within the buffer to start at.
        /// </param>
        /// <returns>The next text element at that position.</returns>
        string NextTextElement(int offset)
        {
            while (true)
            {
                // Only accept the element if it isn't at the end of the
                // buffer.  This ensures we get the whole element, even if
                // it extends into the next read.
                string elem = StringInfo.GetNextTextElement(
                    buffer_, bufferPos_ + offset);
                if (bufferPos_ + offset + elem.Length < buffer_.Length)
                    return elem;
                // If at EOF, just return what we read.
                if (!ExtendBuffer())
                    return elem;
            }
        }

        /// <summary>
        /// Performs a read from the stream and appends the results to the
        /// buffer.
        /// </summary>
        /// <returns>True if something was read, false if at EOF.</returns>
        bool ExtendBuffer()
        {
            byte[] buffer = new byte[kReadSize];
            int bytesRead = source_.Read(buffer, 0, kReadSize);
            int charCount = decoder_.GetCharCount(buffer, 0, bytesRead);
            char[] chars = new char[charCount];
            int charsRead = decoder_.GetChars(buffer, 0, bytesRead, chars, 0);

            buffer_ = buffer_.Substring(bufferPos_) +
                      new string(chars, 0, charsRead);
            bufferPos_ = 0;
            return bytesRead != 0;
        }
    }
}
