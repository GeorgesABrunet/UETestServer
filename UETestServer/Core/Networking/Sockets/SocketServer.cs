using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Core.Logging;

namespace Core.Networking.Sockets
{
    public class SocketServer : SocketBase
    {
        #region Private Members

        // thread and event for listening for and handling client connections
        private Thread m_ListenerThread;
        protected ManualResetEvent m_ConnectionWaitEvent;

        // list of connected clients
        protected Dictionary<Guid, RemoteConnection> m_Clients;

        // client connect event
        public event ConnectionHandler OnClientConnect;

        #endregion

        /// <summary>
        /// Instantiate a new SocketServer
        /// </summary>
        /// <param name="ip">IP to bind to</param>
        /// <param name="port">Port to bind to</param>
        /// <param name="useHeaders">Use message headers, clients must be configured the same way</param>
        /// <param name="encryptionKey">Secret key to use for encryption, leave null for no encryption. Headers must be enabled.</param>
        public SocketServer(string ip, int port, bool useHeaders, string encryptionKey = null)
        {
            InitServer();
            base.Init(ip, port, useHeaders, encryptionKey);
        }

        /// <summary>
        /// Instantiate a new SocketServer
        /// </summary>
        /// <param name="endpoint">Endpoint to bind to</param>
        /// <param name="useHeaders">Use message headers, clients must be configured the same way</param>
        /// <param name="encryptionKey">Secret key to use for encryption, leave null for no encryption. Headers must be enabled.</param>
        public SocketServer(IPEndPoint endpoint, bool useHeaders, string encryptionKey = null)
        {
            InitServer();
            base.Init(endpoint, useHeaders, encryptionKey);
        }

        #region Public Methods

        /// <summary>
        /// Start server and bind to endpoint
        /// </summary>
        public void Start()
        {
            try
            {
                // create socket
                m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // bind to endpoint
                m_Socket.Bind(m_Endpoint);
                m_Socket.Listen(100);

                // start listening for clients
                ThreadStart ts = new ThreadStart(StartListening);
                m_ListenerThread = new Thread(ts);
                m_ListenerThread.Start();


                Logger.Info(String.Format("[SocketServer] Started. IP: {0}:{1}", m_Endpoint.Address, m_Endpoint.Port));
            }
            catch (Exception ex)
            {
                Logger.Error("[SocketServer] Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Stop socket server and disconnect clients
        /// </summary>
        public void Stop()
        {
            try
            {
                m_ConnectionWaitEvent.Close();

                // disconnect all clients
                while (m_Clients.Count() > 0)
                {
                    RemoteConnection client = m_Clients.First().Value;
                    Disconnected(client);
                    client.m_Socket.Disconnect(false);
                }

                //if (null != m_ListenerThread)
                //    m_ListenerThread.Abort();

                Close();

                Logger.Info("[SocketServer] Stopped.");
            }
            catch (Exception ex)
            {
                Logger.Error("Error stopping socket server: " + ex.Message);
            }
        }

        /// <summary>
        /// Send data to a client as text
        /// </summary>
        public bool Send(Guid clientID, string data)
        {
            return Send(clientID, System.Text.Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send data to a client as bytes
        /// </summary>
        public bool Send(Guid clientID, byte[] data)
        {
            RemoteConnection client;

            if (m_Clients.TryGetValue(clientID, out client))
            {
                return Send(client, data);
            }

            return false;
        }

        /// <summary>
        /// Broadcast data to all connected clients as text
        /// </summary>
        public void Broadcast(string data)
        {
            Broadcast(System.Text.Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Broadcast data to all connected clients as bytes
        /// </summary>
        public void Broadcast(byte[] data)
        {
            KeyValuePair<Guid, RemoteConnection>[] clientListCopy = m_Clients.ToArray();
            for (int i = 0; i < clientListCopy.Length; i++)
            {
                RemoteConnection client = clientListCopy[i].Value;

                Send(client, data);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Instantiate local variables and components
        /// </summary>
        private void InitServer()
        {
            m_Clients = new Dictionary<Guid, RemoteConnection>();
            m_ConnectionWaitEvent = new ManualResetEvent(false);
            OnDisconnect += SocketServer_OnDisconnect;
        }

        /// <summary>
        /// Listen for and handle new client connections
        /// </summary>
        private void StartListening()
        {
            while (true)
            {
                m_ConnectionWaitEvent.Reset();

                m_Socket.BeginAccept(new AsyncCallback(ClientConnected), null);

                m_ConnectionWaitEvent.WaitOne();

                if (true == m_Shutdown)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Handle client connection and begin waiting for data
        /// </summary>
        private void ClientConnected(IAsyncResult result)
        {
            m_ConnectionWaitEvent.Set();

            if (m_Shutdown)
            {
                m_ConnectionWaitEvent.Close();
                return;
            }

            RemoteConnection client = new RemoteConnection(m_Socket.EndAccept(result));
            m_Clients.Add(client.m_ID, client);

            Logger.Info("[SocketServer] Client connected: {0}", client.m_Socket.RemoteEndPoint);

            lock (this)
            {
                if (null != OnClientConnect)
                    OnClientConnect(this, client);
            }

            WaitForData(client);
        }

        /// <summary>
        /// Handle client disconnections
        /// </summary>
        private void SocketServer_OnDisconnect(object sender, RemoteConnection client)
        {
            Logger.Info("[SocketServer] Client disconnected: {0}", client.m_Socket.RemoteEndPoint); 
            
            m_Clients.Remove(client.m_ID);
        }

        #endregion // Private Methods

    }

}
