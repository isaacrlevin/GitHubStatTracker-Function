using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Octokit;

namespace GitHubRepoTracking
{
    public static class TrackRepos
    {
        public static async Task Run(ILogger log, Microsoft.Azure.WebJobs.ExecutionContext context)
        {
            var client = new GitHubClient(new ProductHeaderValue("TrackRepos"));

            var basicAuth = new Credentials(Environment.GetEnvironmentVariable("GitHubToken"));
            client.Credentials = basicAuth;

            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            BlobServiceClient blobServiceClient = new BlobServiceClient(storageConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("repos");
            BlobClient blobClient = containerClient.GetBlobClient("Repos.json");
            BlobDownloadInfo download = await blobClient.DownloadAsync();

            List<Campaign> campaigns = JsonConvert.DeserializeObject<List<Campaign>>(new StreamReader(download.Content).ReadToEnd());

            List<RepoStats> stats = new List<RepoStats>();

            foreach (Campaign campaign in campaigns)
            {
                if (!string.IsNullOrEmpty(campaign.CampaignName) && !string.IsNullOrEmpty(campaign.OrgName))
                    foreach (Repo repo in campaign.Repos)
                    {
                        if (!string.IsNullOrEmpty(repo.RepoName))
                        {
                            var views = await client.Repository.Traffic.GetViews(campaign.OrgName, repo.RepoName, new RepositoryTrafficRequest(TrafficDayOrWeek.Day));
                            var clones = await client.Repository.Traffic.GetClones(campaign.OrgName, repo.RepoName, new RepositoryTrafficRequest(TrafficDayOrWeek.Day));
                            foreach (var item in views.Views)
                            {
                                var stat = new RepoStats($"{campaign.CampaignName}{repo.RepoName}", item.Timestamp.UtcDateTime.ToShortDateString().Replace("/", ""))
                                {
                                    OrgName = campaign.OrgName,
                                    CampaignName = campaign.CampaignName,
                                    RepoName = repo.RepoName,
                                    Date = item.Timestamp.UtcDateTime.ToShortDateString(),
                                    Views = item.Count,
                                    UniqueUsers = item.Uniques
                                };

                                var clone = clones.Clones.Where(a => a.Timestamp.UtcDateTime.ToShortDateString() == item.Timestamp.UtcDateTime.ToShortDateString()).FirstOrDefault();

                                if (clone != null)
                                {
                                    stat.Clones = clone.Count;
                                    stat.UniqueClones = clone.Uniques;
                                }
                                stats.Add(stat);
                            }
                            Thread.Sleep(5000);
                        }
                    }
            }

            string tableName = "RepoStats";
            CloudTable table = await TableStorageHelper.CreateTableAsync(tableName);

            foreach (var view in stats)
            {
                var jsonString = JsonConvert.SerializeObject(view, 
                    Formatting.Indented, 
                    new JsonConverter[] { new StringEnumConverter() });
              log.LogInformation($"Inserted: {jsonString}");
                await TableStorageHelper.InsertOrMergeEntityAsync(table, view);
            }
        }
    }
}
