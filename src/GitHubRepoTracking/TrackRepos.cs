using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Octokit;

namespace GitHubRepoTracking
{
    public static class TrackRepos
    {
        public static async Task Run(ILogger log, ExecutionContext context)
        {


            var client = new GitHubClient(new ProductHeaderValue("TrackRepos"));

            var basicAuth = new Credentials(Environment.GetEnvironmentVariable("GitHubToken")); // NOTE: not real credentials
            client.Credentials = basicAuth;

            var path = System.IO.Path.Combine(context.FunctionDirectory, "../Repos.json");

            List<Campaign> campaigns = JsonConvert.DeserializeObject<List<Campaign>>(File.ReadAllText(path));


            List<RepoStats> views = new List<RepoStats>();

            foreach (Campaign campaign in campaigns)
            {
                if (!string.IsNullOrEmpty(campaign.CampaignName) && !string.IsNullOrEmpty(campaign.OrgName))
                    foreach (Repo repo in campaign.Repos)
                    {
                        if (!string.IsNullOrEmpty(repo.RepoName))
                        {
                            var data = await client.Repository.Traffic.GetViews(campaign.OrgName, repo.RepoName, new RepositoryTrafficRequest(TrafficDayOrWeek.Day));
                            foreach (var item in data.Views)
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
                                views.Add(stat);
                            }
                        }
                    }
            }

            string tableName = "RepoStats";
            CloudTable table = await TableStorageHelper.CreateTableAsync(tableName);

            foreach (var view in views)
            {
                Console.WriteLine("Insert an Entity.");
                await TableStorageHelper.InsertOrMergeEntityAsync(table, view);
            }
        }
    }
}
