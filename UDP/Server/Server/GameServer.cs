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
		Verification = 2,
		Connected = 3
	}

	internal enum Request
	{
		None = 0,
		Disconnect = 1,
		JoinGame = 2,
		JoinSpectators = 3
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
		public ushort playerNumber { get; set; }

		public PlayerInfo(string name, IPEndPoint endPoint, ConnectionState state)
		{
			Name = name;
			EndPoint = endPoint;
			State = state;
			playerNumber = 0;
		}
	}

	internal class GameServer
	{
		private readonly int PORT = 5000;

		private UdpClient socket;

		private List<PlayerInfo> clients;
		private Position[] playerPositions;
		private IPEndPoint[] players;
		private TimeSpan serverTime;

		public GameServer()
		{
			clients = new List<PlayerInfo>();
			players = new IPEndPoint[3];

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
				int nextIndex = 0;

				ConnectionState packetState = (ConnectionState)BitConverter.ToInt16(data, nextIndex);
				nextIndex += 2;

				int nameLength = BitConverter.ToInt32(data, nextIndex);
				nextIndex += 4;

				string name = Encoding.ASCII.GetString(data, nextIndex, nameLength);
				nextIndex += nameLength;

				if (packetState == ConnectionState.Connecting)
				{
					AddPlayer(endPoint, name, packetState);
					SendVerificationPacket(endPoint);
				}
				else if(packetState == ConnectionState.Verification)
				{
					UpdatePlayer(endPoint, name, ConnectionState.Connected);
					BroadcastWorldState();
				}
				else if(packetState == ConnectionState.Connected)
				{
					Request request = (Request)BitConverter.ToUInt16(data, nextIndex);
					nextIndex += 2;

					if(request == Request.JoinGame)
					{
						JoinGame(endPoint);
					}
					else if (request == Request.JoinSpectators)
					{
						JoinSpectators(endPoint);
					}
					else if (request == Request.Disconnect)
					{
						RemovePlayer(endPoint);
					}

					UpdatePlayer(endPoint, name, ConnectionState.Connected);
					BroadcastWorldState();
				}
			}

			socket.BeginReceive(ReceiveCallback, null);
		}

		private void RemovePlayer(IPEndPoint endPoint)
		{
			throw new NotImplementedException();
		}

		private void JoinSpectators(IPEndPoint endPoint)
		{
			throw new NotImplementedException();
		}

		// TODO: Refactor this ASAP
		private void JoinGame(IPEndPoint endPoint)
		{
			ushort playerNumber = 0;

			for (int i = 0; i < players.Length; i++)
			{
				IPEndPoint playerEndPoint = players[i];

				if(playerEndPoint == null)
				{
					playerEndPoint = endPoint;
					playerNumber = (ushort)(i + 1);

					clients[GetPlayerIndex(endPoint)].playerNumber = playerNumber;

					break;
				}
			}

			SendJoinGamePacket(playerNumber, endPoint);
		}

		private void AddPlayer(IPEndPoint endPoint, string name, ConnectionState state)
		{
			// If it's a new client, add to the client list
			if (GetPlayer(endPoint) == null)
			{
				Console.WriteLine($"Player: {name}({endPoint}) connected to server.");
				clients.Add(new PlayerInfo(name, endPoint, state));
			}
		}

		public PlayerInfo GetPlayer(IPEndPoint endPoint)
		{
			return clients.Find(p => p.EndPoint.Equals(endPoint));
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

		private int GetPlayerIndex(IPEndPoint endPoint)
		{
			return clients.FindIndex(p => p.EndPoint.Equals(endPoint));
		}


		// ---- Send packets---- //
		private void SendDataToClient(byte[] data, IPEndPoint endPoint)
		{
			for (int i = 0; i < 10; i++)
			{
				socket.Send(data, data.Length, endPoint);
			}
		}

		private void SendVerificationPacket(IPEndPoint endPoint)
		{
			byte[] data = GenerateVerificationPacket();
			SendDataToClient(data, endPoint);
		}

		private void SendJoinGamePacket(ushort playerNumber, IPEndPoint endPoint)
		{
			byte[] data = GenerateJoinGamePacket(playerNumber);
			SendDataToClient(data, endPoint);
		}


		// TODO: Call 30 times per second
		private void BroadcastWorldState()
		{
			try
			{
				byte[] data = GenerateWorldPacket();

				foreach(PlayerInfo player in clients)
				{
					//Console.WriteLine($"Sending world data to: {player.Name}");
					socket.Send(data, data.Length, player.EndPoint);
				}
			}
			catch(Exception e)
			{
				Console.WriteLine($"Error! {e}");
			}
		}


		// ---- Generated packets ---- //


		private byte[] GenerateJoinGamePacket(ushort playerNumber)
		{
			byte[] data, buffer;
			using (MemoryStream stream = new MemoryStream())
			{
				buffer = BitConverter.GetBytes(serverTime.Ticks);
				stream.Write(buffer, 0, buffer.Length);

				buffer = BitConverter.GetBytes((ushort)ConnectionState.Connected);
				stream.Write(buffer, 0, buffer.Length);

				buffer = BitConverter.GetBytes((ushort)Request.JoinGame);
				stream.Write(buffer, 0, buffer.Length);

				buffer = BitConverter.GetBytes(playerNumber);
				stream.Write(buffer, 0, buffer.Length);

				data = stream.GetBuffer();
			}

			return data;
		}

		private byte[] GenerateVerificationPacket()
		{
			byte[] data, buffer;
			using (MemoryStream stream = new MemoryStream())
			{
				buffer = BitConverter.GetBytes(serverTime.Ticks);
				stream.Write(buffer, 0, buffer.Length);

				buffer = BitConverter.GetBytes((ushort)ConnectionState.Verification);
				stream.Write(buffer, 0, buffer.Length);

				data = stream.GetBuffer();
			}

			return data;
		}

		/// <summary>
		/// (long) current TimeSpan of server
		/// (ushort) ConnectionState
		/// (ushort) Request
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
		/// (ushort) player number (0,1,2,3)
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

				buffer = BitConverter.GetBytes((ushort)Request.None);
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

				for(int i = 0; i < clients.Count; i++)
				{
					byte[] nameBuffer = Encoding.ASCII.GetBytes(clients[i].Name);
					buffer = BitConverter.GetBytes(nameBuffer.Length);
					stream.Write(buffer, 0, buffer.Length);
					stream.Write(nameBuffer, 0, nameBuffer.Length);

					buffer = BitConverter.GetBytes(clients[i].playerNumber);
					stream.Write(buffer, 0, buffer.Length);
				}

				data = stream.GetBuffer();
			}

			return data;
		}
	}
}
