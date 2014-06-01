using System;
using System.Diagnostics;
using System.Threading;

namespace Mono.WebServer.Http
{
	class MainClass
	{

		public static void Main (string[] args)
		{
			Console.WriteLine ("Http WebServer ProcessId={0}",Process.GetCurrentProcess().Id);

			Server server = new Server ();

			ThreadPool.SetMinThreads (40, 4);
			server.Start ();

			Console.ReadLine ();

			server.Shutdown ();
		}
	}
}
