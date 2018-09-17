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

using ModMaker.Lua.Parser;
using NUnit.Framework;
using System;
using System.IO;
using System.Text;

namespace UnitTests.Parser
{
    [TestFixture]
    class BufferedStringReaderTest
    {
        Stream CreateStream(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            return new MemoryStream(bytes);
        }

        [Test]
        public void TracksPosition()
        {
            var str = "Foo\nBar\nBaz\n\n123\n000";
            var reader = new BufferedStringReader(CreateStream(str), null);
            Assert.AreEqual("Fo", reader.Read(2));
            Assert.AreEqual(3, reader.Column);
            Assert.AreEqual(1, reader.Line);
            Assert.AreEqual("o\nB", reader.Read(3));
            Assert.AreEqual(2, reader.Column);
            Assert.AreEqual(2, reader.Line);
            Assert.AreEqual("ar", reader.Read(2));
            Assert.AreEqual(4, reader.Column);
            Assert.AreEqual(2, reader.Line);
            Assert.AreEqual("\n", reader.Read(1));
            Assert.AreEqual(1, reader.Column);
            Assert.AreEqual(3, reader.Line);
            Assert.AreEqual("Baz\n\n123\n0", reader.Read(10));
            Assert.AreEqual(2, reader.Column);
            Assert.AreEqual(6, reader.Line);
            Assert.AreEqual("00", reader.Read(10));
            Assert.AreEqual("", reader.Read(10));
        }

        [Test]
        public void ReadUntil()
        {
            var str = "Foo_Bar__Baz";
            var reader = new BufferedStringReader(CreateStream(str), null);
            Assert.AreEqual("Foo_", reader.ReadUntil("_"));
            Assert.AreEqual("Bar_", reader.ReadUntil("_"));
            Assert.AreEqual("_", reader.ReadUntil("_"));
            Assert.AreEqual("Baz", reader.ReadUntil("_"));
            Assert.AreEqual("", reader.ReadUntil("_"));

            str = "Foo_Bar*Baz*_0_*Foo";
            reader = new BufferedStringReader(CreateStream(str), null);
            Assert.AreEqual("Foo_Bar*Baz*_0_*", reader.ReadUntil("_*"));
            Assert.AreEqual("Foo", reader.ReadUntil("_*"));
            Assert.AreEqual("", reader.ReadUntil("_*"));
        }

        [Test]
        public void ReadWhile()
        {
            Predicate<string> func = (ch) => char.IsLetter(ch, 0);
            var str = "Foo_Bar";
            var reader = new BufferedStringReader(CreateStream(str), null);
            Assert.AreEqual("Foo", reader.ReadWhile(func));
            Assert.AreEqual("", reader.ReadWhile(func));
            Assert.AreEqual("", reader.ReadWhile(func));
            Assert.AreEqual("_", reader.Read(1));
            Assert.AreEqual("Bar", reader.ReadWhile(func));
            Assert.AreEqual("", reader.ReadWhile(func));
        }

        [Test]
        public void HandlesSurrogates()
        {
            var str = "😀😈_😓😭";
            Assert.AreEqual(9, str.Length);  // Each should be a surrogate pair
            var reader = new BufferedStringReader(CreateStream(str), null);
            Assert.AreEqual("😀", reader.Peek(1));
            Assert.AreEqual("😀😈", reader.Peek(2));
            Assert.AreEqual("😀", reader.Read(1));
            Assert.AreEqual("😈_😓😭", reader.Peek(10));
            Assert.AreEqual("😈_", reader.ReadUntil("_"));
            Assert.AreEqual("😓😭", reader.ReadUntil("😱"));

            str = "😀😀😀😀😀😀_123";
            Predicate<string> func = (ch) => ch == "😀";
            reader = new BufferedStringReader(CreateStream(str), null);
            Assert.AreEqual("😀😀😀😀😀😀", reader.ReadWhile(func));
            Assert.AreEqual("_123", reader.Read(10));
        }

        [Test]
        public void HandleCombiningCharacters()
        {
            var str = "cre\u0300me bru\u0302le\u0301e 000";
            var reader = new BufferedStringReader(CreateStream(str), null);
            Assert.AreEqual("cre\u0300me ", reader.Read(6));
            Predicate<string> func = (ch) => char.IsLetter(ch, 0);
            Assert.AreEqual("bru\u0302le\u0301e", reader.ReadWhile(func));
            Assert.AreEqual(" 000", reader.Read(10));
        }

        /// <summary>
        /// A read-only stream that splits a combining character across
        /// different reads.
        /// </summary>
        class SplitStream : Stream
        {
            int index_ = 0;

            public SplitStream() { }

            public override bool CanRead { get { return true; } }

            public override bool CanSeek { get { return false; } }

            public override bool CanWrite { get { return false; } }

            public override long Length => throw new NotImplementedException();

            public override long Position {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                byte[] tmp = new byte[0];
                switch (index_++)
                {
                    case 0:
                        tmp = new byte[] { 0x65 };
                        break;
                    case 1:
                        tmp = new byte[] { 0xCC, 0x82, 0x65 };
                        break;
                    case 2:
                        tmp = new byte[] { 0xCC };
                        break;
                    case 3:
                        tmp = new byte[] { 0x82, 0x65, 0xCC, 0x82 };
                        break;
                    case 4:
                        tmp = new byte[] { 0x5F };
                        break;
                }

                Assert.Greater(count, tmp.Length);
                tmp.CopyTo(buffer, offset);
                return tmp.Length;
            }

            public override void Flush() { }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }
        }

        [Test]
        public void HandlesSplitReads()
        {
            var reader = new BufferedStringReader(new SplitStream(), null);
            Assert.AreEqual("e\u0302e\u0302", reader.Peek(2));
            Assert.AreEqual("e\u0302e\u0302e\u0302", reader.Read(3));
            Assert.AreEqual("_", reader.Read(10));
        }

        [Test]
        public void DetectsEncodings()
        {
            var utf8 = new byte[] { 0xEF, 0xBB, 0xBF, 0xF0, 0x9F, 0x98, 0x80 };
            var reader =
                new BufferedStringReader(new MemoryStream(utf8), null);
            Assert.AreEqual("😀", reader.Read(10));

            var utf16le = new byte[] { 0xFF, 0xFE, 0x3D, 0xD8, 0x00, 0xDE };
            reader = new BufferedStringReader(new MemoryStream(utf16le), null);
            Assert.AreEqual("😀", reader.Read(10));

            var utf16be = new byte[] { 0xFE, 0xFF, 0xD8, 0x3D, 0xDE, 0x00 };
            reader = new BufferedStringReader(new MemoryStream(utf16be), null);
            Assert.AreEqual("😀", reader.Read(10));

            var utf32 = new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x00, 0xF6, 0x01, 0x00 };
            reader = new BufferedStringReader(new MemoryStream(utf32), null);
            Assert.AreEqual("😀", reader.Read(10));
        }
    }
}
