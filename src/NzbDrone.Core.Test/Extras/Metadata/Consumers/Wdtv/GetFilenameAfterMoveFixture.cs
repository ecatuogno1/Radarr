using System.IO;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Extras.Metadata.Consumers.Wdtv;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Extras.Metadata.Consumers.Wdtv
{
    [TestFixture]
    public class GetFilenameAfterMoveFixture : CoreTest<WdtvMetadata>
    {
        private Movie _movie;
        private MovieFile _movieFile;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                                   .With(s => s.Path = @"C:\Test\Movies\The Movie (2011)".AsOsAgnostic())
                                   .Build();

            _movieFile = new MovieFile { RelativePath = "The Movie (2011) Bluray-1080p.mkv" };
        }

        [Test]
        public void should_not_rename_folder_jpg_to_metathumb()
        {
            var metadataFile = new MetadataFile { Type = MetadataType.MovieImage, RelativePath = "folder.jpg" };

            Subject.GetFilenameAfterMove(_movie, _movieFile, metadataFile)
                   .Should().Be(Path.Combine(_movie.Path, "folder.jpg"));
        }

        [Test]
        public void should_rename_metathumb_to_match_movie_file()
        {
            var metadataFile = new MetadataFile { Type = MetadataType.MovieImage, RelativePath = "the.movie.2011.metathumb" };

            Subject.GetFilenameAfterMove(_movie, _movieFile, metadataFile)
                   .Should().Be(Path.Combine(_movie.Path, "The Movie (2011) Bluray-1080p.metathumb"));
        }
    }
}
