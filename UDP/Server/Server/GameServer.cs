using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
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
		public IPEndPoint EndPoint { get; }
		public ConnectionState State { get; set; }
		public string Name { get; set; }
		public bool IsPlaying { get; set; }

		public PlayerInfo(string name, IPEndPoint endPoint, ConnectionState state)
		{
			Name = name;
			EndPoint = endPoint;
			State = state;
			IsPlaying = false;
		}
	}

	internal class GameServer
	{
		private readonly int PORT = 5000;

		private UdpClient socket;

		private List<PlayerInfo> players;
		private Position[] playerPositions;
		private TimeSpan serverTime;

		public GameServer()
		{
			players = new List<PlayerInfo>();
			playerPositions = new Position[]
			{
				new Position(0,0,0),
				new Position(0,0,0),
				new Position(0,0,0)
			};
			serverTime = DateTime.Now.TimeOfDay;

			socket = new UdpClient(PORT);
			Console.WriteLine($"Server listening on port: {PORT}");

		}

		public void Run()
		{
			socket.BeginReceive(ReceiveCallback, null);
		}

		private void ReceiveCallback(IAsyncResult result)
		{
			IPEndPoint endPoint = null;
			byte[] data = socket.EndReceive(result, ref endPoint);

			if (data?.Length != 0)
			{
				ConnectionState packetState = (ConnectionState)BitConverter.ToInt16(data, 0);

				int nameLength = BitConverter.ToInt32(data, 2);
				string name = Encoding.ASCII.GetString(data, 6, nameLength);

				if(packetState == ConnectionState.Connecting)
				{
					AddPlayer(endPoint, name, packetState);
					SendAcceptPacket(endPoint);
				}
				else if(packetState == ConnectionState.AcceptConnection)
				{
					UpdatePlayer(endPoint, name, ConnectionState.Connected);
					BroadcastWorldState();
				}
				else if(packetState == ConnectionState.Connected)
				{
					UpdatePlayer(endPoint, name, ConnectionState.Connected);
					BroadcastWorldState();
				}
			}

			socket.BeginReceive(ReceiveCallback, null);
		}

		private void AddPlayer(IPEndPoint endPoint, string name, ConnectionState state)
		{
			// If it's a new client, add to the client list
			if (GetPlayer(endPoint) == null)
			{
				Console.WriteLine($"Player: {name}({endPoint}) connected to server.");
				players.Add(new PlayerInfo(name, endPoint, state));
			}
		}

		public PlayerInfo GetPlayer(IPEndPoint endPoint)
		{
			return players.Find(p => p.EndPoint.Equals(endPoint));
		}

		public void UpdatePlayer(IPEndPoint endPoint, string name, ConnectionState state)
		{
			PlayerInfo player = GetPlayer(endPoint);

			if(player != null)
			{
				player.Name = name;
				player.State = state;
			}
		}

		private int GetPlayerNumber(IPEndPoint endPoint)
		{
			return players.FindIndex(p => p.EndPoint.Equals(endPoint));
		}


		// ---- Send packets---- //


		private void SendAcceptPacket(IPEndPoint endPoint)
		{
			byte[] data = GenerateAcceptPacket();

			for(int i = 0; i < 10; i++)
			{
				socket.Send(data, data.Length, endPoint); // Send connection accepted packet
			}
		}

		private void BroadcastWorldState()
		{
			try
			{
				byte[] data = GenerateWorldPacket();

				foreach(PlayerInfo player in players)
				{
					socket.Send(data, data.Length, player.EndPoint);
				}
			}
			catch(Exception e)
			{
				Console.WriteLine($"Error! {e}");
			}
		}


		// ---- Generated packets ---- //

		private byte[] GenerateAcceptPacket()
		{
			byte[] data, buffer;
			using (MemoryStream stream = new MemoryStream())
			{
				buffer = BitConverter.GetBytes(serverTime.Ticks);
				stream.Write(buffer, 0, buffer.Length);

				buffer = BitConverter.GetBytes((ushort)ConnectionState.AcceptConnection);
				stream.Write(buffer, 0, buffer.Length);

				data = stream.GetBuffer();
			}

			return data;
		}

		/// <summary>
		/// (long) current TimeSpan of server
		/// (ushort) ConnectionState
		/// 
		/// (float) player1.posX 
		/// (float) player1.posY 
		/// (float) player1.posZ 
		/// 
		/// (float) player2.posX 
		/// (float) player2.posY 
		/// (float) player2.posZ
		/// 
		/// (float) player3.posX 
		/// (float) player3.posY 
		/// (float) player3.posZ
		/// 
		/// (int32) number of connected clients
		/// 
		/// (int32) nameLength of client 1
		/// (string) name of client 1
		/// (bool) is active player
		/// 
		/// Repeat last steps until all clients names are listed
		/// </summary>
		/// <returns></returns>
		private byte[] GenerateWorldPacket()
		{
			byte[] data, buffer;
			using (MemoryStream stream = new MemoryStream())
			{
				buffer = BitConverter.GetBytes(serverTime.Ticks);
				stream.Write(buffer, 0, buffer.Length);

				buffer = BitConverter.GetBytes((ushort)ConnectionState.Connected);
				stream.Write(buffer, 0, buffer.Length);

				for (int i = 0; i < 3; i++)
				{
					Position pos = playerPositions[i];

					buffer = BitConverter.GetBytes(pos.x);
					stream.Write(buffer, 0, buffer.Length);

					buffer = BitConverter.GetBytes(pos.y);
					stream.Write(buffer, 0, buffer.Length);

					buffer = BitConverter.GetBytes(pos.z);
					stream.Write(buffer, 0, buffer.Length);
				}

				for(int i = 0; i < players.Count; i++)
				{
					byte[] nameBuffer = Encoding.ASCII.GetBytes(players[i].Name);
					buffer = BitConverter.GetBytes(nameBuffer.Length);
					stream.Write(buffer, 0, buffer.Length);
					stream.Write(nameBuffer, 0, nameBuffer.Length);

					buffer = BitConverter.GetBytes(players[i].IsPlaying);
					stream.Write(buffer, 0, buffer.Length);
				}

				data = stream.GetBuffer();
			}

			return data;
		}
	}
}
