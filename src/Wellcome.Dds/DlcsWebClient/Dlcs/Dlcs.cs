using DlcsWebClient.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using Utils;
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

        internal Task<Operation<TRequest, TResponse>> PostOperation<TRequest, TResponse>(TRequest requestObject, Uri uri)
        {
            var operation = new Operation<TRequest, TResponse>
            {
                HttpMethod = "POST",
                Uri = uri
            };
            return DoOperation(requestObject, operation);
        }

        internal Task<Operation<TRequest, TResponse>> GetOperation<TRequest, TResponse>(TRequest requestObject, Uri uri)
        {
            var operation = new Operation<TRequest, TResponse>
            {
                HttpMethod = "GET",
                Uri = uri
            };
            return DoOperation(requestObject, operation);
        }

        internal Task<Operation<TRequest, TResponse>> PatchOperation<TRequest, TResponse>(TRequest requestObject, Uri uri)
        {
            var operation = new Operation<TRequest, TResponse>
            {
                HttpMethod = "PATCH",
                Uri = uri
            };
            return DoOperation(requestObject, operation);
        }

        private async Task<Operation<TRequest, TResponse>> DoOperation<TRequest, TResponse>(
            TRequest requestObject, Operation<TRequest, TResponse> operation)
        {
            //httpClient.Timeout = TimeSpan.FromMilliseconds(360000);
            
            HttpResponseMessage response = null;
            try
            {
                if (requestObject != null)
                {
                    operation.RequestObject = requestObject;
                    operation.RequestJson = JsonConvert.SerializeObject(
                        operation.RequestObject, Formatting.Indented, jsonSerializerSettings);
                }
                
                switch (operation.HttpMethod)
                {
                    case "POST":
                        logger.LogInformation("About to HTTP POST to {uri}", operation.Uri);
                        response = await httpClient.PostAsync(operation.Uri, GetJsonContent(operation.RequestJson));
                        operation.ResponseJson = await response.Content.ReadAsStringAsync();
                        break;
                    case "PATCH":
                        logger.LogInformation("About to HTTP PATCH to {uri}", operation.Uri);
                        response = await httpClient.PatchAsync(operation.Uri, GetJsonContent(operation.RequestJson));
                        operation.ResponseJson = await response.Content.ReadAsStringAsync();
                        break;
                    case "GET":
                        var uriBuilder = new UriBuilder(operation.Uri);
                            
                        if (requestObject != null)
                        {
                            uriBuilder.Query = $"?q={operation.RequestJson}";
                        }
                        
                        logger.LogInformation("About to HTTP GET {uri}", uriBuilder.Uri);
                        operation.ResponseJson = await httpClient.GetStringAsync(uriBuilder.Uri);
                        break;
                    case "PUT":
                        throw new NotImplementedException("PUT - do this with HttpClient");
                    case "DELETE":
                        response = await httpClient.DeleteAsync(operation.Uri);
                        operation.ResponseJson = await response.Content.ReadAsStringAsync();
                        break;
                    default:
                        throw new NotImplementedException("Unknown HTTP Method " + operation.HttpMethod);
                }

                logger.LogInformation("Response object received");

                try
                {
                    operation.ResponseObject = JsonConvert.DeserializeObject<TResponse>(operation.ResponseJson);
                }
                catch (Exception deserializeEx)
                {
                    // TODO: for protagonist, this is where we would now try:
                    // var hydraError = JsonConvert.DeserializeObject<Hydra.Model.Error>(operation.ResponseJson);
                    // ... and deal with a proper error.
                    // But we should only do that once we are using the Hydra and DLCS API Client libraries as used by
                    // protagonist - they need to be nuget packages.
                    
                    // So for now, just do what was happening already in the catch below:
                    operation.Error = GetError(deserializeEx, response);
                    logger.LogError(deserializeEx, "Error in dlcs request client - {message}", deserializeEx.Message);
                }
            }
            catch (Exception ex)
            {
                operation.Error = GetError(ex, response);
                logger.LogError(ex, "Error in dlcs request client - {message}", ex.Message);
            }

            return operation;
        }

        private static StringContent GetJsonContent(string json) =>
            new StringContent(json, Encoding.UTF8, "application/json");
        
        private static readonly char[] SlashSeparator = new[] { '/' };
        
        // TODO: Is this required still?
        private string GetLocalStorageIdentifier(string id)
        {
            // This used to rely on the fact that all Preservica / Wellcome IDs
            // had a GUID as their last component
            var last = id.Split(SlashSeparator).Last();
            return last;
        }
        
        public Task<Operation<ImageQuery, HydraImageCollection>> GetImages(ImageQuery query, int defaultSpace)
        {
            int space = defaultSpace;
            if (query.Space.HasValue) space = query.Space.Value;
            var imageQueryUri = $"{options.ApiEntryPoint}customers/{options.CustomerId}/spaces/{space}/images";
            return GetOperation<ImageQuery, HydraImageCollection>(query, new Uri(imageQueryUri));
        }

        public Task<Operation<ImageQuery, HydraImageCollection>> GetImages(string nextUri) 
            => GetOperation<ImageQuery, HydraImageCollection>(null, new Uri(nextUri));

        private Task<Operation<string, HydraErrorByMetadataCollection>> GetErrorsByMetadata(string uri) 
            => GetOperation<string, HydraErrorByMetadataCollection>(null, new Uri(uri));

        public Task<Operation<string, Batch>> GetBatch(string batchId)
        {
            const string batchTemplate = "{0}customers/{1}/queue/batches/{2}";
            if (!Uri.IsWellFormedUriString(batchId, UriKind.Absolute))
            {
                batchId = string.Format(batchTemplate, options.ApiEntryPoint, options.CustomerId, batchId);
            }
            return GetOperation<string, Batch>(null, new Uri(batchId));
        }

        private static Error GetError(Exception ex, HttpResponseMessage response)
        {
            if (ex is HttpRequestException httpRequestException)
            {
                return new Error
                {
                    Status = (int?)response?.StatusCode ?? 0,
                    Message = httpRequestException.Message
                };
            }
            
            return new Error
            {
                Status = 0,
                Message = ex.Message
            };
        }

        private static Uri _imageQueueUri;
        private void InitQueue()
        {
            if (_imageQueueUri == null)
            {
                // TODO: At this point we would work out RESTfully where the queue for this customer is
                // and cache that for a bit - we assume the API stays reasonably stable.
                _imageQueueUri = new Uri($"{options.ApiEntryPoint}customers/{options.CustomerId}/queue");
            }
        }

        public Task<Operation<HydraImageCollection, Batch>> RegisterImages(HydraImageCollection images, bool priority = false)
        {
            InitQueue();
            return PostOperation<HydraImageCollection, Batch>(images,
                priority
                ? new Uri($"{_imageQueueUri}/priority")
                : _imageQueueUri);
        }

        public Task<Operation<HydraImageCollection, HydraImageCollection>> PatchImages(HydraImageCollection images)
        {
            string uri =
                $"{options.ApiEntryPoint}customers/{options.CustomerId}/spaces/{options.CustomerDefaultSpace}/images";
            foreach (var image in images.Members)
            {
                if (image.ModelId.IndexOf("/") == -1)
                {
                    image.ModelId = $"{options.CustomerId}/{options.CustomerDefaultSpace}/{image.ModelId}";
                }
            }
            return PatchOperation<HydraImageCollection, HydraImageCollection>(images, new Uri(uri));
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

        public Task<IEnumerable<Image>> GetImagesForIdentifier(string identifier)
        {
            var ddsId = new DdsIdentifier(identifier);
            if(ddsId.IdentifierType == IdentifierType.BNumberAndSequenceIndex)
            {
                throw new NotSupportedException("No more sequence index");
            }
            return ddsId.IdentifierType switch
            {
                IdentifierType.BNumber => GetImagesForBNumber(identifier),
                IdentifierType.Volume => GetImagesForVolume(identifier),
                IdentifierType.BNumberAndSequenceIndex => GetImagesBySequenceIndex(ddsId.BNumber, ddsId.SequenceIndex),
                IdentifierType.Issue => GetImagesForIssue(identifier),
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
        /// <param name="identfier"></param>
        /// <returns></returns>
        // string 1
        public Task<IEnumerable<Image>> GetImagesForBNumber(string identifier) 
            => GetImagesFromQuery(new ImageQuery { String1 = identifier });

        // string 1, number 1
        public Task<IEnumerable<Image>> GetImagesBySequenceIndex(string identifier, int sequenceIndex) 
            => GetImagesFromQuery(new ImageQuery { String1 = identifier, Number1 = sequenceIndex });

        // string 2
        public Task<IEnumerable<Image>> GetImagesForVolume(string volumeIdentifier) 
            => GetImagesFromQuery(new ImageQuery { String2 = volumeIdentifier });

        // string 3
        public Task<IEnumerable<Image>> GetImagesForIssue(string issueIdentifier) 
            => GetImagesFromQuery(new ImageQuery { String3 = issueIdentifier });

        public Task<IEnumerable<Image>> GetImagesForString3(string identifier) 
            => GetImagesForIssue(identifier);

        public async Task<IEnumerable<Image>> GetImagesByDlcsIdentifiers(List<string> identifiers)
        {
            // POST a list of identifiers to /allImages, which will return images by Id
            var uri = $"{options.ApiEntryPoint}customers/{options.CustomerId}/allImages";
            var fullIds = identifiers.Select(g => $"{options.CustomerId}/{options.CustomerDefaultSpace}/{g}");
            var request = new HydraStringIdCollection(fullIds);
            var op = await PostOperation<HydraStringIdCollection, HydraImageCollection>(request, new Uri(uri));
            
            foreach (var image in op.ResponseObject.Members)
            {
                image.StorageIdentifier = GetLocalStorageIdentifier(image.Id);
            }

            return op.ResponseObject.Members;
        }

        // immediate mode
        public IEnumerable<Image> RegisterNewImages(List<Image> images)
        {
            throw new NotImplementedException();
        }

        public async Task<int> DeleteImages(List<Image> images)
        {
            const string uriTemplate = "{0}customers/{1}/deleteImages";
            string uri = string.Format(uriTemplate, options.ApiEntryPoint, options.CustomerId);
            const string template = "{0}/{1}/{2}";
            var fullIds = images.Select(
                    im => string.Format(template, options.CustomerId, options.CustomerDefaultSpace, im.StorageIdentifier));
            var request = new HydraStringIdCollection(fullIds);
            var op = await PostOperation<HydraStringIdCollection, MessageObject>(request, new Uri(uri));
            var message = op.ResponseObject == null ? "NO RESPONSE OBJECT" : op.ResponseObject.Message;
            logger.LogInformation("Attempted to delete {0} images, response was {1}", images.Count, message);
            return images.Count;
        }

        /// <summary>
        /// Get details of PDF from PDF-control file
        /// </summary>
        public async Task<IPdf> GetPdfDetails(string identifier)
        {
            // e.g., pdf-control/wellcome/pdf/5/b12345678_0004
            var uri =
                $"https://dlcs.io/pdf-control/{options.CustomerName}/{options.PdfQueryName}/{options.CustomerDefaultSpace}/{identifier}";

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
        /// <returns></returns>
        public async Task<List<Batch>> GetTestedImageBatches(List<Batch> imageBatches)
        {
            const string template = "{0}customers/{1}/queue/batches/{2}/test";
            var testedBatches = new List<Batch>();
            foreach (var imageBatch in imageBatches)
            {
                var batchLocalId = imageBatch.Id.Split('/').Last();
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
                    var batch = await GetBatch(imageBatch.Id);
                    testedBatches.Add(batch.ResponseObject);
                }
                else
                {
                    testedBatches.Add(imageBatch);
                }
            }
            return testedBatches;
        }


        public async Task<IEnumerable<ErrorByMetadata>> GetErrorsByMetadata()
        {
            const string template = "{0}customers/{1}/queue/recentErrorsByMetadata/string3";
            bool first = true;
            string nextUri = null;

            var allErrors = new List<ErrorByMetadata>();

            while (first || nextUri != null)
            {
                Operation<string, HydraErrorByMetadataCollection> statesOperation;
                if (first)
                {
                    logger.LogInformation("On the first call, pass base URL");
                    var initialUri = string.Format(template, options.ApiEntryPoint, options.CustomerId);
                    statesOperation = await GetErrorsByMetadata(initialUri);
                    
                    first = false;
                }
                else
                {
                    logger.LogInformation("Following link to next page {0}", nextUri);
                    statesOperation = await GetErrorsByMetadata(nextUri);
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
        public async Task<Page<ErrorByMetadata>> GetErrorsByMetadata(int page)
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
            var statesOperation = await GetErrorsByMetadata(initialUri);
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
        /// <returns></returns>
        private async Task<IEnumerable<Image>> GetImagesFromQuery(ImageQuery query)
        {
            bool first = true;
            string nextUri = null;

            var images = new List<Image>();

            while (first || nextUri != null)
            {
                Operation<ImageQuery, HydraImageCollection> statesOperation;
                if (first)
                {
                    logger.LogInformation("On the first call, pass the query object");
                    statesOperation = await GetImages(query, options.CustomerDefaultSpace);
                    first = false;
                }
                else
                {
                    logger.LogInformation("Following link to next page {0}", nextUri);
                    statesOperation = await GetImages(nextUri);
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
                        image.StorageIdentifier = GetLocalStorageIdentifier(image.Id);
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
            httpClient.Timeout = TimeSpan.FromMilliseconds(4000);
            var response = await httpClient.GetStringAsync(url);

            var result = JObject.Parse(response);

            return new Dictionary<string, long>
            {
                {"incoming", result["incoming"].Value<long>()},
                {"priority", result["priority"].Value<long>()}
            };
        }


        public List<AVDerivative> GetAVDerivatives(Image dlcsAsset)
        {
            // This knows that we have webm, mp4 and mp3... it shouldn't know this, it should learn it.
            const string AVDerivativeTemplateVideo = "{0}iiif-av/{1}/{2}/{3}/full/full/max/max/0/default.{4}";
            const string AVDerivativeTemplateAudio = "{0}iiif-av/{1}/{2}/{3}/full/max/default.{4}";

            var derivs = new List<AVDerivative>();
            if (dlcsAsset.MediaType.StartsWith("video"))
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
            {
                Id = string.Format(template,
                       options.ResourceEntryPoint,
                       options.CustomerName.ToLower(),
                       options.CustomerDefaultSpace,
                       dlcsAsset.StorageIdentifier,
                       fileExt),
                Label = fileExt
            };
        }
    }

    class MessageObject
    {
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
    }
}
