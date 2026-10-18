using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.MovieImport.Specifications
{
    public class NotSampleSpecification : IImportDecisionEngineSpecification
    {
        private readonly IDetectSample _detectSample;
        private readonly Logger _logger;

        public NotSampleSpecification(IDetectSample detectSample,
                                      Logger logger)
        {
            _detectSample = detectSample;
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            var sample = _detectSample.IsSample(localMovie.Movie.MovieMetadata, localMovie.Path);

            if (localMovie.ExistingFile)
            {
                // Files already in the movie folder (disk rescan) are not rejected when the sample check is
                // inconclusive, but a file positively detected as a sample must never be assigned as the movie.
                if (sample == DetectSampleResult.Sample)
                {
                    _logger.Debug("Existing file {0} is a sample, rejecting", localMovie.Path);
                    return ImportSpecDecision.Reject(ImportRejectionReason.Sample, "Sample");
                }

                _logger.Debug("Existing file, not a sample");
                return ImportSpecDecision.Accept();
            }

            if (sample == DetectSampleResult.Sample)
            {
                return ImportSpecDecision.Reject(ImportRejectionReason.Sample, "Sample");
            }
            else if (sample == DetectSampleResult.Indeterminate)
            {
                return ImportSpecDecision.Reject(ImportRejectionReason.SampleIndeterminate, "Unable to determine if file is a sample");
            }

            return ImportSpecDecision.Accept();
        }
    }
}
