using System;
using System.IO;

namespace VKPlayer.AudioPlayer
{
    public class SimultaneousStream : Stream
    {
        public enum PositionMode { None, FromRead, FromWrite }
        public PositionMode Mode { get; set; }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }

        public override long Length { get { lock (_innerStream) { return _innerStream.Length; } } }
        public override long Position
        {
            get
            {
                switch (Mode)
                {
                    case PositionMode.FromRead:
                        return _readPosition;

                    case PositionMode.FromWrite:
                        return _writePosition;

                    default:
                        throw new NotSupportedException();
                }
            }
            set
            {
                switch (Mode)
                {
                    case PositionMode.FromRead:
                        _readPosition = value;
                        break;

                    case PositionMode.FromWrite:
                        _writePosition = value;
                        break;

                    default:
                        throw new NotSupportedException();
                }
            }
        }

        private MemoryStream _innerStream;
        private long _readPosition;
        private long _writePosition;
        private long _copyPosition;


        public SimultaneousStream(PositionMode mode) { _innerStream = new MemoryStream(); Mode = mode; }


        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotImplementedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (_innerStream)
            {
                _innerStream.Position = _readPosition;
                int red = _innerStream.Read(buffer, offset, count);
                _readPosition = _innerStream.Position;

                return red;
            }
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_innerStream)
            {
                _innerStream.Position = _writePosition;
                _innerStream.Write(buffer, offset, count);
                _writePosition = _innerStream.Position;

                return;
            }
        }

        public override void Flush() { lock (_innerStream) { _innerStream.Flush(); } }

        public new void CopyTo(Stream destination)
        {
            byte[] buffer = new byte[16 * 1024];
            int read;
            while ((read = CopyRead(buffer, 0, buffer.Length)) > 0)
                destination.Write(buffer, 0, read);

            _copyPosition = 0;
        }
        private int CopyRead(byte[] buffer, int offset, int count)
        {
            lock (_innerStream)
            {
                _innerStream.Position = _copyPosition;
                int red = _innerStream.Read(buffer, offset, count);
                _copyPosition = _innerStream.Position;

                return red;
            }
        }
    }
}
