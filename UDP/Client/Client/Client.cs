using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace Client
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

	internal class Client
	{
		private UdpClient socket;       // Client socket
		private IPEndPoint endPoint;    // Server socket

		public bool IsRunning { get; set; }

		public Client()
		{
			socket = new UdpClient();
			endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000);
		}


		public void Run()
		{
			IsRunning = true;

			Console.WriteLine("Listening for messages");

			for(int i = 0; i < 10; i++)
			{
				SendPacket($"Connect message nr: {i + 1}", endPoint);
			}

			socket.BeginReceive(ReceiveCallback, null);
		}

		public void SendMessage(string message)
		{
			SendPacket(message, endPoint);
		}

		private void SendPacket(string message, IPEndPoint endPoint)
		{
			Packet packet = new Packet(message);
			socket.Send(packet.Buffer, packet.Buffer.Length, endPoint);
		}

		private void ReceiveCallback(IAsyncResult result)
		{
			IPEndPoint senderEndpoint = null;
			Packet packet = new Packet(socket.EndReceive(result, ref senderEndpoint));

			Console.WriteLine($"Received from {senderEndpoint}: {packet.Message}");

			socket.BeginReceive(ReceiveCallback, null);
		}

	}
}
