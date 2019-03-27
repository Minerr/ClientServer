using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PacketSerialization
{
	class Program
	{
		static void Main(string[] args)
		{
			NetworkClient client = new NetworkClient("CoolGuy69");
			client.RunTest();

			Console.ReadKey();
		}
	}
}
