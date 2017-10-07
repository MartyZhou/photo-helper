using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Marty.JPG.EXIF;
using Marty.JPG.EXIF.Common;
using Marty.Photo.Location.Folder.Common;
using Newtonsoft.Json;

namespace Marty.Photo.Location.Folder
{
    class Program
    {
		private static readonly Config config = GetConfig();
		private static List<string> unprocessedPhotos = new List<string>();
		private static List<string> notCopiedPhotos = new List<string>();
		private static List<string> exceptionPhotos = new List<string>();
        private static List<PhotoMetadata> noGPSPhotos = new List<PhotoMetadata>();
		private static Dictionary<string, Tuple<DateTime, DateTime, string>> locationTimeSpan = new Dictionary<string, Tuple<DateTime, DateTime, string>>();

		static void Main(string[] args)
		{
			Console.WriteLine("*********** Started ***********");

			Console.WriteLine(JsonConvert.SerializeObject(config));

			var photoPaths = Directory.EnumerateFiles(config.PhotoSourceFolder, "*.jpg", SearchOption.AllDirectories);

			Task.WaitAll(ProcessPhotos(photoPaths));

			ProcessNoGPSPhotos();

			LogUnprocessedPhotos();

			LogNotCopiedPhotos();

			LogExceptionPhotos();

			Console.WriteLine("*********** Finished ***********");
		}

		private static async Task ProcessPhotos(IEnumerable<string> paths)
		{
			foreach (var path in paths)
			{
				try
				{
					var photoInfo = await ReadMeta(path).ConfigureAwait(false);
                    if (photoInfo.ExifMetadata != null && photoInfo.ExifMetadata.LatRef == 'N')
					{
						CreateNewPhotoPathWithLocation(photoInfo);
						CopyPhoto(photoInfo);
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(string.Format("[PhotoHelper] Error proccessing photo: {0}", e.Message));
					exceptionPhotos.Add(path);
				}
			}
		}

		private static void CopyPhoto(PhotoMetadata info)
		{
			try
			{
				// Console.WriteLine(string.Format("************************** Trying to copy file: {0}", info.NewPath));
				var directory = Path.GetDirectoryName(info.NewPath);
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				if (!File.Exists(info.NewPath))
				{
					Console.WriteLine(string.Format("[PhotoHelper] Copying file: {0}", info.NewPath));
					File.Copy(info.FilePath, info.NewPath);
				}
				else
				{
					info.NewPath = string.Format("{0}_{1}.jpg", info.NewPath, Guid.NewGuid());
					File.Copy(info.FilePath, info.NewPath);
					Console.WriteLine(string.Format("[PhotoHelper] Copying file with Guid path: {0}", info.NewPath));
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(string.Format("[PhotoHelper] Error copying photo: {0}", e.Message));
				notCopiedPhotos.Add(info.FilePath);
			}
		}

		private static async Task<PhotoMetadata> ReadMeta(string path)
		{
			var result = new PhotoMetadata();
			using (FileStream stream = new FileStream(path, FileMode.Open))
			{
				var reader = new ExifReader(stream);
                var meta = reader.Read();
				if (meta != null)
				{
					result.FilePath = stream.Name;
					meta.Model = meta.Model.Trim('\x00');

					//Console.WriteLine(string.Format("[PhotoHelper]###################### ReadMeta for file {0}. meta: {1}", meta.FilePath, JsonConvert.SerializeObject(meta)));

                    result.ExifMetadata = meta;
                    result.HasLocation |= (meta.LatRef == 'N' || meta.LatRef == 'S');

					if (result.HasLocation)
					{
						var cityLocation = await Cache.GetCityName(meta.LatRef, meta.Lat, meta.LonRef, meta.Lon).ConfigureAwait(false);
						var cityAndCountryName = GetCityAndCountryName(cityLocation);
						result.City = cityAndCountryName.Item1;
						result.Country = cityAndCountryName.Item2;

						if (string.IsNullOrWhiteSpace(result.City))
						{
							noGPSPhotos.Add(result);
							//meta.HasLocation = false;
						}
					}
					else
					{
						noGPSPhotos.Add(result);
					}
				}
				else
				{
					unprocessedPhotos.Add(path);
				}
			}

			return result;
		}

		private static void CreateNewPhotoPathWithLocation(PhotoMetadata info)
		{
            if (info.HasLocation && !string.IsNullOrWhiteSpace(info.City))
			{
				Console.WriteLine(string.Format("[PhotoHelper]###################### CreateNewPhotoPathWithLocation for city {0}. Info: {1}", info.City, JsonConvert.SerializeObject(info)));
				info.NewPath = CreateGPSNewPath(info);

				if (!IsCityExcluded(info.City) && !IsLocationExcluded(info.ExifMetadata))
				{
					CacheLocationTimeSpan(info);
				}
			}
		}

		private static void CacheLocationTimeSpan(PhotoMetadata info)
		{
			if (!locationTimeSpan.ContainsKey(info.City))
			{
				locationTimeSpan.Add(info.City, new Tuple<DateTime, DateTime, string>(info.ExifMetadata.TakenDate, info.ExifMetadata.TakenDate, info.Country));
			}
			else
			{
				var dateSpan = locationTimeSpan[info.City];
				//Console.WriteLine(string.Format("[PhotoHelper]###################### before city {0} adjusted time span {1}", info.City, JsonConvert.SerializeObject(dateSpan)));

				if (dateSpan.Item1 > info.ExifMetadata.TakenDate && dateSpan.Item1.Subtract(info.ExifMetadata.TakenDate).TotalDays < config.SpanLimitDays)
				{
					locationTimeSpan[info.City] = new Tuple<DateTime, DateTime, string>(info.ExifMetadata.TakenDate, dateSpan.Item2, info.Country);
				}

				if (dateSpan.Item2 < info.ExifMetadata.TakenDate && info.ExifMetadata.TakenDate.Subtract(dateSpan.Item2).TotalDays < config.SpanLimitDays)
				{
					locationTimeSpan[info.City] = new Tuple<DateTime, DateTime, string>(dateSpan.Item1, info.ExifMetadata.TakenDate, info.Country);
				}

				//Console.WriteLine(string.Format("[PhotoHelper]###################### after city {0} adjusted time span {1}", info.City, JsonConvert.SerializeObject(dateSpan)));
			}
		}

		private static string CreateNoGPSNewPath(PhotoMetadata info)
		{
			return string.Format(@"{0}/!NoGPS/{1}/{2}/{2}_{3}.jpg", config.PhotoDestinationFolder, info.ExifMetadata.TakenDate.ToString("yyyy-MM"), info.ExifMetadata.Model, info.ExifMetadata.TakenDate.ToString("yyyy-MM-dd HH-mm-ss"));
		}

		private static string CreateGPSNewPath(PhotoMetadata info)
		{
			return string.Format(@"{0}/{1}/{2}/{3}/{1}_{2}_{4}.jpg", config.PhotoDestinationFolder, info.Country, info.City, info.ExifMetadata.Model, info.ExifMetadata.TakenDate.ToString("yyyy-MM-dd HH-mm-ss"));
		}

		private static void ProcessNoGPSPhotos()
		{
			var tolerance = new TimeSpan(0, config.TimeTolerance, 0);

			foreach (var photo in noGPSPhotos)
			{
				AppendLocationForNoneGPSPhotos(photo, tolerance);

				if (string.IsNullOrWhiteSpace(photo.City))
				{
					photo.NewPath = CreateNoGPSNewPath(photo);
				}
				else
				{
					photo.NewPath = CreateGPSNewPath(photo);
				}

				CopyPhoto(photo);
			}
		}

		private static void LogUnprocessedPhotos()
		{
			File.WriteAllText(string.Format(@"{0}/unprocessed_photos.txt", config.PhotoDestinationFolder), JsonConvert.SerializeObject(unprocessedPhotos));
		}

		private static void LogNotCopiedPhotos()
		{
			File.WriteAllText(string.Format(@"{0}/not_copyied_photos.txt", config.PhotoDestinationFolder), JsonConvert.SerializeObject(notCopiedPhotos));
		}

		private static void LogExceptionPhotos()
		{
			File.WriteAllText(string.Format(@"{0}/exception_photos.txt", config.PhotoDestinationFolder), JsonConvert.SerializeObject(exceptionPhotos));
		}

		private static Config GetConfig()
		{
			using (var stream = new FileStream("./config.json", FileMode.Open))
			{
				using (var reader = new StreamReader(stream))
				{
					return JsonConvert.DeserializeObject<Config>(reader.ReadToEnd());
				}
			}
		}

		private static Tuple<string, string> GetCityAndCountryName(CityLocation cityLocation)
		{
			var city = string.Empty;
			var country = cityLocation.Country;

			if (!string.IsNullOrWhiteSpace(cityLocation.City))
			{
				city = cityLocation.City;
			}
			else if (!string.IsNullOrWhiteSpace(cityLocation.AreaLevel2))
			{
				city = cityLocation.AreaLevel2;
			}
			else if (!string.IsNullOrWhiteSpace(cityLocation.AreaLevel1))
			{
				city = cityLocation.AreaLevel1;
			}
			else
			{
				city = country;
			}

			if (string.IsNullOrWhiteSpace(country))
			{
				country = city;
			}

			return new Tuple<string, string>(city, country);
		}

		private static bool IsCityExcluded(string cityName)
		{
			var result = config.ExcludedCities.Contains(cityName.ToLower());

			if (result)
			{
				Console.WriteLine(string.Format("[PhotoHelper]###################### city {0} is excluded for location appending", cityName));
			}

			return result;
		}

        private static bool IsLocationExcluded(ExifMetadata meta)
		{
			var result = false;

			var lat = meta.LatRef == 'N' ? meta.Lat : -meta.Lat;
			var lon = meta.LonRef == 'E' ? meta.Lon : -meta.Lon;

			foreach (var area in config.ExcludedArea)
			{
				var latTest = lat <= area.Norteast.Lat && lat >= area.Southwest.Lat;
				var lonTest = lon <= area.Norteast.Lng && lon >= area.Southwest.Lng;

				if (latTest && lonTest)
				{
					result = true;

					Console.WriteLine(string.Format("[PhotoHelper]###################### exclude location: {0}", JsonConvert.SerializeObject(meta)));
					break;
				}
			}

			return result;
		}

		private static void AppendLocationForNoneGPSPhotos(PhotoMetadata info, TimeSpan tolerance)
		{
			foreach (var item in locationTimeSpan)
			{
				//var leftTime = item.Value.Item1.Subtract(tolerance);
				//var rightTime = item.Value.Item2.Add(tolerance);

				var leftTime = item.Value.Item1;
				var rightTime = item.Value.Item2;

				//Console.WriteLine(string.Format("[PhotoHelper]###################### try to append location {0} to {1}. Photo Date {2}, left time: {3}, right time: {4}", info.City, info.PhotoMetadata.FilePath, info.PhotoMetadata.TakenDate.ToString("yyyy-MM-dd HH-mm-ss"), leftTime.ToString("yyyy-MM-dd HH-mm-ss"), rightTime.ToString("yyyy-MM-dd HH-mm-ss")));

                if (info.ExifMetadata.TakenDate > leftTime && info.ExifMetadata.TakenDate < rightTime)
				{
					info.City = item.Key;
					info.Country = item.Value.Item3;

                    Console.WriteLine(string.Format("[PhotoHelper]###################### append location {0} to {1}. Photo Date {2}, left time: {3}, right time: {4}", info.City, info.FilePath, info.ExifMetadata.TakenDate.ToString("yyyy-MM-dd HH-mm-ss"), leftTime.ToString("yyyy-MM-dd HH-mm-ss"), rightTime.ToString("yyyy-MM-dd HH-mm-ss")));

					break;
				}
			}
		}
    }
}
