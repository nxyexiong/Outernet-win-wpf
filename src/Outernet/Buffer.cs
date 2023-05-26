using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Outernet
{
    public class OtBuffer
    {
        private byte[] _buf;
        private int _len;

        public OtBuffer()
        {
            _buf = new byte[512];
            _len = 0;
        }

        public void SetLen(int len)
        {
            while (_buf.Length < len) Expand();
            _len = len;
        }

        public int GetLen()
        {
            return _len;
        }

        public byte[] GetBuf()
        {
            return _buf;
        }

        public void InsertFront(OtBuffer buf)
        {
            InsertFront(buf.GetBuf(), buf.GetLen());
        }

        public void InsertFront(byte[] bytes, int len)
        {
            while (_buf.Length < _len + len) Expand();
            Array.Copy(_buf, 0, _buf, len, _len);
            Array.Copy(bytes, 0, _buf, 0, len);
            _len += len;
        }

        public void InsertBack(OtBuffer buf)
        {
            InsertBack(buf.GetBuf(), buf.GetLen());
        }

        public void InsertBack(byte[] bytes, int len)
        {
            while (_buf.Length < _len + len) Expand();
            Array.Copy(bytes, 0, _buf, _len, len);
            _len += len;
        }

        public void RemoveFront(int len)
        {
            if (len >= _len)
            {
                _len = 0;
                return;
            }
            Array.Copy(_buf, len, _buf, 0, _len - len);
            _len -= len;
        }

        public void RemoveBack(int len)
        {
            _len -= len;
            if (_len < 0) _len = 0;
        }

        public void Copy(OtBuffer buf)
        {
            Copy(buf.GetBuf(), buf.GetLen());
        }

        public void Copy(byte[] bytes, int len)
        {
            while (_buf.Length < len) Expand();
            Array.Copy(bytes, 0, _buf, 0, len);
            _len = len;
        }

        public void Alloc(int len)
        {
            while (_buf.Length < len) Expand();
        }

        public void Clear()
        {
            _len = 0;
        }

        private void Expand()
        {
            var newBuf = new byte[_buf.Length * 2];
            Array.Copy(_buf, 0, newBuf, 0, _buf.Length);
            _buf = newBuf;
        }
    }
}
