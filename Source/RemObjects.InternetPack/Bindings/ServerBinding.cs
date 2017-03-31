/*---------------------------------------------------------------------------
  RemObjects Internet Pack for .NET
  (c)opyright RemObjects Software, LLC. 2003-2016. All rights reserved.
---------------------------------------------------------------------------*/

using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RemObjects.InternetPack
{
	public class ServerBinding : Binding
	{
		private const Int32 MAX_WAIT_CONNECTIONS = 50;

		private Thread[] fListenThreads;

		public ServerBinding()
		{
			this.ListenerThreadCount = 1;
			this.MaxWaitConnections = MAX_WAIT_CONNECTIONS;
			this.EnableNagle = false;
			this.ReuseAddress = true;
		}

		#region Properties
		[Browsable(false)]
		public Socket ListeningSocket
		{
			get
			{
				return this.fListeningSocket;
			}
		}
		private Socket fListeningSocket;

		[Browsable(false)]
		public Boolean ReuseAddress { get; set; }

		[Browsable(false)]
		public IPEndPoint EndPoint
		{
			get
			{
				return this.fEndPoint;
			}
		}
		private IPEndPoint fEndPoint;

		[DefaultValue(MAX_WAIT_CONNECTIONS)]
		public Int32 MaxWaitConnections { get; set; }

		[DefaultValue(1)]
		public Int32 ListenerThreadCount { get; set; }

		[DefaultValue(false)]
		public Boolean EnableNagle { get; set; }
		#endregion

		public virtual void Bind(IListener listener)
		{
			this.fEndPoint = new IPEndPoint(this.Address, this.Port);
			this.fListeningSocket = new Socket(this.AddressFamily, this.SocketType, this.Protocol);
			if (!this.EnableNagle)
				this.fListeningSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, 1);
#if FULLFRAMEWORK
			if (this.ReuseAddress)
				this.fListeningSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
#endif
			this.fListeningSocket.Bind(this.fEndPoint);
			this.fListeningSocket.Listen(this.MaxWaitConnections);
			this.fListenThreads = new Thread[ListenerThreadCount];

			for (Int32 i = 0; i < this.fListenThreads.Length; i++)
			{
				listener.ListeningSocket = this.fListeningSocket;
				fListenThreads[i] = new Thread(listener.Listen);
#if FULLFRAMEWORK
				fListenThreads[i].Name = String.Format("Internet Pack Listener {0} for {1}", i, this.EndPoint);
#endif
				fListenThreads[i].Start();
			}
		}

		public void Unbind()
		{
			this.Unbind(true);
		}

		public virtual void Unbind(Boolean block)
		{
			if (this.fListeningSocket == null)
				return;

			this.fListeningSocket.Close();
#if FULLFRAMEWORK
			if (block && this.fListenThreads != null)
				for (Int32 i = 0; i < this.fListenThreads.Length; i++)
					this.fListenThreads[i].Join();
#endif
		}

		public virtual void BindUnthreaded()
		{
			this.fEndPoint = new IPEndPoint(this.Address, this.Port);
			this.fListeningSocket = new Socket(this.AddressFamily, this.SocketType, this.Protocol);
			if (!this.EnableNagle)
				this.fListeningSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, 1);
#if FULLFRAMEWORK
			if (this.ReuseAddress)
				this.fListeningSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
#endif
			this.fListeningSocket.Bind(this.fEndPoint);
		}

		public virtual Connection Accept()
		{
			return new Connection(this.fListeningSocket.Accept());
		}
	}

    public class PortRangeServerBinding : ServerBinding
    {
        public int PortRangeStart { get; set; }
        public int PortRangeEnd { get; set; }

        public PortRangeServerBinding(int firstPort, int lastPort)
        {
            PortRangeStart = firstPort;
            PortRangeEnd = lastPort;
        }

        public override void Bind(IListener listener)
        {
            throw new NotSupportedException("Threaded binding not supported with port range");
        }

        public override void BindUnthreaded()
        {
            int numPorts = PortRangeEnd - PortRangeStart + 1;

            // select first port if no port, or current port is out of the valid range
            int currentPort = (this.Port == 0)
                ? PortRangeStart
                : Math.Min(PortRangeEnd, Math.Max(PortRangeStart, PortRangeStart + ((this.Port + 1 - PortRangeStart) % numPorts)));

            // try the entire range, starting at the current port (if given)
            for (int i = 0; numPorts > i; i++)
            {
                try
                {
                    //this.ReuseAddress = false; // = true -> in theory this should avoid having to wait for TIME_WAIT sockets to finish, but it doesn't ?
                    //this.ExclusiveAddressUse = true; // avoid TIME_WAIT ? -> no
                    this.Port = ((currentPort - PortRangeStart + i) % numPorts) + PortRangeStart;
                    base.BindUnthreaded();
                    return;
                }
                catch (SocketException)
                {
                    //// possible error codes to limit this to:
                    //switch (se.ErrorCode)
                    //{
                    //    case 10013: // WSAEACCES
                    //    case 10048: // WSAEADDRINUSE
                    //        // TODO
                    //        break;
                    //}
                }
            }
            throw new SocketException(10048); // fake it           
        }
    }
}