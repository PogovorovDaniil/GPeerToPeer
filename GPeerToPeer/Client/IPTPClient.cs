namespace GPeerToPeer.Client
{
    public interface IPTPClient
    {
        public PTPNode GetSelfKey(PTPNode router);
        public PTPNode selfNode { get; }
        public bool ReachConnection(PTPNode node);
        public bool ReachConnection(string nodeKey);
        public Task<bool> ReachConnectionAsync(string nodeKey);
        public bool SendMessageTo(PTPNode ptpnode, byte[] message);
        public Task<bool> SendMessageToAsync(PTPNode ptpnode, byte[] message);
        public delegate void ProcessMessageFromHandler(byte[] message, PTPNode node);
        public event ProcessMessageFromHandler ReceiveMessageFrom;
        public void Work();
    }
}
