using System;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Text;

namespace Mono.WebServer.Http
{

	public class SendStateObject
	{
		//private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		public static int refCount = 0;
		public static volatile int lastTimeInfo = 0;

		public bool disconnectAfterSend;
		public byte[] buffer;
		public int offset;
		public Socket workSocket;
	}

	public class StateObject
	{
		//TODO: IDisposable
		//	public int offset;
		public byte[] buffer = new byte[16384];
		//read length prefix in this buffer
		public const int headerLength = 8;
		public byte[] header = new byte[headerLength];
		public int arrayOffset;

		public Socket workSocket;
	}

	public class NetworkConnector
	{
		private AsyncCallback asyncRecieveCallback;
		private AsyncCallback asyncSendCallback;
		private Socket client;
		private int sendProcessing;
		private SendStateObject sendState=new SendStateObject();
		StateObject state = new StateObject();
		IAsyncResult recvRes;

		private Queue<string> sendQueue=new Queue<string>();

		bool isDisconnected;

		public NetworkConnector (Socket client)
		{
			asyncRecieveCallback = new AsyncCallback(ReceiveCallback);
			asyncSendCallback = new AsyncCallback(SendCallback);
			this.client = client;
		}

		public void Receive()
		{
			try
			{
				state.workSocket = client;

				// Begin receiving the data from the remote device.
				recvRes=client.BeginReceive(state.buffer, 0, state.buffer.Length, 0, asyncRecieveCallback, state);
				/*int readBytes=client.Receive(state.buffer);
				if (readBytes>0)
				{
					Process();
				}
				else
				{
					Console.WriteLine("error in receiving data");
				}*/
			}
			catch (Exception)
			{
//				log.Error("Receive",ex);
				throw;
			}

		}

		private void ReceiveCallback(IAsyncResult ar)
		{
			int bytesRead = 0;
			StateObject state = (StateObject)ar.AsyncState;
	
			if (!state.workSocket.Connected) {
				//Console.WriteLine ("rcb not connected {0}", state.workSocket.Handle);
				return;
			}

			try
			{
				Socket client = state.workSocket;
				//	state.offset = 0;
				SocketError socketError;

				bytesRead = client.EndReceive(ar, out socketError);

				if (socketError != SocketError.Success)
				{
					OnDisconnected();
					return;
				}

				if (bytesRead <= 0 && !client.Connected)
				{
					//Client disconnected
					OnDisconnected();
					return;
				}


				if (bytesRead > 0)
				{
					//send reply
					Process();
				}

				//client.BeginReceive(state.buffer, 0, state.buffer.Length, 0, asyncRecieveCallback, state);

			}
			catch (Exception ex) {
				Console.WriteLine ("{0}", ex);
			}
		}

//		private void ReceiveCallback(IAsyncResult ar)
//		{
//			Console.WriteLine ("Second receive {0}", client.Handle);
//		}

		private void StartSendPackets()
		{
			//byte[] buffer;
			//SendStateObject sendState = new SendStateObject();


			if (isDisconnected)
			{
				Interlocked.Exchange(ref sendProcessing,0);
				return;
			}

			bool hasPacket=false;
			string packet=null;

			try
			{
				lock (sendQueue)
				{
					if (sendQueue.Count>0)
					{
						packet = sendQueue.Dequeue();
						hasPacket=true;
					}
				}

				if (hasPacket)
				{
					if (packet==String.Empty)
					{
						Disconnect();
						return;
					}
					sendState.buffer = Encoding.Default.GetBytes(packet);
					sendState.offset = 0;
					sendState.workSocket = this.client;
				}
			}
			catch (Exception)
			{
				Interlocked.Exchange(ref sendProcessing,0);
//				log.Error("StartSendPacket", ex);
				throw;
			}

			try
			{
				if (!isDisconnected && hasPacket)
				{
					//lock (history)
					//	history.Add(String.Format("<--Id={0} Type={1} h={2} a={3}",packet.RequestId,packet.Type,client.Handle,((IPEndPoint)client.RemoteEndPoint).Port));
					client.BeginSend(sendState.buffer, sendState.offset, sendState.buffer.Length, SocketFlags.None, asyncSendCallback, sendState);
				}
				else
				{
					Interlocked.Exchange(ref sendProcessing,0);
				}
			}
			catch (SocketException ex)
			{
//				log.WarnFormat("Socket error while sending data: {0}", ex.Message);
				Console.WriteLine ("StartSendPackets: {0}", ex);
				sendState.buffer = null;
				sendState.workSocket = null;
				OnDisconnected();
				Interlocked.Exchange(ref sendProcessing,0);
			}
			catch (ObjectDisposedException)
			{
//				log.WarnFormat("Socket error while sending data: {0}", ex.Message);
				sendState.buffer = null;
				sendState.workSocket = null;
				OnDisconnected();
				Interlocked.Exchange(ref sendProcessing,0);
			}
			catch (Exception)
			{
//				log.Error("StartSendPacket 2", ex);
				sendState.buffer = null;
				sendState.workSocket = null;
				Interlocked.Exchange(ref sendProcessing,0);
				throw;
			}
		}

		private void SendCallback(IAsyncResult ar)
		{
			SendStateObject sendState = (SendStateObject)ar.AsyncState;

			try
			{
				//Socket client = (Socket)ar.AsyncState;
				Socket client = sendState.workSocket;

				int bytesSent=client.EndSend(ar);

				if (bytesSent<sendState.buffer.Length-sendState.offset)
				{
					sendState.offset+=bytesSent;
					client.BeginSend(sendState.buffer, sendState.offset, sendState.buffer.Length-sendState.offset, SocketFlags.None, asyncSendCallback, sendState);
					return;
				}

			}
			catch (SocketException ex)
			{
//				log.DebugFormat("SendCallback. Socket error while sending data: {0}", ex.Message);
				Console.WriteLine ("SendCallback: {0}", ex);
				OnDisconnected();
				//			((AsyncResult)ar).EndInvoke(ar);
				Interlocked.Exchange(ref sendProcessing,0);
				return;
			}
			catch (ObjectDisposedException)
			{
//				log.Debug("Socket has already been disposed");
				OnDisconnected();
				//			((AsyncResult)ar).EndInvoke(ar);
				Interlocked.Exchange(ref sendProcessing,0);
				return;
				//??? should we return or try to process other messages in queue.
			}
			catch (Exception)
			{
//				log.Error("Unexpected exception in SendCallback", ex);
				//			((AsyncResult)ar).EndInvoke(ar);
				Interlocked.Exchange(ref sendProcessing,0);
				throw;
			}
			finally
			{
				//set sendState properties to null
				//workaround for fixing huge memory leak with sendstate
				if (sendState != null)
				{
					sendState.buffer = null;
					sendState.workSocket = null;
					sendState = null;
				}
			}

			bool continueSend = false; 

			lock (sendQueue)
			{
				if (sendQueue.Count > 0) {
					//get next element if we have
					continueSend = true;
				} else {
					Interlocked.Exchange(ref sendProcessing,0);
				}
			}

			try
			{
				if (continueSend)
				{
					StartSendPackets();
				}

//				log.Debug("Data have been sent");
			}
			finally
			{
				//		((AsyncResult)ar).EndInvoke(ar);

			}
		}

		public void Process()
		{
			ThreadPool.QueueUserWorkItem ((state) => {
				SendRecord (TestResponse.Header);
				SendRecord (TestResponse.Response);
				SendRecord (String.Empty);
			});
		}

		public void SendRecord (string record)
		{
			lock (sendQueue)
			{
				sendQueue.Enqueue(record);
				//toSend = sendQueue.Count >= nStartSend;
			}

			//if we already have no other processes, then start to send  
			if (Interlocked.CompareExchange(ref sendProcessing, 1, 0) == 0)
			{
				StartSendPackets();
			}

		}

		public void Disconnect()
		{
			//if (client.Connected)
			{
				try {
					client.Shutdown(SocketShutdown.Both);
				} catch (Exception ex) {
					Console.WriteLine ("Exception in Shutdown! {0}",ex);
					throw;
				}
				try	{
					//mono does not handle "reuseSocket" parameter
					//so it does not matter "true" or "false"
					//we pass to Disconnect
					client.Disconnect(false);
				} catch (Exception ex) {
					Console.WriteLine ("Exception in disconnect! {0}", ex);
					throw;
				}

				//comment this line to get an exception in Disconnect
//				client.Close ();

			}
			OnDisconnected ();
		}



		#region Events
		public event EventHandler Connected;
		public event EventHandler Disconnected;
		#endregion

		protected void OnConnected()
		{
			isDisconnected=false;
			EventHandler Connected = this.Connected;

			if (Connected != null)
			{
				Connected(this, new EventArgs());
			}
		}

		protected void OnDisconnected()
		{
			if (!isDisconnected)
			{
				isDisconnected = true;
				EventHandler Disconnected = this.Disconnected;

				if (Disconnected != null)
				{
					Disconnected(this, new EventArgs());
				}
			}
		}

		#region IDisposable
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			//clear managed resources
			if (disposing)
			{
				//this line is commented
//				client.Close();
			}
		}

		~NetworkConnector()
		{
			Dispose(false);
		}
		#endregion


	}
}

