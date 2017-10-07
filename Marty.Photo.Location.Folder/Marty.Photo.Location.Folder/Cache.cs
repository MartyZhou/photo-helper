using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Marty.Photo.Location.Folder.Common;
using Newtonsoft.Json;

namespace Marty.Photo.Location.Folder
{
    public static class Cache
    {
        const string CITY_LEVEL = "locality";
        const string COUNTRY_LEVEL = "country";
        const string AREA_LEVEL1 = "administrative_area_level_1";
        const string AREA_LEVEL2 = "administrative_area_level_2";
        const string LOCATION_TYPE_GEOMETRIC_CENTER = "GEOMETRIC_CENTER";

        static readonly string API_KEY;
        static readonly string LOCAL_PATH;

        static ConcurrentDictionary<string, AddressDetails> cityLevelAddressComponentCache = new ConcurrentDictionary<string, AddressDetails>();
        static ConcurrentDictionary<string, AddressDetails> areaLevel1AddressComponentCache = new ConcurrentDictionary<string, AddressDetails>();
        static ConcurrentDictionary<string, AddressDetails> areaLevel2AddressComponentCache = new ConcurrentDictionary<string, AddressDetails>();
        static ConcurrentDictionary<string, AddressDetails> countryLevelAddressComponentCache = new ConcurrentDictionary<string, AddressDetails>();

        static Cache()
        {
            var config = GetConfig();
            API_KEY = config.API_KEY;
            LOCAL_PATH = config.Path;
            InitLocalRawAddresses();
        }

        public static ConcurrentDictionary<string, AddressDetails> TestCityLevelCache()
        {
            return cityLevelAddressComponentCache;
        }

        public static ConcurrentDictionary<string, AddressDetails> TestCountryLevelCache()
        {
            return countryLevelAddressComponentCache;
        }

        public static ConcurrentDictionary<string, AddressDetails> TestAreaLevel1Cache()
        {
            return areaLevel1AddressComponentCache;
        }

        public static ConcurrentDictionary<string, AddressDetails> TestAreaLevel2Cache()
        {
            return areaLevel2AddressComponentCache;
        }

        public static async Task<CityLocation> GetCityName(char latRef, double lat, char lonRef, double lon)
        {
            var cityLocation = new CityLocation();

            AddressDetails addressDetailsInCache;
            AddressDetails parentAddressDetailsInCache;
            if (TryGetAddressComponentFromCache(latRef, lat, lonRef, lon, out addressDetailsInCache, out parentAddressDetailsInCache))
            {
                cityLocation = ParseCityNameAndInsertCache(addressDetailsInCache, new AddressResult());
            }
            else
            {
                var address = await LoadRawAddressFromServerAsync(latRef, lat, lonRef, lon).ConfigureAwait(false);

                if (address.IsValid)
                {
                    cityLocation = ParseCityNameAndInsertCache(address.Results[0], address);
                }
                else if (!string.IsNullOrWhiteSpace(parentAddressDetailsInCache.PlaceId))
                {
                    cityLocation = ParseCityNameAndInsertCache(parentAddressDetailsInCache, new AddressResult());
                }
            }

            return cityLocation;
        }

        private static void InitLocalRawAddresses()
        {
            try
            {
                var rawAddressFiles = Directory.EnumerateFiles(LOCAL_PATH, "*.json");

                Console.WriteLine("[LocationCache]################ InitLocalRawAddresses path: " + LOCAL_PATH);

                foreach (var fileName in rawAddressFiles)
                {
                    using (var stream = new FileStream(fileName, FileMode.Open))
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var address = JsonConvert.DeserializeObject<AddressResult>(reader.ReadToEnd());
                            address.IsValid = true;
                            var cityLocation = ParseCityNameAndInsertCache(address.Results[0], address);
                            // Console.WriteLine("test ################ city location " + JsonConvert.SerializeObject(cityLocation));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static CityLocation ParseCityNameAndInsertCache(AddressDetails addressDetails, AddressResult addressResult)
        {
            var cityLocation = new CityLocation();
            var areaLevel = string.Empty;

            for (var i = addressDetails.AddressComponents.Length - 1; i > -1; i--)
            {
                var addressComponent = addressDetails.AddressComponents[i];

                foreach (var addressType in addressComponent.Types)
                {
                    if (addressType == CITY_LEVEL)
                    {
                        cityLocation.City = addressComponent.LongName;
                    }
                    else if (addressType == AREA_LEVEL2)
                    {
                        cityLocation.AreaLevel2 = addressComponent.LongName;
                    }
                    else if (addressType == AREA_LEVEL1)
                    {
                        cityLocation.AreaLevel1 = addressComponent.LongName;
                    }
                    else if (addressType == COUNTRY_LEVEL)
                    {
                        cityLocation.Country = addressComponent.LongName;
                    }
                }


            }

            if (addressResult.IsValid)
            {
                if (string.IsNullOrWhiteSpace(cityLocation.City))
                {
                    if (string.IsNullOrWhiteSpace(cityLocation.AreaLevel2))
                    {
                        if (string.IsNullOrWhiteSpace(cityLocation.AreaLevel1))
                        {
                            if (!string.IsNullOrWhiteSpace(cityLocation.Country))
                            {
                                areaLevel = COUNTRY_LEVEL;
                            }
                        }
                        else
                        {
                            areaLevel = AREA_LEVEL1;
                        }
                    }
                    else
                    {
                        areaLevel = AREA_LEVEL2;
                    }
                }
                else
                {
                    areaLevel = CITY_LEVEL;
                }

                if (!string.IsNullOrWhiteSpace(areaLevel))
                {
                    Console.WriteLine(string.Format("[LocationCache]################ ParseCityNameAndInsertCache. AddressLevel: {0}", areaLevel));
                    InsertAddressComponentToCache(addressResult, areaLevel);
                }
                else
                {
                    Console.WriteLine("[LocationCache]################ ParseCityNameAndInsertCache. AddressLevel is empty");
                }

                if (addressDetails.Geometry.LocationType == LOCATION_TYPE_GEOMETRIC_CENTER)
                {
                    areaLevel = addressDetails.Types[0];
                    InsertAddressComponentToCache(addressResult, areaLevel);
                }
            }

            return cityLocation;
        }

        private static void InsertAddressComponentToCache(AddressResult addressResult, string areaLevel)
        {
            AddressDetails countryArea = new AddressDetails();
            AddressDetails areaLevel1 = new AddressDetails();
            AddressDetails areaLevel2 = new AddressDetails();
            AddressDetails cityArea = new AddressDetails();
            foreach (var addressComponent in addressResult.Results)
            {
                foreach (var addressType in addressComponent.Types)
                {
                    if (addressType == areaLevel)
                    {
                        if (!cityLevelAddressComponentCache.ContainsKey(addressComponent.PlaceId))
                        {
                            cityLevelAddressComponentCache.TryAdd(addressComponent.PlaceId, addressComponent);
                            Console.WriteLine(string.Format("[LocationCache]################ InsertAddressComponentToCache. AddressLevel: {0}, PlaceId: {1}, Address: {2}", areaLevel, addressComponent.PlaceId, addressComponent.FormattedAddress));
                        }

                        break;
                    }
                    else if (addressType == CITY_LEVEL)
                    {
                        cityArea = addressComponent;
                    }
                    else if (addressType == COUNTRY_LEVEL)
                    {
                        countryArea = addressComponent;
                    }
                    else if (addressType == AREA_LEVEL1)
                    {
                        areaLevel1 = addressComponent;
                    }
                    else if (addressType == AREA_LEVEL2)
                    {
                        areaLevel2 = addressComponent;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(countryArea.PlaceId))
            {
                if (!countryLevelAddressComponentCache.ContainsKey(countryArea.PlaceId))
                {
                    countryLevelAddressComponentCache.TryAdd(countryArea.PlaceId, countryArea);
                    Console.WriteLine(string.Format("[LocationCache]################ InsertAddressComponentToCache. AddressLevel: {0}, PlaceId: {1}, Address: {2}", COUNTRY_LEVEL, countryArea.PlaceId, addressResult.Results[0].FormattedAddress));
                }
            }

            if (!string.IsNullOrWhiteSpace(areaLevel1.PlaceId))
            {
                if (!areaLevel1AddressComponentCache.ContainsKey(areaLevel1.PlaceId))
                {
                    areaLevel1AddressComponentCache.TryAdd(areaLevel1.PlaceId, areaLevel1);
                    Console.WriteLine(string.Format("[LocationCache]################ InsertAddressComponentToCache. AddressLevel: {0}, PlaceId: {1}, Address: {2}", AREA_LEVEL1, areaLevel1.PlaceId, addressResult.Results[0].FormattedAddress));
                }
            }

            if (!string.IsNullOrWhiteSpace(areaLevel2.PlaceId))
            {
                if (!areaLevel2AddressComponentCache.ContainsKey(areaLevel2.PlaceId))
                {
                    areaLevel2AddressComponentCache.TryAdd(areaLevel2.PlaceId, areaLevel2);
                    Console.WriteLine(string.Format("[LocationCache]################ InsertAddressComponentToCache. AddressLevel: {0}, PlaceId: {1}, Address: {2}", AREA_LEVEL2, areaLevel2.PlaceId, addressResult.Results[0].FormattedAddress));
                }
            }

            if (!string.IsNullOrWhiteSpace(cityArea.PlaceId))
            {
                if (!cityLevelAddressComponentCache.ContainsKey(cityArea.PlaceId))
                {
                    cityLevelAddressComponentCache.TryAdd(cityArea.PlaceId, cityArea);
                    Console.WriteLine(string.Format("[LocationCache]################ InsertAddressComponentToCache. AddressLevel: {0}, PlaceId: {1}, Address: {2}", CITY_LEVEL, cityArea.PlaceId, addressResult.Results[0].FormattedAddress));
                }
            }
        }

        private static bool TryGetAddressComponentFromCache(char latRef, double absLat, char lonRef, double absLon, out AddressDetails match, out AddressDetails parentMatch)
        {
            var result = false;
            var parentResult = false;
            match = new AddressDetails();
            parentMatch = new AddressDetails();

            var lat = latRef == 'N' ? absLat : -absLat;
            var lon = lonRef == 'E' ? absLon : -absLon;

            result = TryGetAddressComponentFromCache(latRef, lat, lonRef, lon, cityLevelAddressComponentCache, out match);

            if (!result)
            {
                parentResult = TryGetAddressComponentFromCache(latRef, lat, lonRef, lon, areaLevel2AddressComponentCache, out parentMatch);

                if (!parentResult)
                {
                    parentResult = TryGetAddressComponentFromCache(latRef, lat, lonRef, lon, areaLevel1AddressComponentCache, out parentMatch);
                }

                if (!parentResult)
                {
                    parentResult = TryGetAddressComponentFromCache(latRef, lat, lonRef, lon, countryLevelAddressComponentCache, out parentMatch);
                }
            }

            //Console.WriteLine("test ################ TryGetAddressComponentFromCache end " + result);

            return result;
        }

        private static bool TryGetAddressComponentFromCache(char latRef, double lat, char lonRef, double lon, ConcurrentDictionary<string, AddressDetails> cache, out AddressDetails match)
        {
            var result = false;
            match = new AddressDetails();

            foreach (var addressComponent in cache)
            {
                if (addressComponent.Value.Geometry.LocationType == LOCATION_TYPE_GEOMETRIC_CENTER)
                {
                    var location = addressComponent.Value.Geometry.Location;
                    var latTest = lat <= location.Lat + 0.01 && lat >= location.Lat - 0.01;
                    var lonTest = lon <= location.Lng + 0.01 && lon >= location.Lng - 0.01;

                    if (latTest && lonTest)
                    {
                        Console.WriteLine(string.Format("[LocationCache]################ found match by GEOMETRIC_CENTER KEY: {0}, address: {1}", addressComponent.Key, JsonConvert.SerializeObject(addressComponent.Value.FormattedAddress)));

                        match = addressComponent.Value;
                        result = true;
                    }
                }
                else
                {
                    var bounds = addressComponent.Value.Geometry.Bounds;
                    var latTest = lat <= bounds.Norteast.Lat && lat >= bounds.Southwest.Lat;
                    var lonTest = lon <= bounds.Norteast.Lng && lon >= bounds.Southwest.Lng;

                    if (latTest && lonTest)
                    {
                        Console.WriteLine(string.Format("[LocationCache]################ found match KEY: {0}, address: {1}", addressComponent.Key, JsonConvert.SerializeObject(addressComponent.Value.FormattedAddress)));

                        match = addressComponent.Value;
                        result = true;
                    }
                }
            }

            return result;
        }

        private static async Task<AddressResult> LoadRawAddressAsync(char latRef, double lat, char lonRef, double lon)
        {
            return await LoadRawAddressFromCacheAsync(latRef, lat, lonRef, lon).ConfigureAwait(false);
        }

        private static async Task<AddressResult> LoadRawAddressFromServerAsync(char latRef, double absLat, char lonRef, double absLon)
        {
            var result = new AddressResult();
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var lat = latRef == 'N' ? absLat : -absLat;
                    var lon = lonRef == 'E' ? absLon : -absLon;
                    var url = string.Format("https://maps.googleapis.com/maps/api/geocode/json?latlng={0},{1}&key={2}", lat, lon, API_KEY);

                    var response = await httpClient.GetStringAsync(url).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        Console.WriteLine(string.Format("[LocationCache]################ LoadRawAddressFromServerAsync successfully and save to {0}", GenerateFileName(latRef, absLat, lonRef, absLon)));

                        result = JsonConvert.DeserializeObject<AddressResult>(response);
                        result.IsValid = true;

                        File.WriteAllText(GenerateFileName(latRef, absLat, lonRef, absLon), JsonConvert.SerializeObject(result));
                    }
                    else
                    {
                        Console.WriteLine(string.Format("[LocationCache]################ Failed to LoadRawAddressFromServerAsync: {0}", GenerateFileName(latRef, absLat, lonRef, absLon)));

                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("Failed to call google map API: {0}", e.Message));
            }

            return result;
        }

        private static string GenerateFileName(char latRef, double lat, char lonRef, double lon)
        {
            return string.Format("{0}/_{1}_{2}_{3}_{4}.json", LOCAL_PATH, latRef, lat, lonRef, lon);
        }

        private static async Task<AddressResult> LoadRawAddressFromCacheAsync(char latRef, double lat, char lonRef, double lon)
        {
            using (var stream = new FileStream("./test/data/SampleAddressHyderabad.json", FileMode.Open))
            {
                using (var reader = new StreamReader(stream))
                {
                    var addressString = reader.ReadToEnd();
                    var address = JsonConvert.DeserializeObject<AddressResult>(addressString);
                    // Console.WriteLine("test ################ LoadRawAddressFromCacheAsync");

                    var result = await Task.FromResult(address);
                    result.IsValid = true;

                    return result;
                }
            }
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
    }
}
