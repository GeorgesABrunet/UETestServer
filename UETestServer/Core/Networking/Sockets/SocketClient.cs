using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Core.Logging;

namespace Core.Networking.Sockets
{
    public class SocketClient : SocketBase
    {
        #region Private Members

        // thread to use for monitoring the connection and handling reconnecting
        private Thread m_ClientThread;

        // information about the connected server
        private RemoteConnection m_Server;

        // flag used to only log connection errors once when retrying
        private bool m_LogConnectionError = true;

        #endregion
        
        /// <summary>
        /// Instantiates a new SocketClient
        /// </summary>
        /// <param name="ip">The address of the server to connect to</param>
        /// <param name="port">The port to connect on</param>
        /// <param name="useHeaders">Use message headers, must match server configuration</param>
        /// <param name="encryptionKey">Secret key to use for encryption, leave null for no encryption. Headers must be enabled.</param>
        public SocketClient(string ip, int port, bool useHeaders, string encryptionKey = null)
        {
            try
            {
                // if we got a named address, resolve it to an IP
                IPAddress myIP = Utilities.ResolveToIPAddress(ip);
                ip = myIP.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error("[SocketClient] Init Error: " + ex.Message);
            }
            
            Init(ip, port, useHeaders, encryptionKey);
        }

        /// <summary>
        /// Instantiate a new SocketClient
        /// </summary>
        /// <param name="endpoint">Endpoint to connect to, should contain both an IP and Port</param>
        /// <param name="useHeaders">Use message headers, must match server configuration</param>
        /// <param name="encryptionKey">Secret key to use for encryption, leave null for no encryption. Headers must be enabled.</param>
        public SocketClient(IPEndPoint endpoint, bool useHeaders, string encryptionKey = null)
        {
            Init(endpoint, useHeaders, encryptionKey);
        }


        #region Public Methods

        /// <summary>
        /// Start attempting to connect to the server
        /// </summary>
        public void Connect()
        {
            ThreadStart ts = new ThreadStart(StartConnection);
            m_ClientThread = new Thread(ts);
            m_ClientThread.Start();      
        }

        /// <summary>
        /// Send a message as text
        /// </summary>
        public bool Send(string text)
        {
            return Send(m_Server, text);
        }

        /// <summary>
        /// Send a message as bytes
        /// </summary>
        public bool Send(byte[] data)
        {
            return Send(m_Server, data);
        }

        /// <summary>
        /// Close the connection to the server
        /// </summary>
        public override void Close()
        {
            base.Close();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Open a connection and attempt to maintain it.
        /// If connection cannot be made or is disconnected, keep retrying indefinitely.
        /// </summary>
        private void StartConnection()
        {
            while (true)
            {
                if (true == m_Shutdown)
                {
                    break;
                }

                if (false == IsConnected())
                {
                    DoConnect();
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// Attempt to open a connection and start waiting for data.
        /// </summary>
        private void DoConnect()
        {
            try
            {
                m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                m_Server = new RemoteConnection(m_Socket); 
                m_Socket.Connect(m_Endpoint);
            }
            catch (Exception ex)
            {
                if (true == m_LogConnectionError)
                {
                    Logger.Error("[SocketClient] Start Error: " + ex.Message);
                    m_LogConnectionError = false;
                }

                return;   
            }

            Logger.Info(String.Format("[SocketClient] Connected. IP: {0}:{1}", m_Endpoint.Address, m_Endpoint.Port));

            m_LogConnectionError = true;
            OnDisconnect += SocketClient_OnDisconnect;
            
            // now wait for data on that socket
            WaitForData(m_Server);             
        }

        /// <summary>
        /// Handle disconnect events
        /// </summary>
        private void SocketClient_OnDisconnect(object sender, RemoteConnection connection)
        {
            OnDisconnect -= SocketClient_OnDisconnect;
        }

        /// <summary>
        /// Check connection state
        /// </summary>
        public bool IsConnected()
        {
            try
            {
                // no socket, no connection
                if (null == m_Socket)
                    return false;

                // check socket connected state
                else if (false == m_Socket.Connected)
                    return false;

                // Connected property isn't reliable, try polling server if it reports true
                else if (m_Socket.Poll(1, SelectMode.SelectRead) && m_Socket.Available == 0)
                    return false;

                // probably connected
                else
                    return true;
            }
            catch
            { 
                // if we get here, assume there's no connection
                return false;
            }
        }
        #endregion
    }
}