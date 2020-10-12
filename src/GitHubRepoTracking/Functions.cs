using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Octokit;

namespace GitHubRepoTracking
{
    public static class Functions
    {
        [FunctionName("TrackReposTimer")]
        public static async void TrackReposTimer([TimerTrigger("0 0 0 * * *")] TimerInfo myTimer, ILogger log, ExecutionContext context)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            await TrackRepos.Run(log, context);
        }

        [FunctionName("TrackReposHttp")]
        public static async Task<IActionResult> TrackReposHttp([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ExecutionContext context, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            await TrackRepos.Run(log, context);

            return new OkObjectResult("");
        }

        [FunctionName("GetRepos")]
        public static async Task<HttpResponseMessage> GetRepos([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ExecutionContext context, ILogger log)
        {
            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            BlobServiceClient blobServiceClient = new BlobServiceClient(storageConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("repos");
            BlobClient blobClient = containerClient.GetBlobClient("Repos.json");
            BlobDownloadInfo download = await blobClient.DownloadAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new StreamReader(download.Content).ReadToEnd(), Encoding.UTF8, "application/json")
            };

        }

        [FunctionName("UpdateRepos")]
        public static async Task<IActionResult> UpdateRepos([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<Campaign> data = JsonConvert.DeserializeObject<List<Campaign>>(requestBody);

            requestBody = JsonConvert.SerializeObject(data, Formatting.Indented);

            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            BlobServiceClient blobServiceClient = new BlobServiceClient(storageConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("repos");
            BlobClient blobClient = containerClient.GetBlobClient("Repos.json");

            blobClient.DeleteIfExists();

            using (var stream = new MemoryStream(Encoding.Default.GetBytes(requestBody), false))
            {
                await blobClient.UploadAsync(stream);
            }

            return (ActionResult)new OkObjectResult($"Update Complete");
        }

        // returns a single page application to build links
        [FunctionName("Manager")]
        public static HttpResponseMessage Admin([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            ILogger log)
        {
            const string PATH = "RepoManager.html";

            var result = SecurityCheck(req);
            if (result != null)
            {
                return result;
            }

            var scriptPath = Path.Combine(Environment.CurrentDirectory, "www");
            if (!Directory.Exists(scriptPath))
            {
                scriptPath = Path.Combine(
                    Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process),
                    @"site\wwwroot\www");
            }
            var filePath = Path.GetFullPath(Path.Combine(scriptPath, PATH));

            if (!File.Exists(filePath))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            log.LogInformation($"Attempting to retrieve file at path {filePath}.");
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var stream = new FileStream(filePath, System.IO.FileMode.Open);
            response.Content = new StreamContent(stream);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }

        private static HttpResponseMessage SecurityCheck(HttpRequestMessage req)
        {
            return req.RequestUri.IsLoopback || req.RequestUri.Scheme == "https" ? null :
                req.CreateResponse(HttpStatusCode.Forbidden);
        }
    }
}
