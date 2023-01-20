using DlcsWebClient.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Utils.Logging;
using Wellcome.Dds.AssetDomain;
using Wellcome.Dds.AssetDomain.DigitalObjects;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Dlcs.RestOperations;
using Wellcome.Dds.Common;

namespace DlcsWebClient.Dlcs
{
    public class Dlcs : IDlcs
    {
        private readonly ILogger<Dlcs> logger;
        private readonly HttpClient httpClient;
        private readonly DlcsOptions options;
        private readonly JsonSerializerSettings jsonSerializerSettings;

        public Dlcs(
            ILogger<Dlcs> logger,
            IOptions<DlcsOptions> options,
            HttpClient httpClient)
        {
            this.logger = logger;
            this.httpClient = httpClient;
            this.options = options.Value;
            
            jsonSerializerSettings = new JsonSerializerSettings
            {
                // http://stackoverflow.com/questions/23170918/is-the-jsonserializersettings-thread-safe
                // TODO - is this (and its CamelCasePropertyNamesContractResolver) thread safe?
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        private async Task<Operation<TRequest, TResponse>> DoOperation<TRequest, TResponse>(
            HttpMethod httpMethod, Uri uri,
            TRequest? requestObject,
            DlcsCallContext dlcsCallContext) where TResponse : JSONLDBase
        {
            Operation<TRequest, TResponse> operation = new Operation<TRequest, TResponse>(uri, httpMethod);
            HttpResponseMessage? response = null;
            HttpRequestMessage requestMessage = new HttpRequestMessage(operation.HttpMethod, operation.Uri);
            var correlationId = Guid.NewGuid();
            requestMessage.Headers.Add("x-correlation-id", correlationId.ToString());
            requestMessage.Headers.Add("x-dds-call-context-id", dlcsCallContext.Id.ToString());
            if (dlcsCallContext.SyncOperationId.HasValue)
            {
                requestMessage.Headers.Add("x-dds-sync-id", dlcsCallContext.SyncOperationId.ToString());
            }
            if (dlcsCallContext.JobId.HasValue)
            {
                requestMessage.Headers.Add("x-dds-job-id", dlcsCallContext.JobId.ToString());
            }

            try
            {
                if (requestObject != null)
                {
                    operation.RequestObject = requestObject;
                    operation.RequestJson = JsonConvert.SerializeObject(
                        operation.RequestObject, Formatting.Indented, jsonSerializerSettings);
                }
                
                switch (operation.HttpMethod.Method)
                {
                    case "POST":
                        requestMessage.Content = GetJsonContent(operation.RequestJson!);
                        break;
                    case "PATCH":
                        requestMessage.Content = GetJsonContent(operation.RequestJson!);
                        break;
                    case "GET":
                        var uriBuilder = new UriBuilder(operation.Uri);
                        if (requestObject != null)
                        {
                            uriBuilder.Query = $"?q={operation.RequestJson}";
                        }
                        requestMessage.RequestUri = uriBuilder.Uri;
                        break;
                    case "PUT":
                        throw new NotImplementedException("PUT - do this with HttpClient");
                    case "DELETE":
                        logger.LogInformation("Operation is a DELETE of {uri}", requestMessage.RequestUri!.AbsoluteUri);
                        break;
                    default:
                        throw new NotImplementedException("Unknown HTTP Method " + operation.HttpMethod.Method);
                }

                dlcsCallContext.AddCall(requestMessage.Method.Method, requestMessage.RequestUri!.PathAndQuery, correlationId);
                logger.LogDebug("About to {httpMethod} to {uri} with correlationId {correlationId}",
                    requestMessage.Method.Method, requestMessage.RequestUri!.AbsoluteUri, correlationId);
                response = await httpClient.SendAsync(requestMessage);
                operation.ResponseStatus = response.StatusCode;
                operation.ResponseJson = await response.Content.ReadAsStringAsync();

                logger.LogDebug("Response received for correlationId {correlationId}, callContext {callContext}",
                    correlationId, dlcsCallContext);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var responseObject = JsonConvert.DeserializeObject<TResponse>(operation.ResponseJson);
                        if (responseObject != null)
                        {
                            if (responseObject is JSONLDBase jsonLdBase)
                            {
                                operation.ResponseObject = responseObject;
                                logger.LogDebug("Deserialized a {responseType} for {callContext}", 
                                    jsonLdBase.Type ?? "[no type]", dlcsCallContext.Id);
                            }
                            else
                            {
                                operation.Error = GetError(response, "DLCS response is not a JSONLD object");
                                logger.LogError("DLCS response is not a JSONLD object");
                            }
                        }
                    }
                    catch (Exception deserializeEx)
                    {
                        operation.Error = GetError(deserializeEx, response);
                    }
                }
                else
                {
                    // non 2xx status codes:
                    try
                    {
                        // Look for a Protagonist error - but don't rely on one!
                        // See comment in this Error class - this is a temporary DLCS-migration approach.
                        var hydraError = JsonConvert.DeserializeObject<Wellcome.Dds.AssetDomain.Dlcs.Model.Protagonist.Error>(operation.ResponseJson);
                        if (hydraError?.Type == "Error")
                        {
                            operation.Error = GetError(response, $"{hydraError.Title}: {hydraError.Detail}");
                        }
                        else
                        {
                            operation.Error = GetError(response, "Unknown DLCS error: " + operation.ResponseJson);
                        }
                    }
                    catch (Exception e)
                    {
                        operation.Error = GetError(e, response);
                    }
                }
            }
            catch (Exception ex)
            {
                operation.Error = GetError(ex, response);
            }

            if (operation.Error != null)
            {
                logger.LogError(operation.Error.Exception, 
                    "Error in dlcs request {collationId} - {message}, callContext {callContext}", 
                    correlationId, operation.Error.Message, dlcsCallContext);
            }
            
            return operation;
        }

        private static StringContent GetJsonContent(string json) => new(json, Encoding.UTF8, "application/json");
        
        private static readonly char[] SlashSeparator = new[] { '/' };
        
        // TODO: Is this required still?
        private string GetLocalStorageIdentifier(string id)
        {
            // This used to rely on the fact that all Preservica / Wellcome IDs
            // had a GUID as their last component
            var last = id.Split(SlashSeparator).Last();
            return last;
        }
        
        public Task<Operation<ImageQuery, HydraImageCollection>> GetImages(
            ImageQuery query, int defaultSpace, DlcsCallContext dlcsCallContext)
        {
            int space = defaultSpace;
            if (query.Space.HasValue) space = query.Space.Value;
            var imageQueryUri = $"{options.ApiEntryPoint}customers/{options.CustomerId}/spaces/{space}/images";
            return DoOperation<ImageQuery, HydraImageCollection>(
                HttpMethod.Get, new Uri(imageQueryUri), query, dlcsCallContext);
        }

        public Task<Operation<ImageQuery, HydraImageCollection>> GetImages(
            string nextUri, DlcsCallContext dlcsCallContext) 
            => DoOperation<ImageQuery, HydraImageCollection>( HttpMethod.Get, new Uri(nextUri), null, dlcsCallContext);

        private Task<Operation<string, HydraErrorByMetadataCollection>> GetErrorsByMetadata(string uri, DlcsCallContext dlcsCallContext) 
            => DoOperation<string, HydraErrorByMetadataCollection>(HttpMethod.Get, new Uri(uri), null, dlcsCallContext);

        public Task<Operation<string, Batch>> GetBatch(string batchId, DlcsCallContext dlcsCallContext)
        {
            const string batchTemplate = "{0}customers/{1}/queue/batches/{2}";
            if (!Uri.IsWellFormedUriString(batchId, UriKind.Absolute))
            {
                batchId = string.Format(batchTemplate, options.ApiEntryPoint, options.CustomerId, batchId);
            }

            return DoOperation<string, Batch>(HttpMethod.Get, new Uri(batchId), null, dlcsCallContext);
        }

        private static Error GetError(HttpResponseMessage response, string message)
        {
            return new Error
            (
                status: (int)response.StatusCode,
                message: message
            );
        }

        private static Error GetError(Exception ex, HttpResponseMessage? response)
        {
            if (ex is HttpRequestException httpRequestException)
            {
                return new Error
                (
                    status: (int?)response?.StatusCode ?? 0,
                    message: httpRequestException.Message
                )
                {
                    Exception = ex
                };
            }
            
            return new Error
            (
                status: 0,
                message: ex.Message
            )
            {
                Exception = ex
            };
        }

        private static Uri? _imageQueueUri;
        private void InitQueue()
        {
            if (_imageQueueUri == null)
            {
                // TODO: At this point we would work out RESTfully where the queue for this customer is
                // and cache that for a bit - we assume the API stays reasonably stable.
                _imageQueueUri = new Uri($"{options.ApiEntryPoint}customers/{options.CustomerId}/queue");
            }
        }

        public Task<Operation<HydraImageCollection, Batch>> RegisterImages(
            HydraImageCollection images, DlcsCallContext dlcsCallContext, bool priority = false)
        {
            InitQueue();
            var queueUri = priority ? new Uri($"{_imageQueueUri}/priority") : _imageQueueUri;
            return DoOperation<HydraImageCollection, Batch>(HttpMethod.Post, queueUri!, images, dlcsCallContext);
        }

        public Task<Operation<HydraImageCollection, HydraImageCollection>> PatchImages(
            HydraImageCollection images, DlcsCallContext dlcsCallContext)
        {
            string uri =
                $"{options.ApiEntryPoint}customers/{options.CustomerId}/spaces/{options.CustomerDefaultSpace}/images";
            foreach (var image in images.Members!)
            {
                // For protagonist, take another look at use of ModelId here.
                if (image.ModelId!.IndexOf("/", StringComparison.Ordinal) == -1)
                {
                    image.ModelId = $"{options.CustomerId}/{options.CustomerDefaultSpace}/{image.ModelId}";
                }
            }

            return DoOperation<HydraImageCollection, HydraImageCollection>(
                HttpMethod.Patch, new Uri(uri), images, dlcsCallContext);
        }

        public string GetRoleUri(string accessCondition)
        {
            // https://api.dlcs.io/customers/1/roles/requiresRegistration
            string firstCharLowered = accessCondition.Trim()[0].ToString().ToLowerInvariant() + accessCondition.Substring(1);
            if (firstCharLowered == "requires registration" || firstCharLowered == "open with advisory")
            {
                firstCharLowered = "clickthrough";
            }
            return $"{options.ApiEntryPoint}customers/{options.CustomerId}/roles/{ToCamelCase(firstCharLowered)}";
        }

        // TODO - leave this here? Move to utils and introduce dependency?
        /// <summary>
        /// converts "Some list of strings" to "someListOfStrings"
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ToCamelCase(string s)
        {
            var sb = new StringBuilder();
            bool previousWasSpace = false;
            foreach (char c in s.Trim())
            {
                if (Char.IsLetterOrDigit(c))
                {
                    sb.Append(previousWasSpace ? Char.ToUpperInvariant(c) : c);
                }
                previousWasSpace = Char.IsWhiteSpace(c);
            }
            return sb.ToString();
        }

        public Task<IEnumerable<Image>> GetImagesForIdentifier(string identifier, DlcsCallContext dlcsCallContext)
        {
            var ddsId = new DdsIdentifier(identifier);
            if(ddsId.IdentifierType == IdentifierType.BNumberAndSequenceIndex)
            {
                throw new NotSupportedException("No more sequence index");
            }
            return ddsId.IdentifierType switch
            {
                IdentifierType.BNumber => GetImagesForBNumber(identifier, dlcsCallContext),
                IdentifierType.Volume => GetImagesForVolume(identifier, dlcsCallContext),
                IdentifierType.BNumberAndSequenceIndex => GetImagesBySequenceIndex(ddsId.BNumber!, ddsId.SequenceIndex, dlcsCallContext),
                IdentifierType.Issue => GetImagesForIssue(identifier, dlcsCallContext),
                // TODO - Archival
                IdentifierType.NonBNumber => throw new NotSupportedException("Unknown identifier"),
                _ => throw new NotSupportedException("Unknown identifier")
            };
        }

        /// <summary>
        /// Implementation NOTE
        /// These must return IEnumerables that don't get enumerated until aske for.
        /// otherwise we have way too many queries hitting the DLCS.
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="dlcsCallContext"></param>
        /// <returns></returns>
        // string 1
        public Task<IEnumerable<Image>> GetImagesForBNumber(string identifier, DlcsCallContext dlcsCallContext) 
            => GetImagesFromQuery(new ImageQuery { String1 = identifier }, dlcsCallContext);

        // string 1, number 1
        public Task<IEnumerable<Image>> GetImagesBySequenceIndex(string identifier, int sequenceIndex, DlcsCallContext dlcsCallContext) 
            => GetImagesFromQuery(new ImageQuery { String1 = identifier, Number1 = sequenceIndex }, dlcsCallContext);

        // string 2
        public Task<IEnumerable<Image>> GetImagesForVolume(string volumeIdentifier, DlcsCallContext dlcsCallContext) 
            => GetImagesFromQuery(new ImageQuery { String2 = volumeIdentifier }, dlcsCallContext);

        // string 3
        public Task<IEnumerable<Image>> GetImagesForIssue(string issueIdentifier, DlcsCallContext dlcsCallContext) 
            => GetImagesFromQuery(new ImageQuery { String3 = issueIdentifier }, dlcsCallContext);

        public Task<IEnumerable<Image>> GetImagesForString3(string identifier, DlcsCallContext dlcsCallContext) 
            => GetImagesForIssue(identifier, dlcsCallContext);

        public async Task<IEnumerable<Image>> GetImagesByDlcsIdentifiers(List<string> identifiers, DlcsCallContext dlcsCallContext)
        {
            // POST a list of identifiers to /allImages, which will return images by Id
            var uri = $"{options.ApiEntryPoint}customers/{options.CustomerId}/allImages";
            var fullIds = identifiers.Select(g => $"{options.CustomerId}/{options.CustomerDefaultSpace}/{g}");
            var request = new HydraStringIdCollection(fullIds);
            var op = await DoOperation<HydraStringIdCollection, HydraImageCollection>(
                HttpMethod.Post, new Uri(uri), request, dlcsCallContext);
            
            foreach (var image in op.ResponseObject!.Members!)
            {
                image.StorageIdentifier = GetLocalStorageIdentifier(image.Id!);
            }

            return op.ResponseObject.Members;
        }

        // immediate mode
        public IEnumerable<Image> RegisterNewImages(List<Image> images, DlcsCallContext dlcsCallContext)
        {
            throw new NotImplementedException();
        }

        public async Task<int> DeleteImages(List<Image> images, DlcsCallContext dlcsCallContext)
        {
            const string uriTemplate = "{0}customers/{1}/deleteImages";
            string uri = string.Format(uriTemplate, options.ApiEntryPoint, options.CustomerId);
            const string template = "{0}/{1}/{2}";
            var fullIds = images.Select(
                    im => string.Format(template, options.CustomerId, options.CustomerDefaultSpace, im.StorageIdentifier));
            var request = new HydraStringIdCollection(fullIds);
            var op = await DoOperation<HydraStringIdCollection, MessageObject>(
                HttpMethod.Post, new Uri(uri), request, dlcsCallContext);
            var message = op.ResponseObject == null ? "NO RESPONSE OBJECT" : op.ResponseObject.Message;
            logger.LogInformation("Attempted to delete {0} images, response was {1}", images.Count, message);
            return images.Count;
        }

        /// <summary>
        /// Get details of PDF from PDF-control file
        /// </summary>
        public async Task<IPdf?> GetPdfDetails(string identifier)
        {
            // e.g., pdf-control/wellcome/pdf/5/b12345678_0004
            var uri =
                $"{options.InternalResourceEntryPoint}pdf-control/{options.CustomerName}/{options.PdfQueryName}/{options.CustomerDefaultSpace}/{identifier}";

            var response = await httpClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                return default;
            }

            var pdf = await response.Content.ReadAsAsync<Pdf>();
            if (string.IsNullOrEmpty(pdf.Url))
            {
                pdf.Url = uri.Replace("pdf-control", "pdf");
            }

            return pdf;
        }

        /// <summary>
        /// Delete PDF for specified identifier.
        /// </summary>
        public async Task<bool> DeletePdf(string identifier)
        {
            // e.g., customers/2/resources/pdf/pdf?args=5/b12345678_0004
            var uri =
                $"{options.ApiEntryPoint}customers/{options.CustomerId}/resources/pdf/{options.PdfQueryName}?args={options.CustomerDefaultSpace}/{identifier}";

            var response = await httpClient.DeleteAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Error deleting PDF for {identifier}. StatusCode: {statusCode}", identifier,
                    response.StatusCode);
                return false;
            }

            dynamic result = JObject.Parse(await response.Content.ReadAsStringAsync());
            return (bool) result.success;
        }

        /// <summary>
        /// Return tested versions of the batches
        /// </summary>
        /// <param name="imageBatches"></param>
        /// <param name="dlcsCallContext"></param>
        /// <returns></returns>
        public async Task<List<Batch>> GetTestedImageBatches(List<Batch> imageBatches, DlcsCallContext dlcsCallContext)
        {
            const string template = "{0}customers/{1}/queue/batches/{2}/test";
            var testedBatches = new List<Batch>();
            foreach (var imageBatch in imageBatches)
            {
                var batchLocalId = imageBatch.Id!.Split('/').Last();
                var uri = string.Format(template, options.ApiEntryPoint, options.CustomerId, batchLocalId);
                
                var request = new HttpRequestMessage(HttpMethod.Post, uri);
                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    testedBatches.Add(imageBatch);
                }
                
                dynamic result = JObject.Parse(await response.Content.ReadAsStringAsync());
                if ((bool)result.success)
                {
                    var batch = await GetBatch(imageBatch.Id, dlcsCallContext);
                    testedBatches.Add(batch.ResponseObject!);
                }
                else
                {
                    testedBatches.Add(imageBatch);
                }
            }
            return testedBatches;
        }


        public async Task<IEnumerable<ErrorByMetadata>> GetErrorsByMetadata(DlcsCallContext dlcsCallContext)
        {
            const string template = "{0}customers/{1}/queue/recentErrorsByMetadata/string3";
            bool first = true;
            string? nextUri = null;

            var allErrors = new List<ErrorByMetadata>();

            while (first || nextUri != null)
            {
                Operation<string, HydraErrorByMetadataCollection> statesOperation;
                if (first)
                {
                    logger.LogInformation("On the first call, pass base URL");
                    var initialUri = string.Format(template, options.ApiEntryPoint, options.CustomerId);
                    statesOperation = await GetErrorsByMetadata(initialUri, dlcsCallContext);
                    
                    first = false;
                }
                else
                {
                    logger.LogInformation("Following link to next page {0}", nextUri);
                    statesOperation = await GetErrorsByMetadata(nextUri!, dlcsCallContext);
                }
                string dataMessage;
                if (statesOperation.Error != null)
                {
                    dataMessage = "DLCS error:" + statesOperation.Error;
                    logger.LogError(dataMessage);
                    throw new ApplicationException(dataMessage);
                }
                var errorByMetadataCollection = statesOperation.ResponseObject;
                if (errorByMetadataCollection == null)
                {
                    dataMessage = "DLCS error: No data in response, aborting";
                    throw new ApplicationException(dataMessage);
                }
                if (errorByMetadataCollection.Members != null)
                {
                    logger.LogInformation("Adding {0} collection members to returned image list", errorByMetadataCollection.Members.Length);
                    allErrors.AddRange(errorByMetadataCollection.Members);
                }
                if (errorByMetadataCollection.View != null && errorByMetadataCollection.View.Next != null)
                {
                    logger.LogInformation("This collection has a view with a next page");
                    nextUri = errorByMetadataCollection.View.Next;
                }
                else
                {
                    logger.LogInformation("There is no next page in this collection");
                    nextUri = null;
                }
            }

            return allErrors;
        }

        /// <summary>
        ///  TODO - generic method, generic collection
        /// </summary>
        /// <returns></returns>
        public async Task<Page<ErrorByMetadata>> GetErrorsByMetadata(int page, DlcsCallContext dlcsCallContext)
        {
            // Here's where we take a RESTful pause to consider Hydra. We're about to sidestep REST 
            // by requesting a result page directly.
            // We could stick to one known URI by requesting the initial page, and then inspecting an array
            // of pages in the response to get the one we want. Or we could have the concept of a templated
            // page link - but that is not very RESTful either.

            // So this code is going to KNOW that it can put page on the end, and is going to assume that the
            // initial navigation is constructed from a request to the first page (to get the totals and pageSize).


            if (page < 1) page = 1;
            var pageObj = new Page<ErrorByMetadata> { PageNumber = page };
            const string template = "{0}customers/{1}/queue/recentErrorsByMetadata/string3?page={2}";

            logger.LogInformation("On the first call, pass base URL");
            var initialUri = string.Format(template, options.ApiEntryPoint, options.CustomerId, page);
            var statesOperation = await GetErrorsByMetadata(initialUri, dlcsCallContext);
            string dataMessage;
            if (statesOperation.Error != null)
            {
                dataMessage = "DLCS error:" + statesOperation.Error;
                logger.LogError(dataMessage);
                throw new ApplicationException(dataMessage);
            }
            var errorByMetadataCollection = statesOperation.ResponseObject;
            if (errorByMetadataCollection == null)
            {
                dataMessage = "DLCS error: No data in response, aborting";
                throw new ApplicationException(dataMessage);
            }
            pageObj.TotalItems = errorByMetadataCollection.TotalItems ?? 0;
            if (pageObj.TotalItems > 0 && errorByMetadataCollection.PageSize.HasValue)
            {
                pageObj.TotalPages = (pageObj.TotalItems - 1) / errorByMetadataCollection.PageSize.Value + 1;
            }
            if (errorByMetadataCollection.Members != null)
            {
                logger.LogInformation("Found {0} collection members in image list", errorByMetadataCollection.Members.Length);
                pageObj.Items = errorByMetadataCollection.Members;
            }
            
            return pageObj;
        }

        /// <summary>
        /// sets the two new strings data and response 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="dlcsCallContext"></param>
        /// <returns></returns>
        private async Task<IEnumerable<Image>> GetImagesFromQuery(ImageQuery query, DlcsCallContext dlcsCallContext)
        {
            bool first = true;
            string? nextUri = null;

            var images = new List<Image>();
            
            var debug = logger.IsEnabled(LogLevel.Debug);
            BatchMetrics? batchMetrics = debug ? new BatchMetrics() : null;
            
            while (first || nextUri != null)
            {
                if (debug)
                {
                    batchMetrics!.BeginBatch();
                }
                Operation<ImageQuery, HydraImageCollection> statesOperation;
                if (first)
                {
                    logger.LogInformation("On the first call, pass the query object");
                    statesOperation = await GetImages(query, options.CustomerDefaultSpace, dlcsCallContext);
                    first = false;
                }
                else
                {
                    logger.LogInformation("Following link to next page {0}", nextUri);
                    statesOperation = await GetImages(nextUri!, dlcsCallContext);
                }
                string dataMessage;
                if (statesOperation.Error != null)
                {
                    dataMessage = "DLCS error:" + statesOperation.Error;
                    logger.LogError(dataMessage);
                    throw new ApplicationException(dataMessage);
                }
                var imageCollection = statesOperation.ResponseObject;
                if (imageCollection == null)
                {
                    dataMessage = "DLCS error: No data in response, aborting";
                    throw new ApplicationException(dataMessage);
                }
                if (imageCollection.Members != null)
                {
                    logger.LogInformation("Yielding {0} collection members to returned image list", imageCollection.Members.Length);
                    foreach (var image in imageCollection.Members)
                    {
                        image.StorageIdentifier = GetLocalStorageIdentifier(image.Id!);
                        if (image.Family == 'T')
                        {
                            await LoadMetadata(image);
                        }

                        images.Add(image);
                    }
                }
                if (imageCollection.View != null && imageCollection.View.Next != null)
                {
                    logger.LogInformation("This collection has a view with a next page");
                    nextUri = imageCollection.View.Next;
                }
                else
                {
                    logger.LogInformation("There is no next page in this collection");
                    nextUri = null;
                }

                if (debug)
                {
                    batchMetrics!.EndBatch(imageCollection.Members?.Length ?? -1);
                }
            }

            if (debug)
            {
                logger.LogDebug("Timings for DLCS::GetImagesFromQuery");
                logger.LogDebug(batchMetrics!.Summary);
            }
            return images;
        }


        private async Task LoadMetadata(Image image)
        {
            try
            {
                httpClient.Timeout = TimeSpan.FromMilliseconds(2000);
                image.Metadata = await httpClient.GetStringAsync(image.Metadata);
            }
            catch (Exception wcEx)
            {
                image.Metadata = $"[could not retrieve metadata from {image.Metadata}: {wcEx.Message}]";
            }
        }

        public int DefaultSpace => options.CustomerDefaultSpace;
        public int BatchSize => options.BatchSize;

        public bool PreventSynchronisation => options.PreventSynchronisation;

        public async Task<Dictionary<string, long>> GetDlcsQueueLevel()
        {
            var url = $"{options.ApiEntryPoint}queue";
            // httpClient.Timeout = TimeSpan.FromMilliseconds(4000);
            var response = await httpClient.GetStringAsync(url);

            var result = JObject.Parse(response);

            return new Dictionary<string, long>
            {
                {"incoming", result["incoming"]!.Value<long>()},
                {"priority", result["priority"]!.Value<long>()}
            };
        }


        public List<AVDerivative> GetAVDerivatives(Image dlcsAsset)
        {
            // This knows that we have webm, mp4 and mp3... it shouldn't know this, it should learn it.
            const string AVDerivativeTemplateVideo = "{0}iiif-av/{1}/{2}/{3}/full/full/max/max/0/default.{4}";
            const string AVDerivativeTemplateAudio = "{0}iiif-av/{1}/{2}/{3}/full/max/default.{4}";

            var derivs = new List<AVDerivative>();
            if (dlcsAsset.MediaType!.StartsWith("video"))
            {
                derivs.Add(FormatAVDerivative(AVDerivativeTemplateVideo, dlcsAsset, "mp4"));
                derivs.Add(FormatAVDerivative(AVDerivativeTemplateVideo, dlcsAsset, "webm"));
            }
            if (dlcsAsset.MediaType.Contains("audio"))
            {
                derivs.Add(FormatAVDerivative(AVDerivativeTemplateAudio, dlcsAsset, "mp3"));
            }
            return derivs;
        }

        private AVDerivative FormatAVDerivative(string template, Image dlcsAsset, string fileExt)
        {
            return new AVDerivative
            (
                id: string.Format(template,
                       options.ResourceEntryPoint,
                       options.CustomerName?.ToLower(),
                       options.CustomerDefaultSpace,
                       dlcsAsset.StorageIdentifier,
                       fileExt),
                label: fileExt
            );
        }
    }

    class MessageObject : JSONLDBase
    {
        [JsonProperty(PropertyName = "message")]
        public string? Message { get; set; }
    }
}
