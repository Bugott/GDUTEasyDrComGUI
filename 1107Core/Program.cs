using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace _1107Core
{
    public static class StringExtsion
    {
        public static string Pow(this string str, int times)
        {
            StringBuilder sb = new StringBuilder(str.Length * times);
            StringBuilder b = new StringBuilder(str, str.Length * times);
            while (times != 0)
            {
                if ((times & 1) != 0)
                    sb.Append(b.ToString());
                times >>= 1;
                b.Append(b.ToString());
            }
            return sb.ToString();
        }
    }

    public class Socketx : Socket
    {
        public Socketx(AddressFamily a, SocketType b, ProtocolType c) :base(a,b,c)
        {
        }

        public new int ReceiveFrom(byte[] buffer, ref EndPoint remoteEP)
        {
            while (true)
            {
                int len = base.ReceiveFrom(buffer, ref remoteEP);
                if (buffer[0] == 0x4d)
                {
                    //log('received message packet, dropped. message: ' + gbk2utf8(data[4:]))
                    Console.WriteLine("received message packet, dropped.");
                    continue;
                }
                return len;
            }
        }
    }

    class Program
    {
        static Random random = new Random();
        static string server = "10.0.3.6";
        static string pppoe_flag = "\x6a";
        static int keep_alive2_flag = 0xdc;
        static int port = 61440;
        static void Main(string[] args)
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Unspecified);
            keep_alive2(s);
            s.Bind(new IPEndPoint(IPAddress.Parse("0.0.0.0"), port));
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 3000);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 3000);
            while(true)
            {
                keep_alive2(s);
            }
        }

        static Tuple<string, bool> gen_crc(string data, int encrypt_type)
        {
            int DRCOM_DIAL_EXT_PROTO_CRC_INIT = 20000711;
            string ret = "";
            string foo = "";
            switch (encrypt_type)
            {
                case 0:
                    //# 加密方式无
                    return Tuple.Create(
                        Encoding.ASCII.GetString(BitConverter.GetBytes(DRCOM_DIAL_EXT_PROTO_CRC_INIT)) +
                        Encoding.ASCII.GetString(BitConverter.GetBytes(126)),
                        false);
                case 1:
                    //# 加密方式为 md5
                    using (MD5CryptoServiceProvider hash = new MD5CryptoServiceProvider())
                        foo = Encoding.ASCII.GetString(hash.ComputeHash(Encoding.ASCII.GetBytes(data)));
                    ret += foo[2];
                    ret += foo[3];
                    ret += foo[8];
                    ret += foo[9];
                    ret += foo[5];
                    ret += foo[6];
                    ret += foo[13];
                    ret += foo[14];
                    return Tuple.Create(ret, true);
                case 2:
                    //# md4
                    using (MD4 hash = new MD4())
                        foo = Encoding.ASCII.GetString(hash.ComputeHash(Encoding.ASCII.GetBytes(data)));
                    ret += foo[1];
                    ret += foo[2];
                    ret += foo[8];
                    ret += foo[9];
                    ret += foo[4];
                    ret += foo[5];
                    ret += foo[11];
                    ret += foo[12];
                    return Tuple.Create(ret, true);
                case 3:
                    //# sha1
                    using (SHA1CryptoServiceProvider hash = new SHA1CryptoServiceProvider())
                        foo = Encoding.ASCII.GetString(hash.ComputeHash(Encoding.ASCII.GetBytes(data)));
                    ret += foo[2];
                    ret += foo[3];
                    ret += foo[9];
                    ret += foo[10];
                    ret += foo[5];
                    ret += foo[6];
                    ret += foo[15];
                    ret += foo[16];
                    return Tuple.Create(ret, true);
                default:
                    throw new Exception("???");
            }
        }

        // random???
        static string keep_alive_package_builder(int number, int random, string tail, int type = 1, bool first = false)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\x07").Append((char)number).Append("\x28\x00\x0b").Append((char)type);
            string data = "\x07" + (char)number + "\x28\x00\x0b" + (char)type;
            if (first)
            {
                data += "\x0f\x27";
                sb.Append("\x0f\x27");
            }
            else
            {
                data += (char)keep_alive2_flag + "\x02";
                sb.Append((char)keep_alive2_flag).Append("\x02");
            }
            data += "\x2f\x12" + "\x00".Pow(6);
            sb.Append("\x2f\x12").Append("\x00".Pow(6));
            data += tail;
            sb.Append(tail);
            if (type == 3)
            {
                //string foo = "".join([chr(int(i)) for i in "0.0.0.0".split(".")]) ;//# host_ip
                string foo = "\x00\x00\x00\x00";
                int encrypt_mode = Encoding.ASCII.GetBytes(tail)[0] & 3;
                var res = gen_crc(data, encrypt_mode);
                string crc = res.Item1;
                bool val = res.Item2;
                //crc, val = gen_crc(data, encrypt_mode);
                data += crc + foo + "\x00".Pow(8);
                sb.Append(crc).Append(foo).Append("\x00".Pow(8));
            }
            else //#packet type = 1
            {
                data += "\x00".Pow(20);
                sb.Append("\x00".Pow(20));
            }
            return data;
        }

        static void keep_alive2(Socket s/*, pppoe*/)
        {
            EndPoint target = new IPEndPoint(IPAddress.Parse(server), port);
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            string tail = "";
            string packet = "";
            string svr = server;

            int ran = random.Next(0, 0xFFFF);
            ran += random.Next(1, 10);
            //# 2014/10/15 add by latyas, maybe svr sends back a file packet
            int svr_num = 0;
            // dump(ran)???
            packet = keep_alive_package_builder(svr_num, ran, "\x00".Pow(4), 1, true);
            byte[] data = new byte[1024];
            while (true)
            {
                //log("[keep-alive2] send1", pkt = packet)
                Console.WriteLine("[keep-alive2] send1");
                Console.WriteLine(BitConverter.ToString(Encoding.ASCII.GetBytes(packet)));
                s.SendTo(Encoding.ASCII.GetBytes(packet), target);
                int datalen = s.ReceiveFrom(data, ref remoteEP);
                if (data[0] == 0x07 && data[2] == 0x28)
                    break;
                else if (data[0] == 0x07 && data[2] == 0x10)
                {
                    //log("[keep-alive2] recv file, resending..")
                    Console.WriteLine("[keep-alive2] recv file, resending..");
                    svr_num++;
                    packet = keep_alive_package_builder(svr_num, ran, "\x00".Pow(4), svr_num, false);
                }
                else
                {
                    //log("[keep-alive2] recv1/unexpected", pkt = data);
                    Console.WriteLine("[keep-alive2] recv1/unexpected");
                    Console.WriteLine(BitConverter.ToString(data));
                }
            }
            //log("[keep-alive2] recv1", pkt = data);
            Console.WriteLine("[keep-alive2] recv1");
            Console.WriteLine(BitConverter.ToString(data));

            ran += random.Next(1, 10);
            packet = keep_alive_package_builder(svr_num, ran, "\x00".Pow(4), 1, false);
            //log("[keep-alive2] send2", pkt= packet)
            Console.WriteLine("[keep-alive2] send2");
            Console.WriteLine(BitConverter.ToString(Encoding.ASCII.GetBytes(packet)));
            s.SendTo(Encoding.ASCII.GetBytes(packet), target);
            while (true)
            {
                int datalen = s.ReceiveFrom(data, ref remoteEP);
                if (data[0] == 0x07)
                {
                    svr_num++;
                    break;
                }
                else
                {
                    //log("[keep-alive2] recv2/unexpected", pkt = data);
                    Console.WriteLine("[keep-alive2] recv2/unexpected");
                    Console.WriteLine(BitConverter.ToString(data));
                }
            }
            //log("[keep-alive2] recv2", pkt= data)
            Console.WriteLine("[keep-alive2] recv2");
            Console.WriteLine(BitConverter.ToString(data));
            tail = Encoding.ASCII.GetString(data, 16, 20 - 16);


            ran += random.Next(1, 10);
            packet = keep_alive_package_builder(svr_num, ran, tail, 3, false);
            //log("[keep-alive2] send3", pkt= packet)
            Console.WriteLine("[keep-alive2] send3");
            Console.WriteLine(BitConverter.ToString(Encoding.ASCII.GetBytes(packet)));
            s.SendTo(Encoding.ASCII.GetBytes(packet), target);
            while (true)
            {
                int datalen = s.ReceiveFrom(data, ref remoteEP);
                if (data[0] == 0x07)
                {
                    svr_num++;
                    break;
                }
                else
                {
                    //log("[keep-alive2] recv3/unexpected", pkt = data)
                    Console.WriteLine("[keep-alive2] recv3/unexpected");
                    Console.WriteLine(BitConverter.ToString(data));
                }
            }
            //log("[keep-alive2] recv3", pkt= data)
            Console.WriteLine("[keep-alive2] recv3");
            Console.WriteLine(BitConverter.ToString(data));
            tail = Encoding.ASCII.GetString(data, 16, 20 - 16);
            //log("[keep-alive2] keep-alive2 loop was in daemon.")
            Console.WriteLine("[keep-alive2] keep-alive2 loop was in daemon.");

            int i = svr_num;
            while (true)
            {
                try
                {
                    ran += random.Next(1, 10);
                    packet = keep_alive_package_builder(i, ran, tail, 1, false);
                    //log("[keep_alive2] send", str(i), pkt = packet)
                    Console.WriteLine("[keep_alive2] send");
                    Console.WriteLine(BitConverter.ToString(Encoding.ASCII.GetBytes(packet)));
                    s.SendTo(Encoding.ASCII.GetBytes(packet), target);
                    int datalen = s.ReceiveFrom(data, ref remoteEP);
                    //log("[keep_alive2] recv", pkt = data)
                    Console.WriteLine("[keep_alive2] recv");
                    Console.WriteLine(BitConverter.ToString(data));
                    tail = Encoding.ASCII.GetString(data, 16, 20 - 16);

                    ran += random.Next(1, 10);
                    packet = keep_alive_package_builder(i + 1, ran, tail, 3, false);
                    s.SendTo(Encoding.ASCII.GetBytes(packet), target);
                    //log("[keep_alive2] send", str(i + 1), pkt = packet)
                    Console.WriteLine("[keep_alive2] send");
                    Console.WriteLine(BitConverter.ToString(Encoding.ASCII.GetBytes(packet)));
                    remoteEP = new IPEndPoint(IPAddress.Any, port);
                    datalen = s.ReceiveFrom(data, ref remoteEP);
                    //log("[keep_alive2] recv", pkt = data)
                    Console.WriteLine("[keep_alive2] recv");
                    Console.WriteLine(BitConverter.ToString(data));
                    tail = Encoding.ASCII.GetString(data, 16, 20 - 16);
                    i = (i + 2) % 0xFF;
                    Thread.Sleep(10 * 1000);
                }
                catch (Exception e)
                {
                }
            }
        }
    }
}
