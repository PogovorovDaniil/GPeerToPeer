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
        private TempList<byte[]> receivePackets;

        private PTPClient(int socketPort)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, socketPort));
            socket.ReceiveTimeout = SOCKET_TIMEOUT;
            socket.SendTimeout = SOCKET_TIMEOUT;
            nodes = new List<PTPNode>();
            conforms = new TempList<byte[]>(2 * SOCKET_TIMEOUT * SOCKET_TIMES_OUT);
            receivePackets = new TempList<byte[]>(2 * SOCKET_TIMEOUT * SOCKET_TIMES_OUT);
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
                if (a[i].Equals(b[i])) return false;
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
            for (int i = 0; i < PACKETS_TO_FIX; i++) SendTo(node, new byte[1] { 0 });
        }

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
            PTPNode? nodeFrom;
            int times = 0;
            byte[] conformFrom = new byte[CONFORM_SIZE];
            do
            {
                SendTo(node, allMessage);
                nodeFrom = ReceiveFrom();
                if (buffer[0] == Act.RECEIVE)
                {
                    Array.ConstrainedCopy(buffer, 1, conformFrom, 0, conformFrom.Length);
                }
                if (!(nodeFrom.HasValue && buffer[0] == Act.RECEIVE) && ++times == SOCKET_TIMES_OUT) return false;
            } while (!nodeFrom.HasValue || !FirstEquals(conformFrom, conform, conform.Length));
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
                        if(buffer.Length > 0)
                        {
                            byte act = buffer[0];
                            if(act == Act.SEND)
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
                            }
                            else if(act == Act.NOTHING)
                            {
                                // do nothing
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
