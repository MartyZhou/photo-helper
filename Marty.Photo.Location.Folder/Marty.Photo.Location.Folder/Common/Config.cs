using System.Collections.Generic;
using Newtonsoft.Json;

namespace Marty.Photo.Location.Folder.Common
{
    public class Config
    {
		[JsonProperty("photo_src_path")]
		public string PhotoSourceFolder { get; set; }

		[JsonProperty("photo_dest_path")]
		public string PhotoDestinationFolder { get; set; }

		[JsonProperty("new_path_format")]
		public string NewPhotoPathFormat { get; set; }

		[JsonProperty("excluded_cities")]
		public List<string> ExcludedCities { get; set; }

		[JsonProperty("excluded_area")]
		public List<Bounds> ExcludedArea { get; set; }

		[JsonProperty("time_tolerance")]
		public int TimeTolerance { get; set; }
		[JsonProperty("span_limit_days")]
		public int SpanLimitDays { get; set; }

		[JsonProperty("path")]
		public string Path { get; set; }

		[JsonProperty("api_key")]
		public string API_KEY { get; set; }
    }
}
