using Core.Logging;
using Core.Networking.Sockets;
using System;

namespace Core.Networking
{
    public class TCPClient
    {
        #region Member Variables
        
        // socket connection
        private SocketClient _socketClient;

        public int ExperienceTypeID { get; set; }

        // flag to detirmine if the server is running
        private bool _running = false;

        public event SocketBase.DataReceivedHandler OnDataReceived;
        
        public bool IsConnected
        {
            get
            {
                return null != _socketClient && _socketClient.IsConnected();
            }
        }

        #endregion

        #region Public Methods

        public void Start(string endpoint, int experienceTypeID = -1)
        {
            try
            {
                Logger.Info("[SocketClient] Starting client.");

                ExperienceTypeID = experienceTypeID;
                InitializeSocketClient(endpoint);
                
                _running = true;
            }
            catch (Exception ex)
            {
                Logger.Error("[SocketClient] Startup error: " + ex);
            }
        }

        

        /// <summary>
        /// Start the socket client for communicating with the server
        /// </summary>
        private void InitializeSocketClient(string endpoint)
        {
            // get the server address
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                Logger.Error("[SocketClient] Endpoint is invalid, socket client can't be started.");
                return;
            }


            string[] endpointParts = endpoint.Split(':');
            
            int endpointPort;

            if (endpointParts.Length != 2 || false == int.TryParse(endpointParts[1], out endpointPort))
            {
                Logger.Error("[SocketClient] Endpoint is invalid, socket client can't be started.");
                return;
            }

            try
            {
                _socketClient = new SocketClient(endpointParts[0].ToString(), endpointPort, true);
                _socketClient.OnDisconnect += _socketClient_OnDisconnect;
                _socketClient.OnDataReceived += _socketClient_OnDataReceived;

                _socketClient.Connect();
            }
            catch (Exception ex)
            {
                Logger.Error("[SocketClient] Error starting client: " + ex.Message);
            }
        }
        

        private void _socketClient_OnDisconnect(object sender, RemoteConnection connection)
        {
            
        }

        private void _socketClient_OnDataReceived(object sender, Package packet)
        {
            try
            {
                if (null != OnDataReceived)
                {
                    OnDataReceived(this, packet);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[SocketClient] Error processing request from server: {0}", ex);
            }

        }

        /// <summary>
        /// Stop the server, close connection, kill any running processes
        /// </summary>
        public void Stop()
        {
            if (false == _running)
                return;

            Logger.Info("[SocketClient] Stopping...");

            
            if (null != _socketClient)
                _socketClient.Close();

            _running = false;
        }
        
        public void Send(string data)
        {
            if (false == _running || null == _socketClient)
                return;

            _socketClient.Send(data);
        }

        #endregion
        
    }
}
