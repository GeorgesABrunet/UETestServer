using Core.Logging;
using NAudio.Wave;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace Core
{
    public static class Utilities
    {
        /// <summary>
        /// Read a config value from the app.config file
        /// </summary>
        /// <param name="key">Key of the config value to read</param>
        /// <returns></returns>
        public static string GetConfigValue(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        /// <summary>
        /// Read a config value from the app.config file and return the specified type.
        /// </summary>
        /// <typeparam name="T">Type of the value to read</typeparam>
        /// <param name="key">Key of the config value to read</param>
        /// <returns></returns>
        public static T GetConfigValue<T>(string key)
        {
            Type myType = typeof(T);
            string value = GetConfigValue(key);

            if (myType.IsEnum)
            {
                return (T)Enum.Parse(myType, value);
            }
            else
            {
                return (T)Convert.ChangeType(value, myType);
            }
        }

        /// <summary>
        /// Read a config value from the app.config file and return the specified type.
        /// </summary>
        /// <typeparam name="T">Type of the value to read</typeparam>
        /// <param name="key">Key of the config value to read</param>
        /// <param name="defaultValue">Default value if not found in config file</param>
        /// <returns></returns>
        public static T GetConfigValue<T>(string key, T defaultValue)
        {
            if (ConfigurationManager.AppSettings.AllKeys.Contains(key))
                return GetConfigValue<T>(key);
            else
                return defaultValue;
        }

        /// <summary>
        /// Get's the local machine's internal IP address
        /// </summary>
        public static IPAddress GetLocalIPAddress(System.Net.Sockets.AddressFamily family = System.Net.Sockets.AddressFamily.InterNetwork)
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == family)
                {
                    return ip;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolve an address to an IP
        /// </summary>
        /// <param name="address">The address to resolve</param>
        /// <param name="family">The type of IP to return</param>
        /// <returns></returns>
        public static IPAddress ResolveToIPAddress(string address, System.Net.Sockets.AddressFamily family = System.Net.Sockets.AddressFamily.InterNetwork)
        {
            IPAddress[] hosts = Dns.GetHostAddresses(address);

            foreach (IPAddress ip in hosts)
            {
                if (ip.AddressFamily == family)
                {
                    return ip;
                }
            }

            return null;
        }

        public static string GetPathLast(string path)
        {
            string[] pathParts = path.Split('/');
            if (pathParts.Length > 0)
            {
                return pathParts[pathParts.Length - 1];
            }
            return path;
        }

        public static string GetAppDirectory()
        {
            return System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
        }

        public static byte[] GetAudioSamplesFromFile(string InputFile, string OutputFile = null)
        {
            byte[] AudioData = new byte[0];

            if (File.Exists(InputFile))
            {
                try
                {
                    using (var inputReader = new MediaFoundationReader(InputFile))
                    {
                        var outFormat = new WaveFormat(16000, 16, 1);

                        using (var resampler = new MediaFoundationResampler(inputReader, outFormat))
                        {
                            if (String.IsNullOrWhiteSpace(OutputFile))
                            {
                                OutputFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(InputFile));
                            }

                            WaveFileWriter.CreateWaveFile(OutputFile, resampler);

                            using (WaveFileReader reader = new WaveFileReader(OutputFile))
                            {
                                AudioData = new byte[reader.Length];
                                reader.Read(AudioData, 0, AudioData.Length);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Error reading audio file: {0}", ex.Message);
                }

            }

            return AudioData;
        }

        public static byte[] GetPreProcessedAudioData(string FilePath)
        {
            byte[] AudioData = new byte[0];

            if (File.Exists(FilePath))
            {
                string OutputFile = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Path.GetFileName(FilePath), "ols"));

                try
                {
                    Process p = new Process();
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.FileName = Path.Combine(Core.Utilities.GetAppDirectory(), "AudioPreProcessor.exe");
                    p.StartInfo.Arguments = String.Format("\"{0}\" \"{1}\"", FilePath, OutputFile);
                    p.Start();
                    string output = p.StandardOutput.ReadToEnd();
                    string error = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    Logger.Info(output);

                    if (p.ExitCode != 0)
                    {
                        Logger.Error(error);
                    }
                    else
                    {
                        AudioData = File.ReadAllBytes(OutputFile);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Error pre-processing audio file: {0}", ex.Message);
                }

            }

            return AudioData;
        }
    }
}
