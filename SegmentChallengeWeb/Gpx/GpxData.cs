using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SegmentChallengeWeb.Gpx {
    [XmlRoot("gpx", Namespace = "http://www.topografix.com/GPX/1/1")]
    public class GpxData {
        [XmlAttribute("creator", Namespace = "http://www.topografix.com/GPX/1/1")]
        public String Creator { get; set; }

        [XmlAttribute("version", Namespace = "http://www.topografix.com/GPX/1/1")]
        public String Version { get; set; }

        [XmlElement("metadata", Namespace = "http://www.topografix.com/GPX/1/1")]
        public Metadata Metadata { get; set; }

        [XmlElement("trk", Namespace = "http://www.topografix.com/GPX/1/1")]
        public Track Track { get; set; }
    }

    // [XmlType("metadata", Namespace = "http://www.topografix.com/GPX/1/1")]
    public class Metadata {
        [XmlElement("time", Namespace = "http://www.topografix.com/GPX/1/1")]
        public DateTime? Time { get; set; }

        [XmlElement("name", Namespace = "http://www.topografix.com/GPX/1/1")]
        public String Name { get; set; }
    }

    public class Track {
        [XmlElement("name", Namespace = "http://www.topografix.com/GPX/1/1")]
        public String Name { get; set; }

        [XmlElement("type", Namespace = "http://www.topografix.com/GPX/1/1")]
        public String Type { get; set; }

        [XmlElement("trkseg", Namespace = "http://www.topografix.com/GPX/1/1")]
        public List<TrackSegment> Segments { get; set; }
    }

    // [XmlType("trkseg")]
    public class TrackSegment {
        [XmlElement("trkpt", Namespace = "http://www.topografix.com/GPX/1/1")]
        public List<TrackPoint> Points { get; set; }
    }

    // [XmlType("trkpt")]
    public class TrackPoint {
        [XmlAttribute("lat", Namespace = "http://www.topografix.com/GPX/1/1")]
        public Decimal Latitude { get; set; }

        [XmlAttribute("lon", Namespace = "http://www.topografix.com/GPX/1/1")]
        public Decimal Longitude { get; set; }

        [XmlElement("ele", Namespace = "http://www.topografix.com/GPX/1/1")]
        public Decimal Elevation { get; set; }

        [XmlElement("time", Namespace = "http://www.topografix.com/GPX/1/1")]
        public DateTime Time { get; set; }
    }
}
