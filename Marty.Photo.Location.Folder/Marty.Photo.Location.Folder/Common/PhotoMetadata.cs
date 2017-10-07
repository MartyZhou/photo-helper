using Marty.JPG.EXIF.Common;

namespace Marty.Photo.Location.Folder.Common
{
    public class PhotoMetadata
    {
        public ExifMetadata ExifMetadata { get; set; }

        public string City { get; set; }

        public string Country { get; set; }

        public string FilePath { get; set; }

        public string NewPath { get; set; }

        public bool HasLocation { get; set; }
    }
}
