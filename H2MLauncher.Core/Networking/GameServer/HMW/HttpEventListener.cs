using System.Diagnostics.Tracing;

namespace H2MLauncher.Core.Networking.GameServer.HMW
{
    public sealed class HttpEventListener : EventListener
    {
        // Constant necessary for attaching ActivityId to the events.
        public const EventKeywords TasksFlowActivityIds = (EventKeywords)0x80;
        private readonly AsyncLocal<HttpRequestTimingDataRaw> _timings = new();
        public HttpEventListener()
        {
            // set variable here
            _timings.Value = new HttpRequestTimingDataRaw();
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // List of event source names provided by networking in .NET 5.
            if (eventSource.Name == "System.Net.Http" ||
                eventSource.Name == "System.Net.Sockets" ||
                eventSource.Name == "System.Net.Security" ||
                eventSource.Name == "System.Net.NameResolution")
            {
                EnableEvents(eventSource, EventLevel.LogAlways);
            }
            // Turn on ActivityId.
            else if (eventSource.Name == "System.Threading.Tasks.TplEventSource")
            {
                // Attach ActivityId to the events.
                EnableEvents(eventSource, EventLevel.LogAlways, TasksFlowActivityIds);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            var timings = _timings.Value;
            if (timings == null)
                return; // some event which is not related to this scope, ignore it
            var fullName = eventData.EventSource.Name + "." + eventData.EventName;

            switch (fullName)
            {
                case "System.Net.Http.RequestStart":
                    timings.RequestStart = eventData.TimeStamp;
                    break;
                case "System.Net.Http.RequestStop":
                    timings.RequestStop = eventData.TimeStamp;
                    break;
                case "System.Net.NameResolution.ResolutionStart":
                    timings.DnsStart = eventData.TimeStamp;
                    break;
                case "System.Net.NameResolution.ResolutionStop":
                    timings.DnsStop = eventData.TimeStamp;
                    break;
                case "System.Net.Sockets.ConnectStart":
                    timings.SocketConnectStart = eventData.TimeStamp;
                    break;
                case "System.Net.Sockets.ConnectStop":
                    timings.SocketConnectStop = eventData.TimeStamp;
                    break;
                case "System.Net.Security.HandshakeStart":
                    timings.SslHandshakeStart = eventData.TimeStamp;
                    break;
                case "System.Net.Security.HandshakeStop":
                    timings.SslHandshakeStop = eventData.TimeStamp;
                    break;
                case "System.Net.Http.RequestLeftQueue":
                    timings.RequestLeftQueue = eventData.TimeStamp;
                    break;
                case "System.Net.Http.RequestHeadersStart":
                    timings.RequestHeadersStart = eventData.TimeStamp;
                    break;
                case "System.Net.Http.RequestHeadersStop":
                    timings.RequestHeadersStop = eventData.TimeStamp;
                    break;
                case "System.Net.Http.ResponseHeadersStart":
                    timings.ResponseHeadersStart = eventData.TimeStamp;
                    break;
                case "System.Net.Http.ResponseHeadersStop":
                    timings.ResponseHeadersStop = eventData.TimeStamp;
                    break;
                case "System.Net.Http.ResponseContentStart":
                    timings.ResponseContentStart = eventData.TimeStamp;
                    break;
                case "System.Net.Http.ResponseContentStop":
                    timings.ResponseContentStop = eventData.TimeStamp;
                    break;
            }
        }

        public HttpRequestTimings GetTimings()
        {
            var raw = _timings.Value!;
            return new HttpRequestTimings
            {
                Request = raw.RequestStop - raw.RequestStart,
                Response = raw.RequestStop - (raw.RequestLeftQueue ?? raw.RequestHeadersStart),
                Dns = raw.DnsStop - raw.DnsStart,
                SslHandshake = raw.SslHandshakeStop - raw.SslHandshakeStart,
                SocketConnect = raw.SocketConnectStop - raw.SocketConnectStart,
                RequestHeaders = raw.RequestHeadersStop - raw.RequestHeadersStart,
                ResponseHeaders = raw.ResponseHeadersStop - raw.ResponseHeadersStart,
                ResponseContent = raw.ResponseContentStop - raw.ResponseContentStart
            };
        }

        public class HttpRequestTimings
        {
            public TimeSpan? Request { get; set; }

            public TimeSpan? Response { get; set; }
            public TimeSpan? Dns { get; set; }
            public TimeSpan? SslHandshake { get; set; }
            public TimeSpan? SocketConnect { get; set; }
            public TimeSpan? RequestHeaders { get; set; }
            public TimeSpan? ResponseHeaders { get; set; }
            public TimeSpan? ResponseContent { get; set; }
        }

        private class HttpRequestTimingDataRaw
        {
            public DateTime? DnsStart { get; set; }
            public DateTime? DnsStop { get; set; }
            public DateTime? RequestStart { get; set; }
            public DateTime? RequestStop { get; set; }
            public DateTime? RequestLeftQueue { get; set; }
            public DateTime? SocketConnectStart { get; set; }
            public DateTime? SocketConnectStop { get; set; }
            public DateTime? SslHandshakeStart { get; set; }
            public DateTime? SslHandshakeStop { get; set; }
            public DateTime? RequestHeadersStart { get; set; }
            public DateTime? RequestHeadersStop { get; set; }
            public DateTime? ResponseHeadersStart { get; set; }
            public DateTime? ResponseHeadersStop { get; set; }
            public DateTime? ResponseContentStart { get; set; }
            public DateTime? ResponseContentStop { get; set; }
        }
    }
}
