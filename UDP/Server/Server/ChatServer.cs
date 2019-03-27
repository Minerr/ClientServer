using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Server
{
	internal class Packet
	{
		public string Message { get; }
		public byte[] Buffer { get; }

		public Packet(string message)
		{
			Message = message;
			Buffer = Encoding.ASCII.GetBytes(message);
		}

		public Packet(byte[] buffer)
		{
			Buffer = buffer;
			Message = Encoding.ASCII.GetString(buffer);
		}
	}

	internal class ChatServer
	{
		private readonly int PORT = 5000;
		private UdpClient socket;
		private List<IPEndPoint> clientList = new List<IPEndPoint>();

		public ChatServer()
		{
			socket = new UdpClient(PORT);
			Console.WriteLine($"Server listening on port: {PORT}");
		}


		public void Run()
		{
			socket.BeginReceive(ReceiveCallback, null);
		}


		private void BroadcastPacket(Packet packet)
		{
			foreach(IPEndPoint ip in clientList)
			{
				socket.Send(packet.Buffer, packet.Buffer.Length, ip);
			}
		}

		private void ReceiveCallback(IAsyncResult result)
		{
			IPEndPoint endPoint = null;
			Packet packet = new Packet(socket.EndReceive(result, ref endPoint));

			AddClient(endPoint);

			Console.WriteLine($"Received from {endPoint}: {packet.Message}");

			// Broadcast to other clients
			BroadcastPacket(packet);

			socket.BeginReceive(ReceiveCallback, null);
		}


		private void AddClient(IPEndPoint endpoint)
		{
			if(clientList.Contains(endpoint) == false)
			{ // If it's a new client, add to the client list
				Console.WriteLine($"{endpoint} connected to server.");
				clientList.Add(endpoint);
			}
		}

	}
}
