using System.Collections.Generic;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Dlcs.RestOperations;

namespace Wellcome.Dds.AssetDomain.Dlcs
{
    public interface IDlcs
    {
        // ============================
        // Queued operations
        // ============================

        /// <summary>
        /// Add images to the queue.
        /// POST /c/queue
        /// Image[]
        /// </summary>
        /// <param name="images"></param>
        /// <param name="priority">add the jobs to the priority queue rather than the default</param>
        /// <returns></returns>
        Operation<HydraImageCollection, Batch> RegisterImages(HydraImageCollection images, bool priority = false);
        Operation<HydraImageCollection, HydraImageCollection> PatchImages(HydraImageCollection images);

        /// <summary>
        /// Query the queue for ingest status
        /// GET /c/queue?q={imageQuery}
        /// </summary>
        /// <param name="query"></param>
        /// <param name="defaultSpace"></param>
        /// <returns></returns>
        Operation<ImageQuery, HydraImageCollection> GetImages(ImageQuery query, int defaultSpace);


        Operation<ImageQuery, HydraImageCollection> GetImages(string nextUri);

        Operation<string, Batch> GetBatch(string batchId); 

        string GetRoleUri(string accessCondition);


        // ============================
        // New ops for reconciliation
        // ============================
        // these methods give us our images back for checking

        // any identifier - delegate to any of the FOUR following by same logic as metsrepository
        IEnumerable<Image> GetImagesForIdentifier(string anyIdentifier);
        // string 3
        IEnumerable<Image> GetImagesForIssue(string issueIdentfier);

        IEnumerable<Image> GetImagesForString3(string identfier);
        // string 2
        IEnumerable<Image> GetImagesForVolume(string volumeIdentifier);
        // string 1
        IEnumerable<Image> GetImagesForBNumber(string identfier);

        IEnumerable<ErrorByMetadata> GetErrorsByMetadata();
        Page<ErrorByMetadata> GetErrorsByMetadata(int page);

        /// <summary>
        /// Used for images in the DLCS without string2 and string3 (volume and issue identifiers)
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="sequenceIndex"></param>
        /// <returns></returns>
        IEnumerable<Image> GetImagesBySequenceIndex(string identifier, int sequenceIndex);

        IEnumerable<Image> GetImagesByDlcsIdentifiers(List<string> identifiers);
        

        // If you have a lot of images to register, in batches, call RegisterImages above.
        // and this is for new Tizer ingests - where the DLCS does not have these images yet.
        // This is immediate, for a small number of images.
        IEnumerable<Image> RegisterNewImages(List<Image> images);

        // You can only call this if you are sure that none of the images are in use anywhere.
        // This means scoped to b nnumber in the event of a reorganisation of volumes.
        // That is, don't delete an image until you are sure it is not in use across the entire b number.
        // Example - GetImagesForBNumber from DLCS, get all images for METS from MetsRepo.
        // Any in the first that are not in the second can be deleted safely.
        // This probably can only run as a background job, unless it's a single b number. But for a single
        // b number, this kind of misalignment is unlikely.
        int DeleteImages(List<Image> images);

        /// <summary>
        /// TODO: This MUST be changed to use string3 as soon as the manifest has that info to emit into the rendering
        /// </summary>
        IPdf GetPdfDetails(string string1, int number1);
        bool DeletePdf(string string1, int number1);
        int DefaultSpace { get; }
        int BatchSize { get; }
        bool PreventSynchronisation { get; }
        List<Batch> GetTestedImageBatches(List<Batch> imageBatches);
        Dictionary<string, long> GetDlcsQueueLevel();
    }
}
