using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GPeerToPeer
{
    public class PTPClient : IPTPClient
    {
        private const int MAX_MESSAGE_SIZE = 1024;
        private const int BUFFER_SIZE = MAX_MESSAGE_SIZE + 8 + 1;
        private const int PACKETS_TO_FIX = 20;
        private const int SOCKET_TIMEOUT = 2000;
        private const int SOCKET_TIMES_OUT = 5;
        private const int CONFORM_SIZE = 8;

        private byte[] buffer;
        private object bufferLock = new object();
        private Socket socket;
        private List<PTPNode> nodes;
        public PTPNode selfNode { get; private set; }
        private TempList<byte[]> conforms;
        private Dictionary<byte, TempList<byte[]>> packets;

        private PTPClient(int socketPort)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, socketPort));
            socket.ReceiveTimeout = SOCKET_TIMEOUT;
            socket.SendTimeout = SOCKET_TIMEOUT;
            nodes = new List<PTPNode>();
            conforms = new TempList<byte[]>(2 * SOCKET_TIMEOUT * SOCKET_TIMES_OUT);

            packets = new Dictionary<byte, TempList<byte[]>>();
            packets[Act.RECEIVE] = new TempList<byte[]>(SOCKET_TIMEOUT);
            packets[Act.REACH_CONNECTION_RESPONSE] = new TempList<byte[]>(SOCKET_TIMEOUT);
        }
        public PTPClient(string providerIp, int providerPort, int socketPort) : this(socketPort)
        {
            selfNode = GetSelfKey(new PTPNode(providerIp, providerPort));
        }
        public PTPClient(string providerKey, int socketPort) : this(socketPort)
        {
            selfNode = GetSelfKey(new PTPNode(providerKey));
        }

        private bool FirstEquals<T>(T[] a, T[] b, int count) where T : IComparable<T>
        {
            if (count > a.Length && count > b.Length) return false;
            for(int i = 0; i < count; i++)
            {
                if (!a[i].Equals(b[i])) return false;
            }
            return true;
        }
        private PTPNode? ReceiveFrom()
        {
            try
            {
                buffer = new byte[BUFFER_SIZE];
                EndPoint fromEndPoint = new IPEndPoint(IPAddress.Any, 0);
                int size = socket.ReceiveFrom(buffer, ref fromEndPoint);
                Array.Resize(ref buffer, size);
                return new PTPNode((IPEndPoint)fromEndPoint);
            }
            catch
            {
                return null;
            }
        }
        private void SendTo(PTPNode node, byte[] bytes)
        {
            socket.SendTo(bytes, node.EndPoint);
        }

        public void FixNat(PTPNode node)
        {
            for (int i = 0; i < PACKETS_TO_FIX; i++) SendTo(node, new byte[1] { Act.NOTHING });
        }
        public bool ReachConnection(PTPNode node)
        {
            Task t = Task.Run(() =>
            {
                byte[] bytes = new byte[BUFFER_SIZE];
                byte[] myNodeBytes = PTPNode.byteArrayFromKey(node.Key);
                byte[] message = new byte[myNodeBytes.Length + 1];
                message[0] = Act.REACH_CONNECTION;
                while (true)
                {
                    Array.ConstrainedCopy(myNodeBytes, 0, message, 1, myNodeBytes.Length);
                    SendTo(node, message);
                    if (packets[Act.REACH_CONNECTION_RESPONSE].Get(ref bytes) && FirstEquals(bytes, myNodeBytes, myNodeBytes.Length)) return;
                    Thread.Sleep(SOCKET_TIMEOUT / 10);
                }
            });
            if (!t.Wait(2 * SOCKET_TIMEOUT * SOCKET_TIMES_OUT)) return false;
            return true;
        }
        public bool ReachConnection(string nodeKey) => ReachConnection(new PTPNode(nodeKey));

        public PTPNode GetSelfKey(PTPNode router)
        {
            lock (bufferLock)
            {
                FixNat(router);
                SendTo(router, new byte[0] { });
                PTPNode? node;
                do node = ReceiveFrom();
                while (!(node.HasValue && node.Value.Key == router.Key));
                return new PTPNode(PTPNode.keyFromByteArray(buffer));
            } 
        }

        public bool SendMessageTo(PTPNode node, byte[] message)
        {
            if (!nodes.Contains(node))
            {
                nodes.Add(node);
                FixNat(node);
            }
            byte[] conform = new byte[CONFORM_SIZE];
            Random.Shared.NextBytes(conform);
            byte[] allMessage = new byte[conform.Length + message.Length + 1];
            allMessage[0] = Act.SEND;
            conform.CopyTo(allMessage, 1);
            message.CopyTo(allMessage, conform.Length + 1);
            bool received;
            int times = 0;
            byte[] conformFrom = new byte[CONFORM_SIZE];
            do
            {
                SendTo(node, allMessage);
                received = packets[Act.RECEIVE].Get(ref conformFrom);
                if (!received && ++times == SOCKET_TIMES_OUT) return false;
            } while (!received || !FirstEquals(conformFrom, conform, conform.Length));
            return true;
        }

        public event IPTPClient.ProcessMessageFromHandler ReceiveMessageFrom;
        public void Work()
        {
            while (true)
            {
                lock (bufferLock)
                {
                    PTPNode? node = ReceiveFrom();
                    if (node.HasValue)
                    {
                        if (buffer.Length > 0)
                        {
                            switch (buffer[0])
                            {
                                case Act.SEND:
                                    {
                                        if (buffer.Length > CONFORM_SIZE)
                                        {
                                            byte[] answer = new byte[CONFORM_SIZE + 1];
                                            byte[] conform = new byte[CONFORM_SIZE];
                                            byte[] message = new byte[buffer.Length - CONFORM_SIZE - 1];
                                            Array.ConstrainedCopy(buffer, 0, answer, 0, answer.Length);
                                            answer[0] = Act.RECEIVE;
                                            Array.ConstrainedCopy(buffer, 1, conform, 0, conform.Length);
                                            Array.ConstrainedCopy(buffer, CONFORM_SIZE + 1, message, 0, message.Length);
                                            SendTo(node.Value, answer);
                                            if (!conforms.Contains(conform))
                                            {
                                                conforms.Add(conform);
                                                ReceiveMessageFrom?.Invoke(message, node.Value);
                                            }
                                        }
                                        break;
                                    }
                                case Act.NOTHING:
                                    { 
                                        break;
                                    }
                                case Act.RECEIVE:
                                    { 
                                        byte[] conform = new byte[CONFORM_SIZE];
                                        Array.ConstrainedCopy(buffer, 1, conform, 0, conform.Length);
                                        packets[Act.RECEIVE].Add(conform);
                                        break;
                                    }
                                case Act.REACH_CONNECTION:
                                    {
                                        buffer[0] = Act.REACH_CONNECTION_RESPONSE;
                                        SendTo(node.Value, buffer);
                                        break;
                                    }
                                case Act.REACH_CONNECTION_RESPONSE:
                                    {
                                        byte[] nodeBytes = new byte[PTPNode.PTP_NODE_KEY_SIZE];
                                        Array.ConstrainedCopy(buffer, 1, nodeBytes, 0, nodeBytes.Length);
                                        packets[Act.REACH_CONNECTION_RESPONSE].Add(nodeBytes);
                                        break;
                                    }
                            }
                        }
                        else if(buffer.Length == 0)
                        {
                            SendTo(node.Value, PTPNode.byteArrayFromKey(node.Value.Key));
                        }
                    }
                    else return;
                }
            }
        }
    }
}
