using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Server
{
	internal enum ConnectionState
	{
		Disconnected = 0,
		Connecting = 1,
		Verification = 2,
		Connected = 3
	}

	public enum Request
	{
		None = 0,
		Disconnect = 1,
		JoinGame = 2,
		JoinSpectators = 3,
		MovePosition = 4
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

	internal class ClientInfo
	{
		public IPEndPoint EndPoint { get; }
		public ConnectionState State { get; set; }
		public string Name { get; set; }
		public ushort playerNumber { get; set; }

		public ClientInfo(string name, IPEndPoint endPoint, ConnectionState state)
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

		private List<ClientInfo> clients;
		private Position[] playerPositions;
		private List<IPEndPoint> players;
		private TimeSpan serverTime;

		public GameServer()
		{
			clients = new List<ClientInfo>();
			players = new List<IPEndPoint>();

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

			Timer timer = new Timer(16);
			timer.Elapsed += new ElapsedEventHandler(OnTriggerTimer);
			timer.Enabled = true;

			bool isRunning = true;
			while(isRunning)
			{
				// Game loop

			}

			timer.Enabled = false;
		}

		private void OnTriggerTimer(object sender, ElapsedEventArgs e)
		{
			BroadcastWorldState();
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

				// Client verification
				ClientInfo client = GetClient(endPoint);

				if (client == null)
				{
					AddClient(endPoint, name);
					SendVerificationPacket(endPoint);
					Console.WriteLine($"Sending verification to: {endPoint}.");
				}
				else if(packetState == ConnectionState.Verification && client.State == ConnectionState.Connecting)
				{
					Console.WriteLine($"Received verification from: {endPoint}, client is now connected.");
					client.State = ConnectionState.Connected;
				}
				else if(packetState == ConnectionState.Connected && client.State == ConnectionState.Connected)
				{
					Request request = (Request)BitConverter.ToUInt16(data, nextIndex);
					nextIndex += 2;

					if(request == Request.None)
					{
						
					}
					else if(request == Request.JoinGame)
					{
						JoinGame(client);
					}
					else if(request == Request.JoinSpectators)
					{
						JoinSpectators(client);
					}
					else if(request == Request.Disconnect)
					{
						RemoveClient(client);
					}
					else if(request == Request.MovePosition)
					{
						if(client.playerNumber > 0)
						{
							bool moveUp = BitConverter.ToBoolean(data, nextIndex);
							nextIndex++;

							bool moveDown = BitConverter.ToBoolean(data, nextIndex);
							nextIndex++;

							MovePlayerPosition(client.playerNumber, moveUp, moveDown);
						}
					}
				}
			}

			socket.BeginReceive(ReceiveCallback, null);
		}

		private bool VerifyPacket(IPEndPoint endPoint, ConnectionState packetState)
		{
			ClientInfo client = GetClient(endPoint);

			if(client == null && packetState == ConnectionState.Connecting)
			{
				return true;
			}

			if(client?.State == packetState)
			{
				return true;
			}

			return false;
		}

		private void MovePlayerPosition(int playerNumber, bool moveUp, bool moveDown)
		{
			Position currentPos = playerPositions[playerNumber];

			int moveDirection = moveUp && moveDown ? 0 : moveUp ? 1 : -1; // 0 if both keys are pressed, 1 if only up, -1 if only down.

			currentPos.y += moveDirection * 0.01f;
		}

		private void RemoveClient(ClientInfo client)
		{
			Console.WriteLine($"Player: {client.Name}({client.EndPoint}) was removed from the server.");
			players.Remove(client.EndPoint);

			// TODO: Remove client from list after awhile.
			client.State = ConnectionState.Disconnected;
		}

		private void JoinSpectators(ClientInfo client)
		{
			players.Remove(client.EndPoint);
			client.playerNumber = 0;
		}

		private void JoinGame(ClientInfo client)
		{
			ushort playerNumber = 0;

			if(playerNumber < 3)
			{
				players.Add(client.EndPoint);
				playerNumber = (ushort)players.Count;
			}

			client.playerNumber = playerNumber;

			SendJoinGamePacket(client);
		}

		private void AddClient(IPEndPoint endPoint, string name)
		{
			// If it's a new client, add to the client list
			if (GetClient(endPoint) == null)
			{
				Console.WriteLine($"Player: {name}({endPoint}) is connecting to the server.");
				clients.Add(new ClientInfo(name, endPoint, ConnectionState.Connecting));
			}
		}

		public ClientInfo GetClient(IPEndPoint endPoint)
		{
			return clients.Find(p => p.EndPoint.Equals(endPoint));
		}

		// ---- Send packets---- //
		private void SendDataToClient(byte[] data, IPEndPoint endPoint)
		{
			for (int i = 0; i < 10; i++)
			{
				socket.Send(data, data.Length, endPoint);
			}
		}


		private void BroadcastWorldState()
		{
			try
			{
				byte[] data = GenerateWorldPacket();

				foreach(ClientInfo player in clients)
				{
					if(player.State == ConnectionState.Connected)
					{
						socket.Send(data, data.Length, player.EndPoint);
					}
				}
			}
			catch(Exception e)
			{
				Console.WriteLine($"Error! {e}");
			}
		}


		// ---- Generated packets ---- //

		private void SendJoinGamePacket(ClientInfo client)
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

				buffer = BitConverter.GetBytes(client.playerNumber);
				stream.Write(buffer, 0, buffer.Length);

				data = stream.GetBuffer();
			}

			SendDataToClient(data, client.EndPoint);
		}

		private void SendVerificationPacket(IPEndPoint endPoint)
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

			SendDataToClient(data, endPoint);
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

				List<ClientInfo> connectedClients = clients.Where(c => c.State == ConnectionState.Connected).ToList();
				int connectedClientCount = connectedClients.Count();

				buffer = BitConverter.GetBytes(connectedClientCount);
				stream.Write(buffer, 0, buffer.Length);

				for(int i = 0; i < connectedClientCount; i++)
				{
					byte[] nameBuffer = Encoding.ASCII.GetBytes(connectedClients[i].Name);
					buffer = BitConverter.GetBytes(nameBuffer.Length);
					stream.Write(buffer, 0, buffer.Length);
					stream.Write(nameBuffer, 0, nameBuffer.Length);

					buffer = BitConverter.GetBytes(connectedClients[i].playerNumber);
					stream.Write(buffer, 0, buffer.Length);
				}

				data = stream.GetBuffer();
			}

			return data;
		}
	}
}
