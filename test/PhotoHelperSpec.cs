using System.IO;
using Cluj.Exif;
using Xunit;

namespace Cluj.PhotoHelper.UnitTest
{
    public class PhotoHelperSpec
    {
        [Fact]
        public void UseExifMetadataReader4_metadata()
        {
            using (FileStream stream = new FileStream("./test/p1_exif_header.jpg", FileMode.Open))
            {
                var reader = new PhotoMetadataReader(stream);
                var meta = reader.ParseMetadata();

                Assert.Equal<string>("NIKON CORPORATION", meta.Make);
                Assert.Equal<string>("NIKON D40", meta.Model);
                Assert.Equal<char>('N', meta.GPS.LatRef);
                Assert.Equal<char>('E', meta.GPS.LonRef);
            }
        }
    }
}