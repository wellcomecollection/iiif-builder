using DlcsWebClient.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Dlcs.RestOperations;

namespace DlcsWebClient.Dlcs
{
    public class Dlcs : IDlcs
    {
        private ILogger<Dlcs> logger;
        private DlcsOptions options;

        public Dlcs(
            ILogger<Dlcs> logger,
            IOptions<DlcsOptions> options)
        {
            this.logger = logger;
            this.options = options.Value;
        }

        public int DefaultSpace => throw new NotImplementedException();

        public int DeleteImages(List<Image> images)
        {
            throw new NotImplementedException();
        }

        public bool DeletePdf(string string1, int number1)
        {
            throw new NotImplementedException();
        }

        public Operation<string, Batch> GetBatch(string batchId)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, long> GetDlcsQueueLevel()
        {
            string key = $"queue-{options.CustomerName}";
            return new Dictionary<string, long> { [key] = options.CustomerId };
        }

        public IEnumerable<ErrorByMetadata> GetErrorsByMetadata()
        {
            throw new NotImplementedException();
        }

        public Page<ErrorByMetadata> GetErrorsByMetadata(int page)
        {
            throw new NotImplementedException();
        }

        public Operation<ImageQuery, HydraImageCollection> GetImages(ImageQuery query, int defaultSpace)
        {
            throw new NotImplementedException();
        }

        public Operation<ImageQuery, HydraImageCollection> GetImages(string nextUri)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Image> GetImagesByDlcsIdentifiers(List<string> identifiers)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Image> GetImagesBySequenceIndex(string identifier, int sequenceIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Image> GetImagesForBNumber(string identfier)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Image> GetImagesForIdentifier(string anyIdentifier)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Image> GetImagesForIssue(string issueIdentfier)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Image> GetImagesForString3(string identfier)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Image> GetImagesForVolume(string volumeIdentifier)
        {
            throw new NotImplementedException();
        }

        public IPdf GetPdfDetails(string string1, int number1)
        {
            throw new NotImplementedException();
        }

        public string GetRoleUri(string accessCondition)
        {
            throw new NotImplementedException();
        }

        public List<Batch> GetTestedImageBatches(List<Batch> imageBatches)
        {
            throw new NotImplementedException();
        }

        public Operation<HydraImageCollection, HydraImageCollection> PatchImages(HydraImageCollection images)
        {
            throw new NotImplementedException();
        }

        public Operation<HydraImageCollection, Batch> RegisterImages(HydraImageCollection images, bool priority = false)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Image> RegisterNewImages(List<Image> images)
        {
            throw new NotImplementedException();
        }
    }
}
