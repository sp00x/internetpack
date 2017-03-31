/*---------------------------------------------------------------------------
  RemObjects Internet Pack for .NET
  (c)opyright RemObjects Software, LLC. 2003-2016. All rights reserved.
---------------------------------------------------------------------------*/

using System.Net;

namespace RemObjects.InternetPack
{
	public class SimpleServer
	{
		public SimpleServer()
		{
			this.fBinding = new ServerBinding();
			this.fBinding.Address = IPAddress.Any;
		}

		#region Properties
		public ServerBinding Binding
		{
			get
			{
				return this.fBinding;
			}
		}
		protected ServerBinding fBinding;
		#endregion

        public virtual void Open()
		{
			this.Binding.BindUnthreaded();
			this.Binding.ListeningSocket.Listen(this.Binding.MaxWaitConnections);
		}

		public virtual void Close()
		{
			this.Binding.Unbind();
		}

		public Connection WaitForConnection()
		{
			return this.Binding.Accept();
		}
	}

    public class PortRangeSimpleServer : SimpleServer
    {
        public int PortRangeStart { get; protected set; }
        public int PortRangeEnd { get; protected set; }
        public int CurrentPort { get; protected set; }

        public PortRangeSimpleServer(int firstPort, int lastPort, int currentPort = 0)
        {
            this.PortRangeStart = firstPort;
            this.PortRangeStart = lastPort;
            this.CurrentPort = currentPort;
            this.fBinding = new PortRangeServerBinding(firstPort, lastPort);
            this.fBinding.Address = IPAddress.Any;
        }

        public override void Open()
        {
            this.Binding.Port = CurrentPort;
            this.Binding.BindUnthreaded();
            CurrentPort = this.Binding.Port;
            this.Binding.ListeningSocket.Listen(this.Binding.MaxWaitConnections);
        }
    }
}