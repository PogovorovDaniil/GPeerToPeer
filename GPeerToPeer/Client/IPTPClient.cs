namespace GPeerToPeer.Client
{
    public interface IPTPClient
    {
        public PTPNode GetSelfKey(PTPNode router);
        public PTPNode selfNode { get; }
        public bool ReachConnection(PTPNode node);
        public bool ReachConnection(string nodeKey);
        public Task<bool> ReachConnectionAsync(string nodeKey);
        public bool SendMessageTo(PTPNode node, byte[] message, byte channel = 0);
        public void SendMessageWithoutConfirmationTo(PTPNode node, byte[] message, byte channel = 0);
        public Task<bool> SendMessageToAsync(PTPNode node, byte[] message, byte channel = 0);

        public bool ReceiveMessageFrom(ref PTPNode node, ref byte[] message, byte channel = 0);
        public bool ReceiveMessageWithoutConfirmationFrom(ref PTPNode node, ref byte[] message, byte channel = 0);
        public void Close();

        public delegate void LogPacketHandler(string message, PTPNode node);
#if DEBUG
        public event LogPacketHandler Log;
#endif
        public void Work();
    }
}
