using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class SocketMessage : MonoBehaviour
{
    public IEnumerator Start()
    {
        IPAddress ipAddress = IPAddress.Parse("25.15.45.201");
        IPEndPoint ipEndPoint = new(ipAddress, 11_000);

        using Socket listener = new
        (
            ipEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp
        );

        listener.Bind(ipEndPoint);
        listener.Listen(100);

        var handler = listener.Accept();
        while (true)
        {
            // Receive message.
            var buffer = new byte[1_024];
            var received = handler.Receive(buffer, SocketFlags.None);
            var response = Encoding.UTF8.GetString(buffer, 0, received);

            var eom = "<|EOM|>";
            if (response.IndexOf(eom) > -1 /* is end of message */)
            {
                Debug.Log($"Socket server received message: '{ response.Replace(eom, "")}'");

                var ackMessage = "<|ACK|>";
                var echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                handler.Send(echoBytes, 0);
                Debug.Log($"Socket server sent acknowledgment: '{ackMessage}'");

                yield break;
            }
        }
    }
}
