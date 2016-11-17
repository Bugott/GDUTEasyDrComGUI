using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace GDUTEasyDrComGUI
{
    class Heartbeat
    {
        private static Func<Exception, bool> onException;
        private static bool exit = false;

        private static string server = "10.0.3.6";
        private static int port = 61440;
        private static byte pppoe_flag = 0x6a;
        private static Random random = new Random();
        private static byte keep_alive2_flag = 0xdc;
        private static EndPoint target = new IPEndPoint(IPAddress.Parse(server), port);

        private static void Log(string msg, byte[] data, int len)
        {
            Console.WriteLine(msg);
            Console.WriteLine(BitConverter.ToString(data.Take(len).ToArray()).Replace("-", ""));
        }

        private class Socketx : Socket
        {
            private Encoding encoding = Encoding.GetEncoding(936);

            public Socketx(AddressFamily a, SocketType b, ProtocolType c) : base(a, b, c)
            {
                Bind(new IPEndPoint(IPAddress.Parse("0.0.0.0"), port));
                SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 3000);
                SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 3000);
            }

            public int Receive(byte[] data, ref EndPoint remoteEP)
            {
                while (true)
                {
                    int len = ReceiveFrom(data, ref remoteEP);
                    if (data[0] == 0x4d)
                    {
                        Console.WriteLine("received message packet, dropped.");
                        Console.WriteLine(encoding.GetString(data.Skip(4).ToArray()));
                        continue;
                    }
                    return len;
                }
            }
        }

        private class KA2
        {
            public static Tuple<List<byte>, bool> GenerateCRC(byte[] data, int encrypt_type)
            {
                int DRCOM_DIAL_EXT_PROTO_CRC_INIT = 20000711;
                List<byte> ret = new List<byte>();
                byte[] foo;
                switch (encrypt_type)
                {
                    case 0:
                        //// 加密方式无
                        ret.AddRange(BitConverter.GetBytes(DRCOM_DIAL_EXT_PROTO_CRC_INIT));
                        ret.AddRange(BitConverter.GetBytes(126));
                        return Tuple.Create(ret, false);
                    case 1:
                        //// 加密方式为 md5
                        using (MD5CryptoServiceProvider hash = new MD5CryptoServiceProvider())
                            foo = hash.ComputeHash(data);
                        ret.Add(foo[2]);
                        ret.Add(foo[3]);
                        ret.Add(foo[8]);
                        ret.Add(foo[9]);
                        ret.Add(foo[5]);
                        ret.Add(foo[6]);
                        ret.Add(foo[13]);
                        ret.Add(foo[14]);
                        return Tuple.Create(ret, true);
                    case 2:
                        //// md4
                        using (MD4 hash = new MD4())
                            foo = hash.ComputeHash(data);
                        ret.Add(foo[1]);
                        ret.Add(foo[2]);
                        ret.Add(foo[8]);
                        ret.Add(foo[9]);
                        ret.Add(foo[4]);
                        ret.Add(foo[5]);
                        ret.Add(foo[11]);
                        ret.Add(foo[12]);
                        return Tuple.Create(ret, true);
                    case 3:
                        //// sha1
                        using (SHA1CryptoServiceProvider hash = new SHA1CryptoServiceProvider())
                            foo = hash.ComputeHash(data);
                        ret.Add(foo[2]);
                        ret.Add(foo[3]);
                        ret.Add(foo[9]);
                        ret.Add(foo[10]);
                        ret.Add(foo[5]);
                        ret.Add(foo[6]);
                        ret.Add(foo[15]);
                        ret.Add(foo[16]);
                        return Tuple.Create(ret, true);
                    default:
                        throw new Exception("???");
                }
            }

            public static List<byte> KeepAlivePackageBuilder(byte number, int random, byte[] tail, byte type = 1, bool first = false)
            {
                List<byte> data = new List<byte>();
                data.Add(0x07);
                data.Add(number);
                data.AddRange(new byte[] { 0x28, 0x00, 0x0b });
                data.Add(type);
                if (first)
                    data.AddRange(new byte[] { 0x0f, 0x27 });
                else
                    data.AddRange(new byte[] { keep_alive2_flag, 0x02 });
                data.AddRange(new byte[] { 0x2f, 0x12 });
                for (int i = 0; i < 6; i++)
                    data.Add(0x00);
                data.AddRange(tail);
                if (type == 3)
                {
                    byte[] foo = new byte[] { 0x00, 0x00, 0x00, 0x00 };
                    int encrypt_mode = BitConverter.ToInt32(tail, 0) & 3;
                    Tuple<List<byte>, bool> res = KA2.GenerateCRC(data.ToArray(), encrypt_mode);
                    List<byte> crc = res.Item1;
                    bool val = res.Item2;
                    data.AddRange(crc);
                    data.AddRange(foo);
                    for (int i = 0; i < 8; i++)
                        data.Add(0x00);
                }
                else //packet type = 1
                    for (int i = 0; i < 20; i++)
                        data.Add(0x00);
                return data;
            }

            public static void KeepAlive2(Socketx s, PPPOEHeartbeat pppoe)
            {
                byte[] tail;
                List<byte> packet;
                string svr = server;

                int ran = random.Next(0, 0xFFFF);
                int svr_num = 0;
                byte[] data = new byte[1024];

                ran += random.Next(1, 10);
                packet = KeepAlivePackageBuilder((byte)svr_num, ran, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 1, true);
                int datalen = 0;
                while (true)
                {
                    Log("[keep-alive2] send1", packet.ToArray(), packet.Count);
                    s.SendTo(packet.ToArray(), target);
                    datalen = s.ReceiveFrom(data, ref target);
                    if (data[0] == 0x07 && data[2] == 0x28)
                        break;
                    else if (data[0] == 0x07 && data[2] == 0x10)
                    {
                        Console.WriteLine("[keep-alive2] recv file, resending...");
                        svr_num++;
                        packet = KeepAlivePackageBuilder((byte)svr_num, ran, new byte[] { 0x00, 0x00, 0x00, 0x00 }, (byte)svr_num, false);
                    }
                    else
                        Log("[keep-alive2] recv1/unexpected", data.ToArray(), datalen);
                }
                Log("[keep-alive2] recv1", data.ToArray(), datalen);

                ran += random.Next(1, 10);
                packet = KeepAlivePackageBuilder((byte)svr_num, ran, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 1, false);
                Log("[keep-alive2] send2", packet.ToArray(), packet.Count);
                s.SendTo(packet.ToArray(), target);
                while (true)
                {
                    datalen = s.ReceiveFrom(data, ref target);
                    if (data[0] == 0x07)
                    {
                        svr_num++;
                        break;
                    }
                    else
                        Log("[keep-alive2] recv2/unexpected", data.ToArray(), datalen);
                }
                Log("[keep-alive2] recv2", data.ToArray(), datalen);
                tail = new byte[] { data[16], data[17], data[18], data[19] };


                ran += random.Next(1, 10);
                packet = KeepAlivePackageBuilder((byte)svr_num, ran, tail, 3, false);
                Log("[keep-alive2] send3", packet.ToArray(), packet.Count);
                s.SendTo(packet.ToArray(), target);
                while (true)
                {
                    datalen = s.ReceiveFrom(data, ref target);
                    if (data[0] == 0x07)
                    {
                        svr_num++;
                        break;
                    }
                    else
                        Log("[keep-alive2] recv3/unexpected", data.ToArray(), datalen);
                }
                Log("[keep-alive2] recv3", data.ToArray(), datalen);
                tail = new byte[] { data[16], data[17], data[18], data[19] };

                Console.WriteLine("[keep-alive2] keep-alive2 loop was in daemon.");

                int i = svr_num;
                while (true)
                {
                    try
                    {
                        ran += random.Next(1, 10);
                        packet = KeepAlivePackageBuilder((byte)i, ran, tail, 1, false);
                        Log($"[keep-alive2] send{i}", packet.ToArray(), packet.Count);
                        s.SendTo(packet.ToArray(), target);
                        datalen = s.ReceiveFrom(data, ref target);
                        Log($"[keep-alive2] recv{i}", data.ToArray(), datalen);
                        tail = new byte[] { data[16], data[17], data[18], data[19] };

                        ran += random.Next(1, 10);
                        packet = KeepAlivePackageBuilder((byte)(i + 1), ran, tail, 3, false);
                        s.SendTo(packet.ToArray(), target);
                        Log($"[keep-alive2] send{i + 1}", packet.ToArray(), packet.Count);
                        datalen = s.ReceiveFrom(data, ref target);
                        Log($"[keep-alive2] recv{i + 1}", data.ToArray(), datalen);
                        tail = new byte[] { data[16], data[17], data[18], data[19] };

                        i = (i + 2) % 0xFF;
                        Thread.Sleep(10 * 1000);
                        pppoe.Send(s);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"[ka2] exception {e.Message}");
                        exit = onException(e);
                        if (exit)
                            return;
                    }
                }
            }
        }

        private class PPPOEHeartbeat
        {
            private byte count;

            public PPPOEHeartbeat(byte num = 1)
            {
                count = num;
            }

            public List<byte> MakeChallenge()
            {
                List<byte> data = new List<byte>();
                data.Add(0x07);
                data.Add(count);
                data.AddRange(new byte[] { 0x08, 0x00, 0x01, 0x00 });
                data.AddRange(new byte[] { 0x00, 0x00 });
                return data;
            }

            public int DrcomCRC32(byte[] data, int init = 0)
            {
                int ret = init;
                for (int i = 0; i < data.Length; i += 4)
                {
                    ret ^= BitConverter.ToInt32(data, i);
                    //ret &= 0xFFFFFFFF;
                }
                return ret;
            }

            public List<byte> MakeHeartbeat(byte[] sip, byte[] challenge_seed, bool first = false)
            {
                List<byte> data = new List<byte>();
                // DrcomDialExtProtoHeader - 5 bytes
                data.Add(0x07); // code
                data.Add(count); // id
                data.AddRange(new byte[] { 0x60, 0x00 }); // length
                data.Add(0x03); // type
                data.Add(0x00); // uid length
                data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); // mac
                data.AddRange(sip); // AuthHostIP
                if (first)
                {
                    data.AddRange(new byte[] { 0x00, 0x62, 0x00 });
                    data.Add(pppoe_flag);
                }
                else
                {
                    data.AddRange(new byte[] { 0x00, 0x63, 0x00 });
                    data.Add(pppoe_flag);
                }
                data.AddRange(challenge_seed); // Challenge Seed
                
                int encrypt_mode = BitConverter.ToInt32(challenge_seed, 0) & 3;
                Tuple<List<byte>, bool> res = KA2.GenerateCRC(challenge_seed, encrypt_mode);
                List<byte> crc = res.Item1;
                bool foo = res.Item2;
                data.AddRange(crc);
                if (foo == false)
                {
                    int crc2 = (DrcomCRC32(data.ToArray()) * 19680126) /*& 0xFFFFFFFF*/;
                    data.RemoveRange(data.Count - 8, 8);
                    data.AddRange(BitConverter.GetBytes(crc2));
                    data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
                }
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 16; j++)
                        data.Add(0x00);
                return data;
            }

            public void Send(Socketx s)
            {
                while (true)
                {
                    //1. challenge
                    var data = MakeChallenge();
                    Log("pppoe: send challenge request", data.ToArray(), data.Count);
                    s.SendTo(data.ToArray(), target);
                    byte[] buff = new byte[1024];
                    int recvlen = s.Receive(buff, ref target);
                    Log("pppoe: received challenge response", buff.ToArray(), recvlen);

                    count++;
                    count %= 0xFF;

                    //2. heartbeat
                    byte[] seed = new byte[] { buff[8], buff[9], buff[10], buff[11] };
                    byte[] sip = new byte[] { buff[12], buff[13], buff[14], buff[15] };
                    if (count != 2 && count != 1)
                        data = MakeHeartbeat(sip, seed);
                    else
                        data = MakeHeartbeat(sip, seed, true);
                    Log("pppoe: send heartbeat request", data.ToArray(), data.Count);
                    s.SendTo(data.ToArray(), target);
                    try
                    {
                        recvlen = s.Receive(buff, ref target);
                        Log("pppoe: received heartbeat response", buff.ToArray(), recvlen);
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("pppoe: heartbeat response failed, retry");
                        Console.WriteLine("pppoe: reset idx to 0x01");
                        count = 1;
                        exit = onException(e);
                        if (exit)
                            return;
                        continue;
                    }
                    count++;
                    count %= 0xFF;
                }
            }
        }

        public static void BeginHeartbeat(Func<Exception, bool> OnException)
        {
            onException = OnException;
            unchecked
            {
                if (!BitConverter.IsLittleEndian)
                {
                    exit = onException(new SystemException("Unsupported system"));
                    if (exit)
                        return;
                }

                Socketx s = new Socketx(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Unspecified);
                while (!exit)
                {
                    PPPOEHeartbeat pppoe = new PPPOEHeartbeat();
                    pppoe.Send(s);
                    KA2.KeepAlive2(s, pppoe);
                }
            }
        }
    }
}

