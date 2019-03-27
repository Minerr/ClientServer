using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PacketSerialization
{
	internal enum ConnectionState
	{
		Disconnected = 0,
		Connecting = 1,
		Connected = 2
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

		public PlayerInfo(string name)
		{
			this.name = name;
			position = new Position(0, 0, 0);
		}
	}

	internal class NetworkClient
	{
		private ConnectionState playerState;
		private PlayerInfo[] players;
		private ushort currentPlayer;
		private TimeSpan serverTime;

		private NetworkServer server;

		public NetworkClient(string playerName)
		{
			playerState = ConnectionState.Disconnected;
			players = new PlayerInfo[3];
			currentPlayer = 0;

			players[0] = new PlayerInfo("Available");
			players[1] = new PlayerInfo("Available");
			players[2] = new PlayerInfo("Available");
			players[currentPlayer] = new PlayerInfo(playerName);
			players[currentPlayer].position = new Position(0.23f, 10.4f, 1f);

			server = new NetworkServer();
		}

		public void RunTest()
		{
			playerState = ConnectionState.Connecting;

			byte[] clientData = SerializePlayerState();
			byte[] serverData = server.GetServerData(clientData);
			DeserializeServerData(serverData);


			Console.WriteLine($"Latest server time: {serverTime}");

			for(int i = 0; i < 3; i++)
			{
				Console.WriteLine("");
				PlayerInfo player = players[i];
				Console.WriteLine($"Player{i + 1}: {player.name}");
				Console.WriteLine($"Position: {player.position}");
			}
		}

		private byte[] SerializePlayerState()
		{
			byte[] data, buffer;
			using(MemoryStream stream = new MemoryStream())
			{
				PlayerInfo player = players[currentPlayer];
				Position pos = player.position;

				buffer = BitConverter.GetBytes((ushort) playerState);
				stream.Write(buffer, 0, buffer.Length);

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

				data = stream.GetBuffer();
			}

			return data;
		}

		private void DeserializeServerData(byte[] data)
		{
			PlayerInfo player;
			string name;
			float x, y, z;
			int playerNameLength = 0;
			int nextIndex = 0;

			TimeSpan time = new TimeSpan(BitConverter.ToInt64(data, nextIndex));
			if(serverTime != null && time < serverTime)
			{
				return; // If this data is old than the last received data, discard this.
			}
			serverTime = time;
			nextIndex += 8;

			playerState = (ConnectionState) BitConverter.ToUInt16(data, nextIndex);
			nextIndex += 2;

			// Set player information
			for(int i = 0; i < 3; i++)
			{
				player = players[i];
				playerNameLength = BitConverter.ToInt32(data, nextIndex);
				nextIndex += 4;

				name = Encoding.ASCII.GetString(data, nextIndex, playerNameLength);
				nextIndex += playerNameLength;

				x = BitConverter.ToSingle(data, nextIndex);
				nextIndex += 4;

				y = BitConverter.ToSingle(data, nextIndex);
				nextIndex += 4;

				z = BitConverter.ToSingle(data, nextIndex);
				nextIndex += 4;

				player.name = name;
				player.position = new Position(x, y, z);
			}
		}
	}
}
