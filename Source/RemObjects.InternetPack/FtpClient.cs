/*---------------------------------------------------------------------------
  RemObjects Internet Pack for .NET
  (c)opyright RemObjects Software, LLC. 2003-2016. All rights reserved.
---------------------------------------------------------------------------*/

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RemObjects.InternetPack.CommandBased;
using RemObjects.InternetPack.Events;

namespace RemObjects.InternetPack.Ftp
{
	// ftp://ftp.rfc-editor.org/in-notes/rfc959.txt
#if DESIGN
	[System.Drawing.ToolboxBitmap(typeof(RemObjects.InternetPack.Server), "Glyphs.FtpClient.bmp")]
#endif
	public class FtpClient : CommandBasedClient
	{
		#region Private fields
		private IPAddress fDataAddress;
		private Int32 fDataPort;
		private Connection fDataConnection;
		private SimpleServer fDataServer;
		private String fCurrentDirectory;
		#endregion

		public FtpClient()
		{
			this.Passive = false;
			this.AutoRetrieveListing = true;
			this.fCurrentDirectory = String.Empty;
			this.fCurrentDirectoryContents = new FtpListing();
		}

		#region Properties
		public String UserName { get; set; }

		public String Password { get; set; }

		public String Account { get; set; }

		public Boolean Passive { get; set; }

		public Boolean ShowHiddenFiles { get; set; }

        public Uri Url
        {
            get
            {
                UriBuilder ub = new UriBuilder();
                ub.Scheme = "ftp";
                ub.Host = String.IsNullOrEmpty(HostName) ? HostAddress.ToString() : HostName;
                if (Port != 21) ub.Port = Port;
                if (!string.IsNullOrEmpty(UserName)) ub.UserName = UserName;
                if (!string.IsNullOrEmpty(Password)) ub.Password = Password;
                return ub.Uri;
            }
            set
            {
                Port = value.IsDefaultPort ? 21 : value.Port;
                HostName = value.Host;

                // convert to UriBuilder to access username & password
                UriBuilder ub = new UriBuilder(u);
                if (ub.UserName != null) UserName = ub.UserName;
                if (ub.Password != null) Password = ub.Password;

                // ignored: path
            }
        }
        
        public IPAddress ActiveModeExternalAddress { get; set; }

        public class PortRange
        {
            public int Start { get; set; }
            public int End { get; set; }
            public PortRange(int start, int end)
            {
                Start = start;
                End = end;
            }
        }

        public PortRange ActiveModePortRange { get; set; }

        public int ActiveModeCurrentPort { get; set; }

		public Encoding Encoding
		{
			get
			{
				return this.fEncoding ?? (this.fEncoding = Encoding.UTF8);
			}
			set
			{
				this.fEncoding = value;
			}
		}
		private Encoding fEncoding;

		public Boolean AutoRetrieveListing { get; set; }

		public FtpListing CurrentDirectoryContents
		{
			get
			{
				return this.fCurrentDirectoryContents;
			}
		}
		private readonly FtpListing fCurrentDirectoryContents;
		#endregion

		public override void Open()
		{
			base.Open();

			this.CurrentConnection.Encoding = Encoding;

			if (!this.WaitForResponse(220))
			{
				this.Close();
				throw new CmdResponseException("Invalid connection reply", this.LastResponseNo, this.LastResponseText);
			}
		}

		public override void Close()
		{
			base.Close();

			if (this.fDataConnection != null && this.fDataConnection.Connected)
				this.fDataConnection.Close();
		}

		public void Login()
		{
			if (!this.SendAndWaitForResponse("USER " + this.UserName, 331, 230))
				throw new CmdResponseException("Login unsuccessful", this.LastResponseNo, this.LastResponseText);

			switch (this.LastResponseNo)
			{
				case 331:
					if (!this.SendAndWaitForResponse("PASS " + this.Password, 230, 332))
						throw new CmdResponseException("Login unsuccessful", this.LastResponseNo, this.LastResponseText);

					switch (this.LastResponseNo)
					{
						case 232:
							SendAccount();
							break;

						case 230:
							break;
					}
					break;

				case 230:
					break;
			}
		}

		public void SendAccount()
		{
			if (this.Account.Length == 0)
				throw new Exception("Account cannot be blank");

			if (!this.SendAndWaitForResponse("ACCT " + this.Account, 230, 202))
				throw new CmdResponseException("Account command unsuccessful", this.LastResponseNo, this.LastResponseText);
		}

		public void Quit()
		{
			if (!this.SendAndWaitForResponse("QUIT", 221))
				throw new CmdResponseException("Quit unsuccessful", this.LastResponseNo, this.LastResponseText);
		}

		public void ChangeDirectory(String directory)
		{
			if (!this.SendAndWaitForResponse("CWD " + directory, 250))
				throw new CmdResponseException("Error changing directory", this.LastResponseNo, this.LastResponseText);

			if (this.AutoRetrieveListing)
				this.List();
		}

		public void ChangeToParentDirectory()
		{
			if (!this.SendAndWaitForResponse("CDUP", 250))
				throw new CmdResponseException("Error changing directory", this.LastResponseNo, this.LastResponseText);

			if (this.AutoRetrieveListing)
				this.List();
		}

		public String GetCurrentDirectory()
		{
			if (!this.SendAndWaitForResponse("PWD", 257))
				throw new CmdResponseException("Could not retrieve current directory", this.LastResponseNo, this.LastResponseText);

			StringBuilder lResult = new StringBuilder();

			Int32 i = 1;
			while (i < this.LastResponseText.Length)
			{
				if (this.LastResponseText[i] == '"')
				{
					if (i < this.LastResponseText.Length - 1 && this.LastResponseText[i + 1] == '"')
					{
						lResult.Append('"');
						i++; // skip extra doubled quote
					}
					else
					{
						break;
					}
				}
				else
				{
					lResult.Append(this.LastResponseText[i]);
				}
				i++;
			}

			this.fCurrentDirectory = lResult.ToString();

			return this.fCurrentDirectory;
		}

		public void RemoveDirectory(String directory)
		{
			if (!this.SendAndWaitForResponse("RMD " + directory, 250))
				throw new CmdResponseException("Error removing directory", LastResponseNo, LastResponseText);

			if (this.AutoRetrieveListing)
				this.List();
		}

		public void RemoveDirectory(String directory, Boolean recursive)
		{
			if (recursive)
			{
				this.ChangeDirectory(directory);
				try
				{
					this.List(true);
					foreach (FtpListingItem ftpItem in this.CurrentDirectoryContents)
					{
						if (ftpItem.FileName != "..")
						{
							if (ftpItem.Directory)
								this.RemoveDirectory(ftpItem.FileName);
							else
								this.Delete(ftpItem.FileName);
						}
					}
				}
				finally
				{
					this.ChangeToParentDirectory();
				}
			}

			this.RemoveDirectory(directory);
		}

		public void MakeDirectory(String directory)
		{
			if (!this.SendAndWaitForResponse("MKD " + directory, 257))
				throw new CmdResponseException("Error making directory", LastResponseNo, LastResponseText);

			if (this.AutoRetrieveListing)
				this.List();
		}

		public void StartPassiveConnection()
		{
			if (!this.SendAndWaitForResponse("PASV", 227))
				throw new CmdResponseException("Could not set passive mode", this.LastResponseNo, this.LastResponseText);

			Match lMatch = Regex.Match(LastResponseText, @"(?<A1>\d+),(?<A2>\d+),(?<A3>\d+),(?<A4>\d+),(?<P1>\d+),(?<P2>\d+)");
			GroupCollection lGroups = lMatch.Groups;

			this.fDataAddress = IPAddress.Parse(String.Format("{0}.{1}.{2}.{3}", lGroups["A1"].Value, lGroups["A2"].Value, lGroups["A3"].Value, lGroups["A4"].Value));
			this.fDataPort = (Byte.Parse(lGroups["P1"].Value) * 256) + Byte.Parse(lGroups["P2"].Value);

			this.SendLog(TransferDirection.None, "Connecting to {0}:{1}", this.fDataAddress, this.fDataPort);
			this.fDataConnection = this.NewConnection(CurrentConnection.Binding);
			this.fDataConnection.Connect(this.fDataAddress, this.fDataPort);
			this.fDataConnection.Encoding = Encoding;
			this.SendLog(TransferDirection.None, "Connected to {0} port {1}", this.fDataAddress, this.fDataPort);

			this.fDataConnection.OnBytesReceived += this.TriggerOnBytesReceived;
			this.fDataConnection.OnBytesSent += this.InternalOnBytesSent;
		}

		public void StartActiveConnection()
		{
			if (this.fDataConnection != null)
			{
				if (this.fDataConnection.Connected)
					this.fDataConnection.Close();
				this.fDataConnection = null;
			}

			if (this.fDataServer == null)
			{
                if (ActiveModePortRange != null)
                {
                    var p = new PortRangeSimpleServer(ActiveModePortRange.Start, ActiveModePortRange.End, ActiveModeCurrentPort);
                    this.fDataServer = p;
                    this.fDataServer.Binding.Address = ((IPEndPoint)CurrentConnection.LocalEndPoint).Address;
                    this.fDataServer.Open();
                    ActiveModeCurrentPort = p.CurrentPort;
                }
                else
                {
                    this.fDataServer = new SimpleServer();
                    this.fDataServer.Binding.Address = ((IPEndPoint)CurrentConnection.LocalEndPoint).Address;
                    this.fDataServer.Open();
                }
			}

            IPAddress lPortAddress = ActiveModeExternalAddress ?? ((IPEndPoint)this.fDataServer.Binding.ListeningSocket.LocalEndPoint).Address;
            Byte[] lAddress;
#if FULLFRAMEWORK
            lAddress = lPortAddress.GetAddressBytes();
#endif
#if COMPACTFRAMEWORK
			String[] lIPAddressstr = lPortAddress.ToString().Split(new Char[] {'.'});
			lAddress = new Byte[lIPAddressstr.Length];
			for (Int32 i = 0; i < lIPAddressstr.Length; i++)
				lAddress[i] = Byte.Parse(lIPAddressstr[i]);
#endif

            Int32 lPort = ((IPEndPoint)this.fDataServer.Binding.ListeningSocket.LocalEndPoint).Port;
			String lPortCommand = String.Format("PORT {0},{1},{2},{3},{4},{5}", lAddress[0], lAddress[1], lAddress[2], lAddress[3], unchecked((Byte)(lPort >> 8)), unchecked((Byte)lPort));
			if (!SendAndWaitForResponse(lPortCommand, 200))
				throw new CmdResponseException("Error in PORT command", LastResponseNo, LastResponseText);
		}

		private void RetrieveDataConnection()
		{
			if (!this.Passive)
			{
				if (this.fDataServer == null)
					throw new Exception("DataServer is not assigned");

				Connection lConnection = this.fDataServer.WaitForConnection();
				this.fDataServer.Close();
				this.fDataServer = null;
				this.fDataConnection = lConnection;
				this.fDataConnection.Encoding = this.Encoding;
			}
		}

		public String List()
		{
			return this.List(false);
		}

		public String List(Boolean showHiddenFiles)
		{
			String lResult;

			this.SetType("A");
			this.CheckDataConnection();

			if (!this.SendAndWaitForResponse(this.ShowHiddenFiles || showHiddenFiles ? "LIST -a" : "LIST", 125, 150))
				throw new CmdResponseException("Could not start LIST command", LastResponseNo, LastResponseText);

			this.RetrieveDataConnection();
			Byte[] lResponse = this.fDataConnection.ReceiveAllRemaining();
			this.fDataConnection.Close();

			if (lResponse != null)
			{
				try
				{
					lResult = this.fDataConnection.Encoding.GetString(lResponse, 0, lResponse.Length);
					this.CurrentDirectoryContents.Parse(lResult, this.fCurrentDirectory != "/");
				}
				catch
				{
					// we don't want any exception here
					lResult = null;
					this.fCurrentDirectoryContents.Clear();
				}
			}
			else
			{
				lResult = null;
				this.CurrentDirectoryContents.Clear();
			}
			this.WaitForResponse(226);

			this.TriggerOnNewListing();

			return lResult;
		}

		public void Delete(String filename)
		{
			if (!this.SendAndWaitForResponse("DELE " + filename, 250))
				throw new CmdResponseException("Error deleting file", this.LastResponseNo, this.LastResponseText);

			if (this.AutoRetrieveListing)
				this.List();
		}

		public void SetType(String type)
		{
			if (!SendAndWaitForResponse("TYPE " + type, 200))
				throw new CmdResponseException("Error sending TYPE command", this.LastResponseNo, this.LastResponseText);
		}

		private void CheckDataConnection()
		{
			if (this.fDataConnection == null || (this.fDataConnection != null && !this.fDataConnection.Connected))
			{
				if (this.Passive)
					this.StartPassiveConnection();
				else
					this.StartActiveConnection();
			}
		}

		public void Retrieve(FtpListingItem item, Stream stream)
		{
			this.Retrieve(item.FileName, item.Size, stream);
		}

		public void Retrieve(String filename, Int64 size, Stream stream)
		{
			this.SetType("I");
			this.CheckDataConnection();

			if (!this.SendAndWaitForResponse("RETR " + filename, 150, 125))
				throw new CmdResponseException("Error retrieving file", this.LastResponseNo, this.LastResponseText);

			this.TriggerOnTransferStart(this, new TransferStartEventArgs(TransferDirection.Receive, size));

			this.RetrieveDataConnection();
			this.fDataConnection.ReceiveToStream(stream, size);
			this.fDataConnection.Close();

			this.WaitForResponse(226);
		}

		public void Store(String filename, Stream stream)
		{
			this.SetType("I");
			this.CheckDataConnection();

			if (!this.SendAndWaitForResponse("STOR " + filename, 150, 125))
				throw new CmdResponseException("Error storing file", LastResponseNo, LastResponseText);

			this.TriggerOnTransferStart(this, new TransferStartEventArgs(TransferDirection.Receive, stream.Length));

			this.RetrieveDataConnection();
			this.fDataConnection.SendFromStream(stream);
			this.fDataConnection.Close();

			this.WaitForResponse(226);

			if (this.AutoRetrieveListing)
				this.List();
		}

		public void Rename(String from, String to)
		{
			if (!this.SendAndWaitForResponse("RNFR " + from, 350))
				throw new CmdResponseException("Error renaming file", this.LastResponseNo, this.LastResponseText);

			if (!this.SendAndWaitForResponse("RNTO " + to, 250))
				throw new CmdResponseException("Error renaming file", this.LastResponseNo, this.LastResponseText);

			if (this.AutoRetrieveListing)
				this.List();
		}

        public string ModificationTime(string aFilename)
        {
            // most cases do YYYYMMDDhhmmss but I'm not sure if that's a standard or not.
            if (SendAndWaitForResponse("MDTM " + aFilename, 213))
                return LastResponseText;
            else
                throw new CmdResponseException("Error getting modification time of file", LastResponseNo, LastResponseText);
        }

        public long Size(string aFilename)
        {
            long size;
            if (SendAndWaitForResponse("SIZE " + aFilename, 213) && long.TryParse(LastResponseText, out size))
                return size;
            else
                throw new CmdResponseException("Error getting size of file", LastResponseNo, LastResponseText);
        }

        public string Stat(string aFilename = null)
        {
            // This can be random weird text
            // https://www.w3.org/Protocols/rfc959/4_FileTransfer.html
            if (SendAndWaitForResponse(String.IsNullOrEmpty(aFilename) ? "STAT" : "STAT " + aFilename, 200, 213))
                return LastResponseText;
            else
                throw new CmdResponseException("Error running stat on file", LastResponseNo, LastResponseText);
        }

        public string Raw(string aCommand)
        {
            SendAndWaitForResponse(aCommand);
            return LastResponseNo + " " + LastResponseText;
        }

		#region Events
		public event TransferStartEventHandler OnTransferStart;

		protected virtual void TriggerOnTransferStart(Object sender, TransferStartEventArgs e)
		{
			if (this.OnTransferStart != null)
				this.OnTransferStart(sender, e);
		}

		public event TransferProgressEventHandler OnTransferProgress;

		protected virtual void TriggerOnBytesReceived(Object sender, EventArgs e)
		{
			if (this.OnTransferProgress != null)
				this.OnTransferProgress(sender, new TransferProgressEventArgs(TransferDirection.Receive, ((Connection)sender).BytesReceived));
		}

		protected virtual void InternalOnBytesSent(Object sender, EventArgs e)
		{
			if (this.OnTransferProgress != null)
				this.OnTransferProgress(sender, new TransferProgressEventArgs(TransferDirection.Send, ((Connection)sender).BytesSent));
		}

		public event EventHandler OnNewListing;

		protected virtual void TriggerOnNewListing()
		{
			if (this.OnNewListing != null)
				this.OnNewListing(this, new EventArgs());
		}
		#endregion
       
    }
}