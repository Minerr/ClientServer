using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameServer;

namespace Server
{
	class Program
	{
		static void Main(string[] args)
		{
			GameServer.GameServer server = new GameServer.GameServer();
			server.Run();

			Console.WriteLine("Press key to terminate server... ");
			Console.ReadKey();
		}
	}
}
