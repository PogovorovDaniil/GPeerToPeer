using System;

namespace GPeerToPeer
{
    public interface IPTPClient
    {
        public PTPNode GetSelfKey(PTPNode router);
        public PTPNode selfNode { get; }
        public bool ReachConnection(PTPNode node);
        public bool ReachConnection(string nodeKey);
        public bool SendMessageTo(PTPNode ptpnode, byte[] message);
        public delegate void ProcessMessageFromHandler(byte[] message, PTPNode node);
        public event ProcessMessageFromHandler ReceiveMessageFrom;
        public void Work();
    }
}
