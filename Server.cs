using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Mono.WebServer.Http;
using System.Collections.Generic;

namespace Mono.WebServer.Http
{
	[Serializable]
	public class Server  : MarshalByRefObject
	{
		int port=9001;
		private volatile bool shutdown=false;
		private ManualResetEvent allDone=new ManualResetEvent(false);
		private AsyncCallback accept;
		List<NetworkConnector> connectors=new List<NetworkConnector>();

		public Server ()
		{
			accept = new AsyncCallback (acceptCallback);
		}

		public void Start()
		{
			IPEndPoint localEP = new IPEndPoint(IPAddress.Any,port);

			//log.InfoFormat("Local address and port : {0}",localEP);

			Socket listener = new Socket(localEP.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			listener.NoDelay = true;

			try
			{
				listener.Bind(localEP);
				listener.Listen(500);

				while (!shutdown)
				{
					allDone.Reset();

					//log.Info("Awaiting connection...");

					listener.BeginAccept(accept, listener);

					allDone.WaitOne();

				}
			}
			catch (Exception e) 
			{
				//log.Error(e);
				Console.WriteLine (e);
			}

		}

		public void Shutdown()
		{
			shutdown = true;
			allDone.Set();

			//flush all changes
		}


		public void acceptCallback(IAsyncResult ar)
		{

			//log.Info("Accepting connection");

			Socket listener = (Socket)ar.AsyncState;
			Socket client = null; 
			try 
			{
				client = listener.EndAccept(ar);
			} catch (Exception ex)
			{
				Console.WriteLine ("Exception in accept: {0}", ex);
				throw;
			}
			client.NoDelay = true;
			allDone.Set();

			// Additional code to read data goes here.
			NetworkConnector connector = new NetworkConnector(client);
			connector.Disconnected += OnDisconnect;

			//this line for save reference to connector in .NET
			//and prevent to collecting it by GC
			//uncomment it to produce socket leaks 
			//connectors.Add (connector);

			connector.Receive();
		}

		protected void OnDisconnect(object sender, EventArgs args)
		{
			NetworkConnector connector = sender as NetworkConnector;

			//connector.Tag=null;
			connector.Dispose();
		}

	}
}

