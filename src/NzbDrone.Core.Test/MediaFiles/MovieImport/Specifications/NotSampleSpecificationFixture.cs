using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.MediaFiles.MovieImport.Specifications;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.MovieImport.Specifications
{
    [TestFixture]
    public class NotSampleSpecificationFixture : CoreTest<NotSampleSpecification>
    {
        private Movie _movie;
        private LocalMovie _localEpisode;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                                     .Build();

            _localEpisode = new LocalMovie
            {
                Path = @"C:\Test\30 Rock\30.rock.s01e01.avi",
                Movie = _movie,
            };
        }

        [Test]
        public void should_return_true_for_existing_file()
        {
            _localEpisode.ExistingFile = true;
            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [TestCase(DetectSampleResult.NotSample)]
        [TestCase(DetectSampleResult.Indeterminate)]
        public void should_return_true_for_existing_file_that_is_not_detected_as_sample(DetectSampleResult result)
        {
            _localEpisode.ExistingFile = true;

            Mocker.GetMock<IDetectSample>()
                  .Setup(s => s.IsSample(It.IsAny<MovieMetadata>(), It.IsAny<string>()))
                  .Returns(result);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_for_existing_file_detected_as_sample()
        {
            _localEpisode.ExistingFile = true;

            Mocker.GetMock<IDetectSample>()
                  .Setup(s => s.IsSample(It.IsAny<MovieMetadata>(), It.IsAny<string>()))
                  .Returns(DetectSampleResult.Sample);

            var result = Subject.IsSatisfiedBy(_localEpisode, null);

            result.Accepted.Should().BeFalse();
            result.Reason.Should().Be(ImportRejectionReason.Sample);
        }
    }
}
