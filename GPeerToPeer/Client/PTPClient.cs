using System.Net;
using System.Net.Sockets;
using GPeerToPeer.Constants;

namespace GPeerToPeer.Client
{
    public class PTPClient : IPTPClient
    {
        private const int MAX_MESSAGE_SIZE = 1024;
        private const int BUFFER_SIZE = MAX_MESSAGE_SIZE + 8 + 1;
        private const int PACKETS_TO_FIX = 20;
        private const int SOCKET_TIMEOUT = 2000;
        private const int SOCKET_TIMES_OUT = 5;
        private const int CONFORM_SIZE = 8;
        private const int NODE_LIVE_TIME = 100_000;

        private byte[] buffer;
        private readonly object bufferLock = new object();
        private readonly Socket socket;
        private readonly TempList<PTPNode> nodes;
        public PTPNode selfNode { get; private set; }
        private readonly TempList<byte[]> conforms;
        private readonly Dictionary<byte, TempList<byte[]>> packets;

        private PTPClient(int socketPort)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, socketPort));
            socket.ReceiveTimeout = SOCKET_TIMEOUT;
            socket.SendTimeout = SOCKET_TIMEOUT;
            nodes = new TempList<PTPNode>(NODE_LIVE_TIME);
            conforms = new TempList<byte[]>(2 * SOCKET_TIMEOUT * SOCKET_TIMES_OUT);

            packets = new Dictionary<byte, TempList<byte[]>>()
            {
                { Act.RECEIVE, new TempList<byte[]>(SOCKET_TIMEOUT) },
                { Act.REACH_CONNECTION_RESPONSE, new TempList<byte[]>(SOCKET_TIMEOUT) },
                { Act.KEY_RESPONSE, new TempList<byte[]>(SOCKET_TIMEOUT) }
            };
        }
        public PTPClient(string providerIp, int providerPort, int socketPort) : this(socketPort)
        {
            selfNode = RawGetSelfKey(new PTPNode(providerIp, providerPort));
        }
        public PTPClient(string providerKey, int socketPort) : this(socketPort)
        {
            selfNode = RawGetSelfKey(new PTPNode(providerKey));
        }

        private static bool FirstEquals<T>(T[] a, T[] b, int count) where T : IComparable<T>
        {
            if (count > a.Length && count > b.Length) return false;
            for (int i = 0; i < count; i++)
                if (!a[i].Equals(b[i])) return false;
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
                return new((IPEndPoint)fromEndPoint);
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
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            Task t = Task.Run(() =>
            {
                byte[] bytes = new byte[BUFFER_SIZE];
                byte[] myNodeBytes = PTPNode.ByteArrayFromKey(node.Key);
                byte[] message = new byte[myNodeBytes.Length + 1];
                message[0] = Act.REACH_CONNECTION;
                while (true)
                {
                    Array.ConstrainedCopy(myNodeBytes, 0, message, 1, myNodeBytes.Length);
                    SendTo(node, message);
                    if (packets[Act.REACH_CONNECTION_RESPONSE].Get(ref bytes) && FirstEquals(bytes, myNodeBytes, myNodeBytes.Length)) return;
                }
            }, ct);
            bool isManaged = t.Wait(SOCKET_TIMEOUT * SOCKET_TIMES_OUT);
            tokenSource.Cancel();
            if (isManaged) return true;
            return false;
        }
        public bool ReachConnection(string nodeKey) => ReachConnection(new PTPNode(nodeKey));

        public async Task<bool> ReachConnectionAsync(PTPNode node)
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;
            Task t = Task.Run(() =>
            {
                byte[] bytes = new byte[BUFFER_SIZE];
                byte[] myNodeBytes = PTPNode.ByteArrayFromKey(node.Key);
                byte[] message = new byte[myNodeBytes.Length + 1];
                message[0] = Act.REACH_CONNECTION;
                while (!ct.IsCancellationRequested)
                {
                    Array.ConstrainedCopy(myNodeBytes, 0, message, 1, myNodeBytes.Length);
                    SendTo(node, message);
                    if (packets[Act.REACH_CONNECTION_RESPONSE].Get(ref bytes) && FirstEquals(bytes, myNodeBytes, myNodeBytes.Length)) return;
                }
            }, ct);
            bool isManaged = await Task.WhenAny(t, Task.Delay(SOCKET_TIMEOUT * SOCKET_TIMES_OUT)) == t;
            tokenSource.Cancel();
            if (isManaged) return true;
            return false;
        }
        public async Task<bool> ReachConnectionAsync(string nodeKey) => await ReachConnectionAsync(new PTPNode(nodeKey));

        private PTPNode RawGetSelfKey(PTPNode helper)
        {
            lock (bufferLock)
            {
                FixNat(helper);
                SendTo(helper, Array.Empty<byte>());
                PTPNode? node;
                do node = ReceiveFrom();
                while (!(node.HasValue && node.Value.Key == helper.Key));
                byte[] keyBytes = new byte[PTPNode.PTP_NODE_KEY_SIZE];
                Array.ConstrainedCopy(buffer, 1, keyBytes, 0, keyBytes.Length);
                return new PTPNode(PTPNode.KeyFromByteArray(keyBytes));
            }
        }
        public PTPNode GetSelfKey(PTPNode helper)
        {
            lock (bufferLock)
            {
                FixNat(helper);
                do SendTo(helper, Array.Empty<byte>());
                while (!packets[Act.KEY_RESPONSE].Get(ref buffer));
                return new PTPNode(PTPNode.KeyFromByteArray(buffer));
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

        public async Task<bool> SendMessageToAsync(PTPNode node, byte[] message)
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

            return await Task.Run(() =>
            {
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
            });
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
                                case Act.KEY_RESPONSE:
                                    {
                                        byte[] keyBytes = new byte[PTPNode.PTP_NODE_KEY_SIZE];
                                        Array.ConstrainedCopy(buffer, 1, keyBytes, 0, keyBytes.Length);
                                        packets[Act.KEY_RESPONSE].Add(keyBytes);
                                        break;
                                    }
                            }
                        }
                        else if (buffer.Length == 0)
                        {
                            byte[] keyBytesToSend = new byte[PTPNode.PTP_NODE_KEY_SIZE + 1];
                            Array.ConstrainedCopy(PTPNode.ByteArrayFromKey(node.Value.Key), 0, keyBytesToSend, 1, keyBytesToSend.Length);
                            keyBytesToSend[0] = Act.KEY_RESPONSE;
                            SendTo(node.Value, keyBytesToSend);
                        }
                    }
                }
            }
        }
    }
}
