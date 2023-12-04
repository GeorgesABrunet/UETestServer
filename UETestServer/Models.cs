using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Core.Logging;
using Newtonsoft.Json;
using RestSharp;

namespace UETestServer
{
    public enum ServerType { MQTT, TCP }

    //FMessageTemplate allows you to slap an "event" with your FMessageData.
    //The benefit of this depends on the Topic structure for your project.
    [Serializable]
    public class FMessageTemplate
    {
        public string Topic { get; set; }
        
        [JsonProperty("event")]
        public string Event { get; set; }

        public FMessageData data { get; set; }

        public FMessageTemplate()
        {
            data = new FMessageData();
        }

        public FMessageTemplate(string @topic)
        {
            Topic = @topic;
            data = new FMessageData();
        }

        public FMessageTemplate(string @topic, string @event)
        {
            Topic = @topic;
            Event = @event;
            data = new FMessageData();
        }
    }

    //Edit FMessageData to contain whatever parameters your JSON messages should hold,
    // Remember to account for its parameters in the GUI.
    [Serializable]
    public class FMessageData
    {
        public string Txt { get; set; }
        public string Relevant_WAV { get; set; }
        
    }

    //This is to chain messages
    [Serializable]
    public class FSequenceParams
    {
        public string SequenceTopicName { get; set; }
        public string JsonFilename { get; set; }
        public bool ContainsAudio { get; set; }
        public string SequenceAudioFilename { get; set; }
    }

}
