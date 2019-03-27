using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PacketSerialization
{
	class NetworkServer
	{
		private PlayerInfo[] players;
		private TimeSpan serverTime;

		public NetworkServer()
		{
			players = new PlayerInfo[3];
			players[0] = new PlayerInfo("Available");
			players[1] = new PlayerInfo("Available");
			players[2] = new PlayerInfo("Available");

			serverTime = DateTime.Now.TimeOfDay;
		}

		public byte[] GetServerData(byte[] clientData)
		{
			serverTime = DateTime.Now.TimeOfDay;
			byte[] data, buffer;


		#region deserialize clientData
			PlayerInfo player;
			string name;
			float x, y, z;
			int playerNameLength = 0;
			int nextIndex = 0;

			ConnectionState playerState = (ConnectionState) BitConverter.ToUInt16(clientData, nextIndex);
			nextIndex += 2;

			// Set player information
			player = players[0];
			playerNameLength = BitConverter.ToInt32(clientData, nextIndex);
			nextIndex += 4;

			name = Encoding.ASCII.GetString(clientData, nextIndex, playerNameLength);
			nextIndex += playerNameLength;

			x = BitConverter.ToSingle(clientData, nextIndex);
			nextIndex += 4;

			y = BitConverter.ToSingle(clientData, nextIndex);
			nextIndex += 4;

			z = BitConverter.ToSingle(clientData, nextIndex);
			nextIndex += 4;

			player.name = name;
			player.position = new Position(x, y, z);
		#endregion

		
			using(MemoryStream stream = new MemoryStream())
			{
				buffer = BitConverter.GetBytes(serverTime.Ticks);
				stream.Write(buffer, 0, buffer.Length);

				buffer = BitConverter.GetBytes((ushort) ConnectionState.Connected);
				stream.Write(buffer, 0, buffer.Length);

				for(int i = 0; i < 3; i++)
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
