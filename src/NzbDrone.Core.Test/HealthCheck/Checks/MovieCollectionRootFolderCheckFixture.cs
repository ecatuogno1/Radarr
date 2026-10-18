using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Movies.Collections;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class MovieCollectionRootFolderCheckFixture : CoreTest<MovieCollectionRootFolderCheck>
    {
        private List<MovieCollection> _collections;

        [SetUp]
        public void Setup()
        {
            _collections = new List<MovieCollection>();

            Mocker.GetMock<IMovieCollectionService>().Setup(s => s.GetAllCollections()).Returns(() => _collections);
            Mocker.GetMock<IRootFolderService>().Setup(s => s.All()).Returns(new List<RootFolder>());
            Mocker.GetMock<IDiskProvider>().Setup(s => s.FolderExists(It.IsAny<string>())).Returns(false);

            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                  .Returns<string, Dictionary<string, object>>((key, tokens) => key switch
                  {
                      "CountMore" => $"+{tokens["count"]} more",
                      "MovieCollectionRootFolderMissingRootHealthCheckMessage" => $"Missing root folder for movie collection: {tokens["rootFolderInfo"]}",
                      _ => $"Multiple root folders are missing for movie collections: {tokens["rootFoldersInfo"]}"
                  });
        }

        private static string Folder(int index) => $@"C:\Collections{index}".AsOsAgnostic();

        private void GivenCollections(int folderIndex, int count)
        {
            _collections.AddRange(Enumerable.Range(1, count).Select(i => new MovieCollection
            {
                Title = $"Collection {folderIndex}-{i}",
                RootFolderPath = Folder(folderIndex)
            }));
        }

        [Test]
        public void should_be_ok_without_collections()
        {
            Subject.Check().ShouldBeOk();
        }

        [Test]
        public void should_list_every_title_for_a_short_list()
        {
            GivenCollections(1, 3);

            Subject.Check().Message.Should()
                .Be($"Missing root folder for movie collection: {Folder(1)} (Collection 1-1, Collection 1-2, Collection 1-3)");
        }

        [Test]
        public void should_truncate_a_long_list_of_collections_with_a_count()
        {
            GivenCollections(1, 250);

            var message = Subject.Check().Message;

            message.Should().Be($"Missing root folder for movie collection: {Folder(1)} (Collection 1-1, Collection 1-2, Collection 1-3, +247 more)");
            message.Should().NotContain("Collection 1-4");
        }

        [Test]
        public void should_list_folders_compactly_and_truncate_extra_folders()
        {
            for (var folder = 1; folder <= 7; folder++)
            {
                GivenCollections(folder, folder == 1 ? 10 : 1);
            }

            var result = Subject.Check();

            result.ShouldBeError();
            result.Message.Should().StartWith($"Multiple root folders are missing for movie collections: {Folder(1)} (Collection 1-1, Collection 1-2, Collection 1-3, +7 more) | {Folder(2)} (Collection 2-1)");
            result.Message.Should().Contain(Folder(5));
            result.Message.Should().NotContain(Folder(6));
            result.Message.Should().EndWith(" | +2 more");
        }

        [Test]
        public void should_not_fail_for_a_collection_without_a_root_folder()
        {
            _collections.Add(new MovieCollection { Title = "No Folder", RootFolderPath = null });
            _collections.Add(new MovieCollection { Title = "Also No Folder", RootFolderPath = null });

            Subject.Check().ShouldBeError();
        }
    }
}
