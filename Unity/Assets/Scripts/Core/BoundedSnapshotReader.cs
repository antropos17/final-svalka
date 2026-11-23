using System;
using System.IO;
using System.Text;

namespace JunkToNeon.Core
{
    public sealed class SnapshotSizeLimitException : FormatException
    {
        public SnapshotSizeLimitException() : base("Snapshot exceeds bounded read limits") { }
    }

    // The character limit is UTF-16 code units, matching JsonStateCodec's string.Length.
    public static class BoundedSnapshotReader
    {
        public const int MaximumCharacters = 65536;
        // UTF-32 BMP needs at most four bytes/unit, plus the longest detected BOM.
        // Only one further byte is probed to distinguish EOF from byte overflow.
        public const int MaximumEncodedBytes = 4 * MaximumCharacters + 4;
        public static string ReadFile(string path)
        {
            // Preserve File.Exists/missing behavior and normal race/sharing errors.
            if (!File.Exists(path)) return null;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1))
                return ReadStream(stream, true);
        }
        public static string ReadStream(Stream stream, bool leaveOpen = true)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Readable snapshot stream required", nameof(stream));
            using (var bounded = new BoundedInput(stream, leaveOpen))
            using (var reader = new StreamReader(bounded, Encoding.UTF8, true, 1024, true))
            {
                var result = new StringBuilder(1024);
                var chunk = new char[1024];
                while (true)
                {
                    int remaining = MaximumCharacters - result.Length;
                    int count = reader.Read(chunk, 0, Math.Min(chunk.Length, remaining + 1));
                    if (count == 0) return result.ToString();
                    if (count > remaining) throw new SnapshotSizeLimitException();
                    result.Append(chunk, 0, count);
                }
            }
        }
        sealed class BoundedInput : Stream
        {
            readonly Stream source;
            readonly bool leaveOpen;
            readonly byte[] probe = new byte[1];
            int bytesRead;
            internal BoundedInput(Stream source, bool leaveOpen) { this.source = source; this.leaveOpen = leaveOpen; }
            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException(nameof(count));
                if (count == 0) return 0;
                int remaining = MaximumEncodedBytes - bytesRead;
                if (remaining == 0)
                {
                    if (source.Read(probe, 0, 1) != 0) throw new SnapshotSizeLimitException();
                    return 0;
                }
                int read = source.Read(buffer, offset, Math.Min(count, remaining));
                bytesRead += read;
                return read;
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            protected override void Dispose(bool disposing)
            {
                if (disposing && !leaveOpen) source.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
