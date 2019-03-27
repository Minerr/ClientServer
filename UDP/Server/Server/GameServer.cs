using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace GameServer
{
	internal enum ConnectionState
	{
		Disconnected = 0,
		Connecting = 1,
		Connected = 2,
		AcceptConnection = 3,
		Disconnect = 4
	}

	internal class Position
	{
		public float x;
		public float y;
		public float z;

		public Position(float x, float y, float z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public override string ToString()
		{
			return $"x={x} y={y} z={z}";
		}
	}

	internal class PlayerInfo
	{
		public string name;
		public Position position;
		public IPEndPoint endPoint;

		public PlayerInfo(string name, IPEndPoint endPoint)
		{
			this.name = name;
			this.endPoint = endPoint;
			position = new Position(0, 0, 0);
		}
	}

	internal class GameServer
	{
		private readonly int PORT = 5000;

		private UdpClient socket;

		private List<PlayerInfo> players;
		private TimeSpan serverTime;

		public GameServer()
		{
			players = new List<PlayerInfo>();
			serverTime = DateTime.Now.TimeOfDay;

			socket = new UdpClient("127.0.0.1", PORT);
			Console.WriteLine($"Server listening on port: {PORT}");

		}

		public void Run()
		{
			socket.BeginReceive(ReceiveCallback, null);
		}


		private void BroadcastWorldState()
		{
			try
			{
				byte[] data = GenerateWorldPacket();

				foreach (PlayerInfo player in players)
				{
					socket.Send(data, data.Length, player.endPoint);
				}
			}
			catch(Exception e)
			{
				Console.WriteLine($"Error! {e}");
			}
		}

		private void ReceiveCallback(IAsyncResult result)
		{
			IPEndPoint endPoint = null;
			byte[] data = socket.EndReceive(result, ref endPoint);
			Console.WriteLine($"{endPoint} contacted the server.");

			if (data?.Length != 0)
			{

				ConnectionState state = (ConnectionState)BitConverter.ToInt16(data, 0);

				Console.WriteLine($"Received from {endPoint}: {state.ToString()}");

				if (state == ConnectionState.Connecting)
				{
					int nameLength = BitConverter.ToInt32(data, 2);
					string name = Encoding.ASCII.GetString(data, 6, nameLength);

					AddPlayer(endPoint, name);

					data = GenerateAcceptPacket((ushort)GetPlayerNumber(endPoint));

					for (int i = 0; i < 10; i++)
					{
						socket.Send(data, data.Length, endPoint);
					}
				}

				if (state == ConnectionState.Connected)
				{
					BroadcastWorldState();
				}
			}

			socket.BeginReceive(ReceiveCallback, null);
		}

		private void AddPlayer(IPEndPoint endPoint, string name)
		{
			if (!ExistsClient(endPoint))
			{ // If it's a new client, add to the client list
				Console.WriteLine($"{endPoint} connected to server.");
				players.Add(new PlayerInfo(name, endPoint));
			}
		}

		private bool ExistsClient(IPEndPoint endPoint)
		{
			return players.Exists(p => p.endPoint == endPoint);
		}

		private int GetPlayerNumber(IPEndPoint endPoint)
		{
			return players.FindIndex(p => p.endPoint == endPoint);
		}

		private byte[] GenerateAcceptPacket(ushort playerNumber)
		{
			byte[] data, buffer;
			using (MemoryStream stream = new MemoryStream())
			{
				buffer = BitConverter.GetBytes(serverTime.Ticks);
				stream.Write(buffer, 0, buffer.Length);

				buffer = BitConverter.GetBytes((ushort)ConnectionState.AcceptConnection);
				stream.Write(buffer, 0, buffer.Length);

				buffer = BitConverter.GetBytes(playerNumber);
				stream.Write(buffer, 0, buffer.Length);

				data = stream.GetBuffer();
			}

			return data;
		}

		private byte[] GenerateWorldPacket()
		{
			byte[] data, buffer;
			using (MemoryStream stream = new MemoryStream())
			{
				PlayerInfo player;

				buffer = BitConverter.GetBytes(serverTime.Ticks);
				stream.Write(buffer, 0, buffer.Length);

				buffer = BitConverter.GetBytes((ushort)ConnectionState.AcceptConnection);
				stream.Write(buffer, 0, buffer.Length);

				for (int i = 0; i < 3; i++)
				{
					player = players[i];
					Position pos = player.position;

					byte[] nameBuffer = Encoding.ASCII.GetBytes(player.name);
					buffer = BitConverter.GetBytes(nameBuffer.Length);
					stream.Write(buffer, 0, buffer.Length);
					stream.Write(nameBuffer, 0, nameBuffer.Length);

					buffer = BitConverter.GetBytes(pos.x);
					stream.Write(buffer, 0, buffer.Length);

					buffer = BitConverter.GetBytes(pos.y);
					stream.Write(buffer, 0, buffer.Length);

					buffer = BitConverter.GetBytes(pos.z);
					stream.Write(buffer, 0, buffer.Length);
				}

				data = stream.GetBuffer();
			}

			return data;
		}
	}
}
