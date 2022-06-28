using System.Globalization;
using System.Net;
using System.Text;

namespace GPeerToPeer.Client
{
    public struct PTPNode
    {
        public const int PTP_NODE_KEY_SIZE = 12;

        internal IPEndPoint EndPoint;
        public readonly string Key;
        public PTPNode(string key)
        {
            Key = key;
            EndPoint = EndPointFromKey(Key);
        }
        public PTPNode(IPEndPoint endPoint)
        {
            EndPoint = endPoint;
            Key = KeyFromEndPoint(endPoint);
        }
        public PTPNode(string ip, int port)
        {
            EndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Key = KeyFromEndPoint(EndPoint);
        }
        internal static IPEndPoint EndPointFromKey(string key)
        {
            try
            {
                long ip = long.Parse(key.Substring(0, 8), NumberStyles.HexNumber);
                int port = int.Parse(key.Substring(8, 4), NumberStyles.HexNumber);
                return new IPEndPoint(new IPAddress(ip), port);
            }
            catch
            {
                throw new Exception("Invalid key format!");
            }
        }
        internal static string KeyFromEndPoint(IPEndPoint endPoint)
        {
#pragma warning disable CS0618
            return
                endPoint.Address.Address.ToString("X2").PadLeft(8, '0') +
                endPoint.Port.ToString("X2").PadLeft(4, '0');
#pragma warning restore CS0618
        }

        internal static string KeyFromByteArray(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes).Substring(0, PTP_NODE_KEY_SIZE);
        }

        internal static byte[] ByteArrayFromKey(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public override bool Equals(object obj)
        {
            if (obj is PTPNode ptphost) return ptphost.Key.Equals(Key);
            return base.Equals(obj);
        }

        public override int GetHashCode() => HashCode.Combine(Key);
    }
}