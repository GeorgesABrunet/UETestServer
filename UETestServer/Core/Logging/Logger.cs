using System;

namespace Core.Logging
{
    public static class Logger
    {
        #region Member Variables

        public enum LogType { ERROR, WARNING, INFO, DEBUG }
        //private static ILog _log = null;

        #endregion

        #region Public methods

        public static void Init()
        {
            Init("Logger");
        }

        public static void Init(string name)
        {
            //log4net.Config.XmlConfigurator.Configure();
            //_log = LogManager.GetLogger(name);
        }

        public static void Info(string message, params object[] formatArgs)
        {
            Log(LogType.INFO, formatArgs.Length > 0 ? string.Format(message, formatArgs) : message);
        }

        public static void Warning(string message, params object[] formatArgs)
        {
            Log(LogType.WARNING, formatArgs.Length > 0 ? string.Format(message, formatArgs) : message);
        }

        public static void Debug(string message, params object[] formatArgs)
        {
            Log(LogType.DEBUG, formatArgs.Length > 0 ? string.Format(message, formatArgs) : message);
        }

        public static void Error(Exception exception, string message, params object[] formatArgs)
        {
            Log(LogType.ERROR, formatArgs.Length > 0 ? string.Format(message, formatArgs) : message, exception);
        }

        public static void Error(string message, params object[] formatArgs)
        {
            Log(LogType.ERROR, formatArgs.Length > 0 ? string.Format(message, formatArgs) : message);             
        }

        #endregion

        #region Private Methods

        private static void Log(LogType type, string message, Exception exception = null)
        {
            //if (null == _log)
            //    Init();
            
            switch (type)
            {
                case LogType.ERROR:
                    {
                        Console.Error.WriteLine(message);

                        //if (null != exception)                            
                        //    _log.Error(message, exception);
                        //else
                        //    _log.Error(message);

                        break;
                    }
                case LogType.WARNING:
                    {
                        Console.WriteLine(message);
                        //_log.Warn(message);
                        break;
                    }
                case LogType.INFO:
                    {
                        Console.WriteLine(message); 
                        //_log.Info(message);
                        break;
                    }
                case LogType.DEBUG:
                    {
                        Console.WriteLine(message); 
                        //_log.Debug(message);
                        break;
                    }
            }

            OnLog(new LogEventArgs(type, message));
        }

        #endregion
        
        #region Events
        public delegate void LogEventHandler(object sender, LogEventArgs e);

        public static event LogEventHandler LogReceived;
        private static void OnLog(LogEventArgs e)
        {
            LogEventHandler thisEvent = LogReceived;
            if (thisEvent != null)
                thisEvent(null, e);
        }

        public class LogEventArgs : EventArgs
        {
            public LogType Type;
            public string Message;

            public LogEventArgs(LogType type, string message)
            {
                Type = type;
                Message = message;
            }
        }
        #endregion
    }
}
