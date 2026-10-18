using System.Collections.Generic;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Extras.Metadata
{
    [TestFixture]
    public class MetadataServiceFixture : CoreTest<MetadataService>
    {
        private Movie _movie;
        private MovieFile _movieFile;
        private Mock<IMetadata> _consumer;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                                   .With(s => s.Id = 1)
                                   .With(s => s.Path = @"C:\Test\Movies\The Movie (2011)".AsOsAgnostic())
                                   .Build();

            _movieFile = new MovieFile { Id = 10, MovieId = 1, RelativePath = "The Movie (2011) Bluray-1080p.mkv" };

            _consumer = new Mock<IMetadata>();
            _consumer.Setup(c => c.GetFilenameAfterMove(It.IsAny<Movie>(), It.IsAny<MovieFile>(), It.IsAny<MetadataFile>()))
                     .Returns(System.IO.Path.Combine(_movie.Path, "The Movie (2011) Bluray-1080p.metathumb"));

            Mocker.GetMock<IMetadataFileService>()
                  .Setup(s => s.GetFilesByMovie(_movie.Id))
                  .Returns(new List<MetadataFile>
                  {
                      new MetadataFile
                      {
                          MovieId = 1,
                          MovieFileId = 10,
                          Consumer = _consumer.Object.GetType().Name,
                          Type = MetadataType.MovieImage,
                          RelativePath = "folder.jpg"
                      }
                  });

            Mocker.GetMock<IMetadataFactory>()
                  .Setup(s => s.GetAvailableProviders())
                  .Returns(new List<IMetadata> { _consumer.Object });
        }

        [Test]
        public void should_not_move_files_of_disabled_consumers_after_rename()
        {
            Mocker.GetMock<IMetadataFactory>()
                  .Setup(s => s.Enabled())
                  .Returns(new List<IMetadata>());

            Subject.MoveFilesAfterRename(_movie, new List<MovieFile> { _movieFile });

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_move_files_of_enabled_consumers_after_rename()
        {
            Mocker.GetMock<IMetadataFactory>()
                  .Setup(s => s.Enabled())
                  .Returns(new List<IMetadata> { _consumer.Object });

            Subject.MoveFilesAfterRename(_movie, new List<MovieFile> { _movieFile });

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once());
        }
    }
}
