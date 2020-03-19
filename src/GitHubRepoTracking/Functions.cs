using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        public static async void TrackReposTimer([TimerTrigger("0 0 * * *")]TimerInfo myTimer, ILogger log, ExecutionContext context)
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
    }
}
