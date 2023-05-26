using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Outernet
{
    public class Protocol
    {
        public bool Complete { private set; get; } = false;
        public CmdType Cmd { set; get; } = CmdType.Unknown;
        public byte[] Identification { get; } = new byte[32];
        public uint TunIp { set; get; } = 0;
        public uint DstIp { set; get; } = 0;

        public int ParseHeader(OtBuffer data)
        {
            int ret = 0;
            if (data.GetLen() < 1) return ret;
            Cmd = (CmdType)data.GetBuf()[0];
            ret++;
            if (Cmd == CmdType.ClientHandshake)
            {
                if (data.GetLen() < 1 + 32) return ret;
                Array.Copy(data.GetBuf(), 1, Identification, 0, 32);
                ret += 32;
                Complete = true;
            }
            else if (Cmd == CmdType.ServerHandshake)
            {
                if (data.GetLen() < 1 + 8) return ret;
                TunIp = BitConverter.ToUInt32(data.GetBuf(), 1);
                DstIp = BitConverter.ToUInt32(data.GetBuf(), 1 + 4);
                ret += 8;
                Complete = true;
            }
            else if (Cmd == CmdType.ClientData)
            {
                if (data.GetLen() < 1 + 32) return ret;
                Array.Copy(data.GetBuf(), 1, Identification, 0, 32);
                ret += 32;
                Complete = true;
            }
            else if (Cmd == CmdType.ServerData)
            {
                Complete = true;
            }
            return ret;
        }

        public OtBuffer GetHeaderBytes()
        {
            var buf = new OtBuffer();
            var cmd = new byte[1];
            cmd[0] = (byte)Cmd;
            buf.InsertBack(cmd, 1);
            if (Cmd == CmdType.ClientHandshake)
            {
                buf.InsertBack(Identification, 32);
            }
            else if (Cmd == CmdType.ServerHandshake)
            {
                buf.InsertBack(BitConverter.GetBytes(TunIp), 4);
                buf.InsertBack(BitConverter.GetBytes(DstIp), 4);
            }
            else if (Cmd == CmdType.ClientData)
            {
                buf.InsertBack(Identification, 32);
            }
            else if (Cmd == CmdType.ServerData)
            {
                // pass
            }
            return buf;
        }

        public enum CmdType: int
        {
            Unknown = 0,
            ClientHandshake = 1,
            ServerHandshake = 2,
            ClientData = 3,
            ServerData = 4,
        }
    }
}
