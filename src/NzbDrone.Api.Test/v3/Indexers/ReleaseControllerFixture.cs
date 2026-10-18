using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.History;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Indexers;

namespace NzbDrone.Api.Test.v3.Indexers
{
    [TestFixture]
    public class ReleaseControllerFixture : TestBase<ReleaseController>
    {
        private Movie _movieA;
        private Movie _movieB;
        private ReleaseInfo _release;

        [SetUp]
        public void Setup()
        {
            _movieA = new Movie { Id = 1, QualityProfileId = 1 };
            _movieB = new Movie { Id = 2, QualityProfileId = 1 };

            _release = new ReleaseInfo
            {
                Guid = "guid-1",
                IndexerId = 5,
                Title = "Fear.Street.Trilogy.2021.1080p.WEB-DL"
            };

            Mocker.SetConstant<ICacheManager>(new CacheManager());

            Mocker.GetMock<IQualityProfileService>()
                  .Setup(s => s.GetDefaultProfile(It.IsAny<string>(), It.IsAny<Quality>(), It.IsAny<Quality[]>()))
                  .Returns(new QualityProfile
                  {
                      Items = Quality.DefaultQualityDefinitions
                                     .Select(d => new QualityProfileQualityItem { Quality = d.Quality, Allowed = true })
                                     .ToList()
                  });

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.GetMovie(It.IsAny<int>()))
                  .Returns<int>(id => id == _movieA.Id ? _movieA : _movieB);

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByMovieId(It.IsAny<int>()))
                  .Returns(new List<MovieHistory>());

            Mocker.GetMock<IPrioritizeDownloadDecision>()
                  .Setup(s => s.PrioritizeDecisionsForMovies(It.IsAny<List<DownloadDecision>>()))
                  .Returns<List<DownloadDecision>>(d => d);
        }

        private void GivenSearchResult(int searchedMovieId, Movie parsedMovie)
        {
            var remoteMovie = new RemoteMovie
            {
                Release = _release,
                Movie = parsedMovie,
                ParsedMovieInfo = new ParsedMovieInfo { Quality = new QualityModel(Quality.WEBDL1080p) },
                CustomFormats = new List<CustomFormat>(),
                Languages = new List<Language> { Language.English }
            };

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.MovieSearch(searchedMovieId, true, true))
                  .ReturnsAsync(new List<DownloadDecision> { new DownloadDecision(remoteMovie) });
        }

        private RemoteMovie GrabbedRemoteMovie()
        {
            RemoteMovie grabbed = null;

            Mocker.GetMock<IDownloadService>()
                  .Verify(s => s.DownloadReport(It.IsAny<RemoteMovie>(), It.IsAny<int?>()), Times.Once());

            Mocker.GetMock<IDownloadService>()
                  .Invocations
                  .Where(i => i.Method.Name == nameof(IDownloadService.DownloadReport))
                  .ToList()
                  .ForEach(i => grabbed = (RemoteMovie)i.Arguments[0]);

            return grabbed;
        }

        [Test]
        public async Task should_grab_for_requested_movie_when_the_same_release_was_cached_for_another_movie()
        {
            GivenSearchResult(_movieA.Id, _movieA);
            GivenSearchResult(_movieB.Id, _movieB);

            await Subject.GetReleases(_movieA.Id);
            await Subject.GetReleases(_movieB.Id);

            await Subject.DownloadRelease(new ReleaseResource { IndexerId = _release.IndexerId, Guid = _release.Guid, MovieId = _movieA.Id });

            GrabbedRemoteMovie().Movie.Id.Should().Be(_movieA.Id);
        }

        [Test]
        public async Task should_grab_for_requested_movie_when_release_was_parsed_as_another_movie()
        {
            GivenSearchResult(_movieA.Id, _movieB);

            await Subject.GetReleases(_movieA.Id);

            await Subject.DownloadRelease(new ReleaseResource { IndexerId = _release.IndexerId, Guid = _release.Guid, MovieId = _movieA.Id });

            GrabbedRemoteMovie().Movie.Id.Should().Be(_movieA.Id);
        }

        [Test]
        public async Task should_still_grab_cached_release_when_no_movie_id_is_provided()
        {
            GivenSearchResult(_movieA.Id, _movieA);

            await Subject.GetReleases(_movieA.Id);

            await Subject.DownloadRelease(new ReleaseResource { IndexerId = _release.IndexerId, Guid = _release.Guid });

            GrabbedRemoteMovie().Movie.Id.Should().Be(_movieA.Id);
        }

        private static QualityProfile ProfileScoring(int id, CustomFormat format, int score)
        {
            return new QualityProfile
            {
                Id = id,
                Items = Quality.DefaultQualityDefinitions.Select(d => new QualityProfileQualityItem { Quality = d.Quality, Allowed = true }).ToList(),
                FormatItems = new List<ProfileFormatItem> { new ProfileFormatItem { Format = format, Score = score } }
            };
        }

        [Test]
        public async Task should_recalculate_custom_format_score_for_the_requested_movie()
        {
            var format = new CustomFormat("Preferred") { Id = 7 };
            _movieA.QualityProfile = ProfileScoring(1, format, 100);
            _movieB.QualityProfileId = 2;
            _movieB.QualityProfile = ProfileScoring(2, format, -50);

            Mocker.GetMock<ICustomFormatCalculationService>()
                  .Setup(s => s.ParseCustomFormat(It.IsAny<RemoteMovie>(), It.IsAny<long>()))
                  .Returns(new List<CustomFormat> { format });

            // The release was parsed as movie B and scored against B's profile
            GivenSearchResult(_movieA.Id, _movieB);
            var cached = (await Mocker.GetMock<ISearchForReleases>().Object.MovieSearch(_movieA.Id, true, true)).Single().RemoteMovie;
            cached.CustomFormats = new List<CustomFormat> { format };
            cached.CustomFormatScore = -50;

            await Subject.GetReleases(_movieA.Id);
            await Subject.DownloadRelease(new ReleaseResource { IndexerId = _release.IndexerId, Guid = _release.Guid, MovieId = _movieA.Id });

            var grabbed = GrabbedRemoteMovie();
            grabbed.Movie.Id.Should().Be(_movieA.Id);
            grabbed.CustomFormatScore.Should().Be(100);
            cached.Movie.Id.Should().Be(_movieB.Id);
            cached.CustomFormatScore.Should().Be(-50);
        }
    }
}
