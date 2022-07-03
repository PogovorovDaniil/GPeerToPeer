﻿namespace GPeerToPeer.Client
{
    public interface IPTPClient
    {
        public PTPNode GetSelfKey(PTPNode router);
        public PTPNode selfNode { get; }
        public bool ReachConnection(PTPNode node);
        public bool ReachConnection(string nodeKey);
        public Task<bool> ReachConnectionAsync(string nodeKey);
        public bool SendMessageTo(PTPNode node, byte[] message);
        public void Close();
        public Task<bool> SendMessageToAsync(PTPNode node, byte[] message);
        public delegate void ProcessMessageFromHandler(byte[] message, PTPNode node);
        public event ProcessMessageFromHandler ReceiveMessageFrom;

        public delegate void LogPacketHandler(string message, PTPNode node);
        public event LogPacketHandler Log;
        public void Work();
    }
}
