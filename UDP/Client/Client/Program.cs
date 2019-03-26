using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
	class Program
	{
		static void Main(string[] args)
		{
			Client client = new Client();
			client.Run();

			string input = "";

			while(input != "quit")
			{
				input = Console.ReadLine();
				client.SendMessage(input);
			}
		}
	}
}
