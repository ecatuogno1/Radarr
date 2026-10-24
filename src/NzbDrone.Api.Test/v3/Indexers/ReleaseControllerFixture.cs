using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.History;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Indexers;

namespace NzbDrone.Api.Test.v3.Indexers
{
    [TestFixture]
    public class ReleaseControllerFixture : TestBase<ReleaseController>
    {
        private Movie _movie;
        private ReleaseInfo _release;

        [SetUp]
        public void Setup()
        {
            _movie = new Movie { Id = 1, QualityProfileId = 1 };

            _release = new ReleaseInfo
            {
                Guid = "guid-1",
                IndexerId = 5,
                Title = "Movie.Title.2021.1080p.WEB-DL"
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

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByMovieId(It.IsAny<int>()))
                  .Returns(new List<MovieHistory>());

            Mocker.GetMock<IPrioritizeDownloadDecision>()
                  .Setup(s => s.PrioritizeDecisionsForMovies(It.IsAny<List<DownloadDecision>>()))
                  .Returns<List<DownloadDecision>>(d => d);

            var remoteMovie = new RemoteMovie
            {
                Release = _release,
                Movie = _movie,
                ParsedMovieInfo = new ParsedMovieInfo { Quality = new QualityModel(Quality.WEBDL1080p) },
                CustomFormats = new List<CustomFormat>(),
                Languages = new List<Language> { Language.English }
            };

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.MovieSearch(_movie.Id, true, true))
                  .ReturnsAsync(new List<DownloadDecision> { new DownloadDecision(remoteMovie) });

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.MovieSearch(It.Is<int>(id => id != _movie.Id), true, true))
                  .Returns<int, bool, bool>((id, _, _) => throw new ModelNotFoundException(typeof(Movie), id));
        }

        private static async Task<HttpStatusCode> StatusOf(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (NzbDroneClientException ex)
            {
                return ex.StatusCode;
            }

            return HttpStatusCode.OK;
        }

        [Test]
        public async Task should_return_not_found_when_searching_for_a_movie_that_does_not_exist()
        {
            (await StatusOf(() => Subject.GetReleases(99))).Should().Be(HttpStatusCode.NotFound);
        }

        [Test]
        public async Task should_return_bad_request_when_overriding_without_a_movie()
        {
            await Subject.GetReleases(_movie.Id);

            var resource = new ReleaseResource
            {
                IndexerId = _release.IndexerId,
                Guid = _release.Guid,
                ShouldOverride = true,
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<Language> { Language.English }
            };

            (await StatusOf(() => Subject.DownloadRelease(resource))).Should().Be(HttpStatusCode.BadRequest);

            Mocker.GetMock<IDownloadService>()
                  .Verify(s => s.DownloadReport(It.IsAny<RemoteMovie>(), It.IsAny<int?>()), Times.Never());
        }

        [Test]
        public async Task should_return_bad_request_when_overriding_without_a_quality()
        {
            await Subject.GetReleases(_movie.Id);

            var resource = new ReleaseResource
            {
                IndexerId = _release.IndexerId,
                Guid = _release.Guid,
                MovieId = _movie.Id,
                ShouldOverride = true,
                Languages = new List<Language> { Language.English }
            };

            (await StatusOf(() => Subject.DownloadRelease(resource))).Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
