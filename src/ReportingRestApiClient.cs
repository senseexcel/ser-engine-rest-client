namespace Ser.Engine.Rest.Client
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NLog;
    #endregion

    public class ReportingRestApiClient
    {
        #region Logger
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties
        private HttpClient Client { get; set; }
        public bool SslVerify { get; set; } = true;
        public Dictionary<Uri, string> SslThumbprints { get; private set; } = new Dictionary<Uri, string>();
        #endregion

        #region Constructor
        public ReportingRestApiClient(Uri baseAddress, int timeout = 30)
        {
            if (timeout < 10)
                timeout = 10;
            if (timeout > 300)
                timeout = 300;

            var clientHandler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = ServerCertificateCustomValidation
            };

            Client = new HttpClient(clientHandler)
            {
                BaseAddress = baseAddress,
                Timeout = TimeSpan.FromSeconds(timeout)
            };
        }
        #endregion

        #region Private Methods
        private static Guid Generate(Guid? value)
        {
            if (value.HasValue)
                return value.Value;
            return Guid.NewGuid();
        }

        private bool ServerCertificateCustomValidation(HttpRequestMessage requestMessage, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslErrors)
        {
            logger.Debug("The server called ssl certificate validation...");

            if (sslErrors == SslPolicyErrors.None)
            {
                logger.Debug("No SSL policy errors.");
                return true;
            }

            if (!SslVerify)
            {
                logger.Info("Use property 'SslVerify' with value 'false'.");
                return true;
            }

            foreach (var thumbprint in SslThumbprints)
            {
                if(thumbprint.Key.Host.ToLowerInvariant() == requestMessage.RequestUri.Host.ToLowerInvariant())
                {
                    if (thumbprint.Value.ToLowerInvariant() == certificate.Thumbprint.ToLowerInvariant())
                        return true;
                }
            }

            return false;
        }
        #endregion

        #region Public Methods
        public Guid UploadFile(string fullpath, Guid? fileId = null, string filename = null)
        {
            try
            {
                var uploadFilename = Path.GetFileName(fullpath);
                if (!String.IsNullOrEmpty(filename))
                    uploadFilename = filename;

                var data = File.ReadAllBytes(fullpath);
                return UploadData(data, uploadFilename, fileId);
            }
            catch (Exception ex)
            {
                throw new Exception("The data file to rest api failed.", ex);
            }
        }

        public Guid UploadData(byte[] data, string filename, Guid? fileId = null)
        {
            try
            {
                var uploadId = Generate(fileId);
                var multiPartContent = new MultipartFormDataContent();
                var byteArrayContent = new ByteArrayContent(data);
                byteArrayContent.Headers.Add("Content-Type", "application/octet-stream");
                multiPartContent.Add(byteArrayContent, "file", filename);

                var result = Client.PostAsync($"/upload/{uploadId}", multiPartContent).Result;
                if (result.IsSuccessStatusCode)
                {
                    var jsonGuid = result.Content.ReadAsStringAsync().Result;
                    return JsonConvert.DeserializeObject<Guid>(jsonGuid);
                }
                throw new Exception(result.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("The data upload to rest api failed.", ex);
            }
        }

        public byte[] DownloadData(Guid folderId, string filename = null)
        {
            try
            {
                var requestUri = $"/download/{folderId}";
                if (!String.IsNullOrEmpty(filename))
                    requestUri += $"?filename={Uri.EscapeDataString(filename)}";

                var result = Client.GetAsync(requestUri).Result;
                if (result.IsSuccessStatusCode)
                    return result.Content.ReadAsByteArrayAsync().Result;
                throw new Exception(result.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("The file download from rest api failed.", ex);
            }
        }

        public bool Delete(Guid? folderId = null)
        {
            try
            {
                var requestUri = $"/delete";
                if (folderId.HasValue)
                    requestUri = $"/delete/{folderId}";
                var result = Client.DeleteAsync(requestUri).Result;
                if (result.IsSuccessStatusCode)
                    return true;
                throw new Exception(result.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("The folder deletion on rest api failed.", ex);
            }
        }

        public Guid RunTask(string jobJson, Guid? taskId = null)
        {
            try
            {
                var jobTaskId = Generate(taskId);
                var jsonContent = new StringContent(jobJson, Encoding.UTF8, "application/json");
                var result = Client.PostAsync($"/task/{jobTaskId}", jsonContent).Result;
                if (result.IsSuccessStatusCode)
                {
                    var jsonGuid = result.Content.ReadAsStringAsync().Result;
                    return JsonConvert.DeserializeObject<Guid>(jsonGuid);
                }
                throw new Exception(result.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("The task run on rest api failed.", ex);
            }
        }

        public bool StopTask(Guid? taskId = null)
        {
            try
            {
                var requestUri = $"/task";
                if (taskId.HasValue)
                    requestUri = $"/task/{taskId}";
                var result = Client.DeleteAsync(requestUri).Result;
                if (result.IsSuccessStatusCode)
                    return true;
                throw new Exception(result.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("The task stopping on rest api failed.", ex);
            }
        }

        public string GetStatus(Guid taskId)
        {
            try
            {
                var requestUri = $"/status/{taskId}";
                var result = Client.GetAsync(requestUri).Result;
                if (result.IsSuccessStatusCode)
                {
                    return result.Content.ReadAsStringAsync().Result;
                }
                throw new Exception(result.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("The task get request from rest api failed.", ex);
            }
        }

        public string GetAllStatus()
        {
            try
            {
                var requestUri = $"/status/all";
                var result = Client.GetAsync(requestUri).Result;
                if (result.IsSuccessStatusCode)
                {
                    return result.Content.ReadAsStringAsync().Result;
                }
                throw new Exception(result.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("The task get request from rest api failed.", ex);
            }
        }

        public string HealthStatus()
        {
            try
            {
                var result = Client.GetAsync($"/health").Result;
                if (result.IsSuccessStatusCode)
                    return result.Content.ReadAsStringAsync().Result;
                throw new Exception(result.ReasonPhrase);
            }
            catch (Exception ex)
            {
                throw new Exception("The health status request to rest api failed.", ex);
            }
        }
        #endregion
    }
}