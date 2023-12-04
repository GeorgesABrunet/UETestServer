using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Core.Security;
using Core.Logging;

namespace Core.Networking.Sockets
{
    /// <summary>
    /// Socket base class, used for both clients and servers
    /// </summary>
    public class SocketBase
    {
        #region Protected Members

        // endpoint for binding (if a server) or connecting (if a client)
        protected IPEndPoint m_Endpoint;

        // the socket object
        protected Socket m_Socket;

        // shutdown flag, used for breaking out of threads or halting processing
        protected bool m_Shutdown = false;

        // flag to use message headers when sending/receiving
        // we use this to know the expected size of a message
        // so we can read the entire package before dispatching received events
        protected bool m_UseHeaders = false;

        // key to use when encrypting and decrypting data sent through the socket
        // if null, no encryption will be used
        protected string m_EncryptionKey = null;

        // delegates for dispatching events
        public delegate void ConnectionHandler(object sender, RemoteConnection connection);
        public delegate void DataReceivedHandler(object sender, Package packet);

        // events
        public event ConnectionHandler OnDisconnect;
        public event DataReceivedHandler OnDataReceived;

        #endregion

        #region Protected Methods

        /// <summary>
        /// Initialize connection details
        /// </summary>
        /// <param name="ip">IP to use</param>
        /// <param name="port">Port to use</param>
        /// <param name="useHeaders">Use message headers</param>
        /// <param name="encryptionKey">Secret key to use for encryption, leave null for no encryption. Headers must be enabled.</param>
        protected virtual void Init(string ip, int port, bool useHeaders, string encryptionKey = null)
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Init(endpoint, useHeaders, encryptionKey);
        }

        /// <summary>
        /// Initialize connection details
        /// </summary>
        /// <param name="endpoint">Endpoint to use</param>
        /// <param name="useHeaders">Use message headers</param>
        /// <param name="encryptionKey">Secret key to use for encryption, leave null for no encryption. Headers must be enabled.</param>
        protected virtual void Init(IPEndPoint endpoint, bool useHeaders, string encryptionKey = null)
        {
            m_Endpoint = endpoint;

            m_Shutdown = false;
            m_UseHeaders = useHeaders;

            if (true == m_UseHeaders && false == String.IsNullOrWhiteSpace(encryptionKey))
                m_EncryptionKey = encryptionKey;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Close connection and flag shutdown state
        /// </summary>
        public virtual void Close()
        {
            try
            {
                m_Shutdown = true;
                m_Socket.Close();
            }
            catch { }
        }

        #endregion // Public Methods

        #region Protected Methods

        /// <summary>
        /// Send a message as text
        /// </summary>
        public bool Send(RemoteConnection connection, string data)
        {
            return Send(connection, System.Text.Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send a message as bytes
        /// </summary>
        public bool Send(RemoteConnection client, byte[] data)
        {
            try
            {
                // encrypt the data if we have a key
                if (null != m_EncryptionKey)
                    data = Encryption.Encrypt(data, m_EncryptionKey);
                
                // if we are using headers, send the amount of data that's going to arrive in the next packet first
                if (m_UseHeaders)
                    client.m_Socket.Send(BitConverter.GetBytes(data.Length));

                client.m_Socket.Send(data);
            }
            catch// (Exception e)
            {
                Disconnected(client);
            }

            return true;
        }

        /// <summary>
        /// Handle a disconnection, and fire event
        /// </summary>
        protected void Disconnected(RemoteConnection connection)
        {
            if (null != connection)
            {
                lock (this)
                {
                    if (null != OnDisconnect)
                        OnDisconnect(this, connection);
                }
            }
        }

        /// <summary>
        /// Wait for data from a connection
        /// </summary>
        protected void WaitForData(RemoteConnection connection)
        {
            // if we are shutting down, ignore data
            if (true == m_Shutdown)
                return;

            Package package = new Package()
            { 
                m_Connection = connection
            };

            try
            {
                // begin asynchronous wait for data on connection
                connection.m_Socket.BeginReceive(package.m_Data, 0, Package.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(DataReceived), package);
            }
            catch (Exception ex)
            {
                Logger.Error("[Socket] BeginReceive Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Handle data from a connection
        /// </summary>
        protected void DataReceived(IAsyncResult result)
        {
            // if we are shutting down, ignore data
            if (true == m_Shutdown)
                return;

            Package package = (Package)result.AsyncState;

            int bytesRead = 0;
            try
            {
                bytesRead = package.m_Connection.m_Socket.EndReceive(result);
            }
            catch (Exception ex)
            {
                Logger.Error("[Socket] DataReceived Error: " + ex.Message);
            }


            if (bytesRead > 0)
            {
                // if we are using headers, determine the message size and 
                // read from the socket until we have the entire message

                if (m_UseHeaders)
                {
                    const int headerSize = 4; // 4 bytes for package header
                    int bytesRemaining = bytesRead;
                    int dataOffset = 0;

                    while (bytesRemaining > 0)
                    {
                        // add the bytes read to the current stream until we have at least one int
                        if (package.m_Connection.m_Data.Length < headerSize)
                        {
                            int amountNeeded = (int)(headerSize - package.m_Connection.m_Data.Length);
                            int amountToRead = Math.Min(amountNeeded, bytesRemaining);
                            package.m_Connection.m_Data.Write(package.m_Data, dataOffset, amountToRead);
                            bytesRemaining -= amountToRead;
                            dataOffset += amountToRead;

                            // if we still don't have enough for the current header, we have to wait for more data
                            if (package.m_Connection.m_Data.Length < headerSize)
                            {
                                WaitForData(package.m_Connection);
                                return;
                            }

                            // otherwise, we can figure out the length of our package now
                            package.m_Connection.m_CurPackageSize = BitConverter.ToInt32(package.m_Connection.m_Data.GetBuffer(), 0);
                        }

                        // determine how much more data we need to complete this package
                        int dataNeeded = package.m_Connection.m_CurPackageSize - ((int)package.m_Connection.m_Data.Length - headerSize);
                        int dataToCopy = Math.Min(dataNeeded, bytesRemaining);

                        if (dataToCopy > 0)
                        {
                            package.m_Connection.m_Data.Write(package.m_Data, dataOffset, dataToCopy);
                            dataOffset += dataToCopy;
                            bytesRemaining -= dataToCopy;
                        }

                        // if we've finished off the package, go ahead and send it away
                        if (package.m_Connection.m_Data.Length == package.m_Connection.m_CurPackageSize + headerSize)
                        {
                            Package newPackage = new Package();
                            newPackage.m_Connection = package.m_Connection;
                            newPackage.m_Data = new byte[package.m_Connection.m_CurPackageSize];
                            Buffer.BlockCopy(package.m_Connection.m_Data.GetBuffer(), headerSize, newPackage.m_Data, 0, package.m_Connection.m_CurPackageSize);

                            // decrypt the data if we have an encryption key
                            if (null != m_EncryptionKey)
                            {
                                newPackage.m_Data = Encryption.Decrypt(newPackage.m_Data, m_EncryptionKey);
                            }

                            lock (this)
                            {
                                if (null != OnDataReceived)
                                    OnDataReceived(this, newPackage);
                            }

                            // reset our current data stream
                            package.m_Connection.m_Data.SetLength(0);
                            package.m_Connection.m_CurPackageSize = 0;
                        }
                    }

                    // nothing else to do now but wait for more data
                    WaitForData(package.m_Connection);
                }
                else
                {
                    // truncate
                    byte[] newData = new byte[bytesRead];
                    Array.Copy(package.m_Data, newData, bytesRead);
                    package.m_Data = newData;

                    lock (this)
                    {
                        if (null != OnDataReceived)
                            OnDataReceived(this, package);
                    }

                    WaitForData(package.m_Connection);
                }
            }
            else
            {
                Disconnected(package.m_Connection);
            }
        }

        #endregion

    }

    /// <summary>
    /// Details about a socket connection
    /// </summary>
    public class RemoteConnection
    {
        public Guid m_ID;
        public Socket m_Socket;

        // these variables are used for package-parsing off the stream
        public MemoryStream m_Data = new MemoryStream(); // the current in-progress package (from the async read)
        public int m_CurPackageSize = 0;

        public RemoteConnection(Socket socket)
        {
            m_ID = Guid.NewGuid();
            m_Socket = socket;
        }
    }

    /// <summary>
    /// Package of data received from a socket
    /// </summary>
    public class Package
    {
        public const int BUFFER_SIZE = 1024;
         
        public RemoteConnection m_Connection;
        public byte[] m_Data = new byte[BUFFER_SIZE];

        public string m_Text
        {
            get
            {
                return Encoding.UTF8.GetString(m_Data);
            }
        }
    }
}
