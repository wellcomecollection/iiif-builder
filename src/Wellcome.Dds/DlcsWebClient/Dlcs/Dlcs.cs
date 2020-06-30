using DlcsWebClient.Config;
using DlcsWebClient.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using Wellcome.Dds.AssetDomain.Dashboard;
using Wellcome.Dds.AssetDomain.Dlcs;
using Wellcome.Dds.AssetDomain.Dlcs.Model;
using Wellcome.Dds.AssetDomain.Dlcs.RestOperations;
using Wellcome.Dds.Common;

namespace DlcsWebClient.Dlcs
{
    public class Dlcs : IDlcs
    {
        private ILogger<Dlcs> logger;
        private DlcsOptions options;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

        public Dlcs(
            ILogger<Dlcs> logger,
            IOptions<DlcsOptions> options)
        {
            this.logger = logger;
            this.options = options.Value;
            _jsonSerializerSettings = new JsonSerializerSettings
            {
                // http://stackoverflow.com/questions/23170918/is-the-jsonserializersettings-thread-safe
                // TODO - is this (and its CamelCasePropertyNamesContractResolver) thread safe?
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }


        private string _basicAuthHeader;
        private string GetBasicAuthHeader()
        {
            if (_basicAuthHeader != null)
            {
                return _basicAuthHeader;
            }
            var credentials = options.ApiKey + ":" + options.ApiSecret;
            var b64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            _basicAuthHeader = "Basic " + b64;
            return _basicAuthHeader;
        }


        private void AddHeaders(WebClient wc)
        {
            wc.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            wc.Headers.Add(HttpRequestHeader.Authorization, GetBasicAuthHeader());
        }


        internal Operation<TRequest, TResponse> PostOperation<TRequest, TResponse>(TRequest requestObject, Uri uri)
        {
            var operation = new Operation<TRequest, TResponse>
            {
                HttpMethod = "POST",
                Uri = uri
            };
            return DoOperation(requestObject, operation);
        }

        internal Operation<TRequest, TResponse> GetOperation<TRequest, TResponse>(TRequest requestObject, Uri uri)
        {
            var operation = new Operation<TRequest, TResponse>
            {
                HttpMethod = "GET",
                Uri = uri
            };
            return DoOperation(requestObject, operation);
        }

        internal Operation<TRequest, TResponse> PatchOperation<TRequest, TResponse>(TRequest requestObject, Uri uri)
        {
            var operation = new Operation<TRequest, TResponse>
            {
                HttpMethod = "PATCH",
                Uri = uri
            };
            return DoOperation(requestObject, operation);
        }

        private Operation<TRequest, TResponse> DoOperation<TRequest, TResponse>(
            TRequest requestObject, Operation<TRequest, TResponse> operation)
        {
            // TODO - REFACTOR for System.Net.HttpClient, proper .NET Core usage.
            using (var wc = WebClientProvider.GetWebClient(360000))
            {
                try
                {
                    if (requestObject != null)
                    {
                        operation.RequestObject = requestObject;
                        operation.RequestJson = JsonConvert.SerializeObject(
                            operation.RequestObject, Formatting.Indented, _jsonSerializerSettings);
                    }
                    AddHeaders(wc);
                    switch (operation.HttpMethod)
                    {
                        case "POST":
                            logger.LogInformation("About to HTTP POST to {0}", operation.Uri);
                            operation.ResponseJson = wc.UploadString(operation.Uri, "POST", operation.RequestJson);
                            break;
                        case "PATCH":
                            logger.LogInformation("About to HTTP PATCH to {0}", operation.Uri);
                            operation.ResponseJson = wc.UploadString(operation.Uri, "PATCH", operation.RequestJson);
                            break;
                        case "GET":
                            if (requestObject != null)
                            {
                                wc.QueryString = new NameValueCollection { { "q", operation.RequestJson } };
                            }
                            logger.LogInformation("About to HTTP GET {0}", operation.Uri);
                            operation.ResponseJson = wc.DownloadString(operation.Uri);
                            break;
                        case "PUT":
                            throw new NotImplementedException("PUT - do this with HttpClient");
                        case "DELETE":
                            // TODO: this REALLY needs HttpClient!!
                            var bytes = wc.UploadData(operation.Uri, "DELETE", new byte[0]);
                            operation.ResponseJson = wc.Encoding.GetString(bytes);
                            break;
                        default:
                            throw new NotImplementedException("Unknown HTTP Method " + operation.HttpMethod);
                    }
                    logger.LogInformation("Response object received");
                    operation.ResponseObject = JsonConvert.DeserializeObject<TResponse>(operation.ResponseJson);
                }
                catch (Exception ex)
                {
                    operation.Error = GetError(ex);
                    logger.LogError("Error in web client - " + ex.Message, ex);
                }
            }
            return operation;
        }


        private static readonly char[] SlashSeparator = new[] { '/' };
        // TODO: Is this required still?
        private string GetLocalStorageIdentifier(string id)
        {
            // This used to rely on the fact that all Preservica / Wellcome IDs
            // had a GUID as their last component
            var last = id.Split(SlashSeparator).Last();
            return last;
            //Guid g;
            //Guid.TryParse(last, out g);
            //return g;
        }
        public Operation<ImageQuery, HydraImageCollection> GetImages(ImageQuery query, int defaultSpace)
        {
            int space = defaultSpace;
            if (query.Space.HasValue) space = query.Space.Value;
            var imageQueryUri = string.Format("{0}customers/{1}/spaces/{2}/images",
                options.ApiEntryPoint, options.CustomerId, space);
            return GetOperation<ImageQuery, HydraImageCollection>(query, new Uri(imageQueryUri));
        }

        public Operation<ImageQuery, HydraImageCollection> GetImages(string nextUri)
        {
            return GetOperation<ImageQuery, HydraImageCollection>(null, new Uri(nextUri));
        }

        private Operation<string, HydraErrorByMetadataCollection> GetErrorsByMetadata(string uri)
        {
            return GetOperation<string, HydraErrorByMetadataCollection>(null, new Uri(uri));
        }

        public Operation<string, Batch> GetBatch(string batchId)
        {
            const string batchTemplate = "{0}customers/{1}/queue/batches/{2}";
            if (batchId.IndexOf("/") == -1)
            {
                batchId = string.Format(batchTemplate, options.ApiEntryPoint, options.CustomerId, batchId);
            }
            return GetOperation<string, Batch>(null, new Uri(batchId));
        }

        private static Error GetError(Exception ex)
        {
            if (ex is WebException)
            {
                var we = (WebException)ex;
                var badResponse = (HttpWebResponse)we.Response;
                if (badResponse != null)
                {
                    return new Error
                    {
                        Status = (int)badResponse.StatusCode,
                        Message = we.Message
                    };
                }
                return new Error
                {
                    Status = 0,
                    Message = we.Message
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
                _imageQueueUri = new Uri(string.Format("{0}customers/{1}/queue", options.ApiEntryPoint, options.CustomerId));
            }
        }


        public Operation<HydraImageCollection, Batch> RegisterImages(HydraImageCollection images, bool priority = false)
        {
            InitQueue();
            return PostOperation<HydraImageCollection, Batch>(images,
                priority
                ? new Uri(String.Concat(_imageQueueUri.ToString(), "/priority"))
                : _imageQueueUri);
        }

        public Operation<HydraImageCollection, HydraImageCollection> PatchImages(HydraImageCollection images)
        {
            const string uriTemplate = "{0}customers/{1}/spaces/{2}/images";
            string uri = string.Format(uriTemplate, options.ApiEntryPoint, options.CustomerId, options.CustomerDefaultSpace);
            const string idTemplate = "{0}/{1}/{2}";
            foreach (var image in images.Members)
            {
                if (image.ModelId.IndexOf("/") == -1)
                {
                    image.ModelId = string.Format(idTemplate,
                        options.CustomerId, options.CustomerDefaultSpace, image.ModelId);
                }
            }
            return PatchOperation<HydraImageCollection, HydraImageCollection>(images, new Uri(uri));
        }

        public string GetRoleUri(string accessCondition)
        {
            // https://api.dlcs.io/customers/1/roles/requiresRegistration
            string firstCharLowered = accessCondition.Trim()[0].ToString().ToLowerInvariant() + accessCondition.Substring(1);
            if (firstCharLowered == "requires registration")
            {
                firstCharLowered = "clickthrough";
            }
            return string.Format("{0}customers/{1}/roles/{2}", options.ApiEntryPoint, options.CustomerId, ToCamelCase(firstCharLowered));
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


        public IEnumerable<Image> GetImagesForIdentifier(string identifier)
        {
            var ddsId = new DdsIdentifier(identifier);
            switch (ddsId.IdentifierType)
            {
                case IdentifierType.BNumber:
                    return GetImagesForBNumber(identifier);
                case IdentifierType.Volume:
                    return GetImagesForVolume(identifier);
                case IdentifierType.BNumberAndSequenceIndex:
                    return GetImagesBySequenceIndex(ddsId.BNumber, ddsId.SequenceIndex);
                case IdentifierType.Issue:
                    return GetImagesForIssue(identifier);
            }
            throw new NotSupportedException("Unknown identifier");
        }

        /// <summary>
        /// Implementation NOTE
        /// These must return IEnumerables that don't get enumerated until aske for.
        /// otherwise we have way too many queries hitting the DLCS.
        /// </summary>
        /// <param name="identfier"></param>
        /// <returns></returns>

        // string 1
        public IEnumerable<Image> GetImagesForBNumber(string identifier)
        {
            return GetImagesFromQuery(new ImageQuery { String1 = identifier });
        }

        // string 1, number 1
        public IEnumerable<Image> GetImagesBySequenceIndex(string identifier, int sequenceIndex)
        {
            return GetImagesFromQuery(new ImageQuery { String1 = identifier, Number1 = sequenceIndex });
        }

        // string 2
        public IEnumerable<Image> GetImagesForVolume(string volumeIdentifier)
        {
            return GetImagesFromQuery(new ImageQuery { String2 = volumeIdentifier });
        }

        // string 3
        public IEnumerable<Image> GetImagesForIssue(string issueIdentfier)
        {
            return GetImagesFromQuery(new ImageQuery { String3 = issueIdentfier });
        }
        public IEnumerable<Image> GetImagesForString3(string identifier)
        {
            return GetImagesForIssue(identifier);
        }

        public IEnumerable<Image> GetImagesByDlcsIdentifiers(List<string> identifiers)
        {
            const string uriTemplate = "{0}customers/{1}/allImages";
            string uri = string.Format(uriTemplate, options.ApiEntryPoint, options.CustomerId);
            const string template = "{0}/{1}/{2}";
            var fullIds = identifiers.Select(
                    g => string.Format(template, options.CustomerId, options.CustomerDefaultSpace, g));
            var request = new HydraStringIdCollection(fullIds);
            var op = PostOperation<HydraStringIdCollection, HydraImageCollection>(request, new Uri(uri));
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


        public int DeleteImages(List<Image> images)
        {
            const string uriTemplate = "{0}customers/{1}/deleteImages";
            string uri = string.Format(uriTemplate, options.ApiEntryPoint, options.CustomerId);
            const string template = "{0}/{1}/{2}";
            var fullIds = images.Select(
                    im => string.Format(template, options.CustomerId, options.CustomerDefaultSpace, im.StorageIdentifier));
            var request = new HydraStringIdCollection(fullIds);
            var op = PostOperation<HydraStringIdCollection, MessageObject>(request, new Uri(uri));
            var message = op.ResponseObject == null ? "NO RESPONSE OBJECT" : op.ResponseObject.Message;
            logger.LogInformation("Attempted to delete {0} images, response was {1}", images.Count, message);
            return images.Count;
        }

        /// <summary>
        /// TODO: This MUST be changed to use string3 as soon as the manifest has that info to emit into the rendering
        /// </summary>
        /// <param name="string1"></param>
        /// <param name="number1"></param>
        /// <returns></returns>
        public IPdf GetPdfDetails(string string1, int number1)
        {
            const string controlTemplate = "{0}pdf-control/{1}/pdf-item/{2}/{3}";
            var uri = string.Format(controlTemplate,
                options.ResourceEntryPoint, options.CustomerName, string1, number1);
            Pdf pdf;
            using (var wc = WebClientProvider.GetWebClient(360000))
            {
                var s = wc.DownloadString(uri);
                pdf = JsonConvert.DeserializeObject<Pdf>(s);
                if (string.IsNullOrEmpty(pdf.Url))
                {
                    pdf.Url = uri.Replace("pdf-control", "pdf");
                }
            }
            return pdf;
        }

        /// <summary>
        /// TODO: This MUST be changed to use string3 as soon as the manifest has that info to emit into the rendering
        /// </summary>
        public bool DeletePdf(string string1, int number1)
        {
            const string template = "{0}customers/{1}/resources/pdf/pdf-item?args={2}";
            var uri = string.Format(template,
                options.ApiEntryPoint, options.CustomerId, string1 + "/" + number1);
            using (var wc = WebClientProvider.GetWebClient(360000))
            {
                AddHeaders(wc);
                var s = wc.UploadString(uri, "DELETE", String.Empty);
                dynamic result = JObject.Parse(s);
                return (bool)result.success;
            }
        }

        /// <summary>
        /// Return tested versions of the batches
        /// </summary>
        /// <param name="imageBatches"></param>
        /// <returns></returns>
        public List<Batch> GetTestedImageBatches(List<Batch> imageBatches)
        {
            const string template = "{0}customers/{1}/queue/batches/{2}/test";
            var testedBatches = new List<Batch>();
            foreach (var imageBatch in imageBatches)
            {
                var batchLocalId = imageBatch.Id.Split('/').Last();
                var uri = string.Format(template, options.ApiEntryPoint, options.CustomerId, batchLocalId);
                using (var wc = WebClientProvider.GetWebClient(360000))
                {
                    AddHeaders(wc);
                    var s = wc.UploadString(uri, "POST");
                    dynamic result = JObject.Parse(s);
                    if ((bool)result.success)
                    {
                        testedBatches.Add(GetBatch(imageBatch.Id).ResponseObject);
                    }
                    else
                    {
                        testedBatches.Add(imageBatch);
                    }
                }
            }
            return testedBatches;
        }


        public IEnumerable<ErrorByMetadata> GetErrorsByMetadata()
        {
            const string template = "{0}customers/{1}/queue/recentErrorsByMetadata/string3";
            bool first = true;
            string nextUri = null;

            while (first || nextUri != null)
            {
                Operation<string, HydraErrorByMetadataCollection> statesOperation;
                if (first)
                {
                    logger.LogInformation("On the first call, pass base URL");
                    var initialUri = string.Format(template, options.ApiEntryPoint, options.CustomerId);
                    statesOperation = GetErrorsByMetadata(initialUri);
                    first = false;
                }
                else
                {
                    logger.LogInformation("Following link to next page {0}", nextUri);
                    statesOperation = GetErrorsByMetadata(nextUri);
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
                    logger.LogInformation("Yielding {0} collection members to returned image list", errorByMetadataCollection.Members.Length);
                    foreach (var errorByMetadata in errorByMetadataCollection.Members)
                    {
                        yield return errorByMetadata;
                    }
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
            //lastResponseData = statesOperation.ResponseJson
        }

        /// <summary>
        ///  TODO - generic method, generic collection
        /// </summary>
        /// <returns></returns>
        public Page<ErrorByMetadata> GetErrorsByMetadata(int page)
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
            var statesOperation = GetErrorsByMetadata(initialUri);
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

            //ArtificiallyPopulateErrorsByMetadataWithExtraStuff(pageObj);
            return pageObj;
        }

        // sets the two new strings data and response
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private IEnumerable<Image> GetImagesFromQuery(ImageQuery query) // , out string data, out string lastResponseData)
        {
            bool first = true;
            string nextUri = null;

            while (first || nextUri != null)
            {
                Operation<ImageQuery, HydraImageCollection> statesOperation;
                if (first)
                {
                    logger.LogInformation("On the first call, pass the query object");
                    statesOperation = GetImages(query, options.CustomerDefaultSpace);
                    first = false;
                }
                else
                {
                    logger.LogInformation("Following link to next page {0}", nextUri);
                    statesOperation = GetImages(nextUri);
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
                            LoadMetadata(image);
                        }
                        yield return image;
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
            //lastResponseData = statesOperation.ResponseJson;
        }


        private void LoadMetadata(Image image)
        {
            try
            {
                using (var wc = WebClientProvider.GetWebClient(2000))
                {
                    AddHeaders(wc);
                    image.Metadata = wc.DownloadString(image.Metadata);
                }
            }
            catch (Exception wcEx)
            {
                image.Metadata = string.Format("[could not retrieve metadata from {0}: {1}]", image.Metadata,
                    wcEx.Message);
            }
        }

        public int DefaultSpace
        {
            get { return options.CustomerDefaultSpace; }
        }


        public Dictionary<string, long> GetDlcsQueueLevel()
        {
            JObject result;
            var url = options.ApiEntryPoint + "queue";
            using (var wc = WebClientProvider.GetWebClient(5000))
            {
                result = JObject.Parse(wc.DownloadString(url));
            }

            return new Dictionary<string, long>
            {
                {"incoming", result["incoming"].Value<long>()},
                {"priority", result["priority"].Value<long>()}
            };
        }
    }


    class MessageObject
    {
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
    }
}
