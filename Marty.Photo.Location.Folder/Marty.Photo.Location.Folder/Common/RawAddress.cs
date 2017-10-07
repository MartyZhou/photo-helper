using System;
using Newtonsoft.Json;

namespace Marty.Photo.Location.Folder.Common
{
    public struct CityLocation
    {
        public string City { get; set; }
        public string AreaLevel2 { get; set; }
        public string AreaLevel1 { get; set; }
        public string Country { get; set; }
    }

    public struct AddressType
    {
        [JsonProperty("long_name")]
        public string LongName { get; set; }

        [JsonProperty("short_name")]
        public string ShortName { get; set; }

        [JsonProperty("types")]
        public string[] Types { get; set; }
    }

    public struct AddressDetails
    {
        [JsonProperty("address_components")]
        public AddressType[] AddressComponents { get; set; }

        [JsonProperty("formatted_address")]
        public string FormattedAddress { get; set; }

        [JsonProperty("place_id")]
        public string PlaceId { get; set; }

        [JsonProperty("geometry")]
        public Geometry Geometry { get; set; }

        [JsonProperty("types")]
        public string[] Types { get; set; }
    }

    public struct Location
    {
        [JsonProperty("lat")]
        public double Lat { get; set; }

        [JsonProperty("lng")]
        public double Lng { get; set; }
    }

    public struct Bounds
    {
        [JsonProperty("northeast")]
        public Location Norteast { get; set; }

        [JsonProperty("southwest")]
        public Location Southwest { get; set; }
    }

    public struct Geometry
    {
        [JsonProperty("bounds")]
        public Bounds Bounds { get; set; }

        [JsonProperty("location")]
        public Location Location { get; set; }

        [JsonProperty("location_type")]
        public string LocationType { get; set; }

        [JsonProperty("types")]
        public string[] Types { get; set; }
    }

    public struct AddressResult
    {
        [JsonProperty("results")]
        public AddressDetails[] Results { get; set; }

        public string City { get; set; }

        public bool IsValid { get; set; }
    }

    public struct TripSpan
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public struct TripDuration
    {
        public AddressDetails Location { get; set; }
        public TripSpan[] Spans { get; set; }
        public TripDuration[] Parents { get; set; }
    }
}
