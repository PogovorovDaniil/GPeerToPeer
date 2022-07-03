using System.Net;
using System.Net.Sockets;
using System.Text;

Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
socket.Bind(new IPEndPoint(IPAddress.Parse("194.61.3.168"), 22345));
Console.WriteLine("Router started.");
while (true)
{
    EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
    byte[] buffer = new byte[256];
    if (socket.ReceiveFrom(buffer, ref endPoint) == 0)
    {
        if(endPoint is IPEndPoint ipEndPoint)
        {
#pragma warning disable CS0618
            string from =   ipEndPoint.Address.Address.ToString("X2").PadLeft(8, '0') +
                            ipEndPoint.Port.ToString("X2").PadLeft(4, '0');
#pragma warning restore CS0618
            Console.WriteLine(endPoint.ToString() + " - " + from);
            byte[] bytes = Encoding.UTF8.GetBytes(from);
            Array.ConstrainedCopy(bytes, 0, buffer, 1, bytes.Length);
            Array.Resize(ref buffer, bytes.Length + 1);
            buffer[0] = 6; // KEY_RESPONSE
            socket.SendTo(buffer, endPoint);
        }
    }
}