using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace GitHubRepoTracking
{
    public class RepoStats : TableEntity
    {
        public RepoStats()
        {
        }

        public RepoStats(string campaignrepo, string date)
        {
            PartitionKey = campaignrepo;
            RowKey = date;
        }

        public string RepoName { get; set; }
        public string OrgName { get; set; }
        public string CampaignName { get; set; }
        public string Date { get; set; }
        public int Views { get; set; }
        public int UniqueUsers { get; set; }
        public int Clones { get; set; }
        public int UniqueClones { get; set; }
    }
}
