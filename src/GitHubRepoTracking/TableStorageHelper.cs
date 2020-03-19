using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GitHubRepoTracking
{
    public static class TableStorageHelper
    {
        public static CloudStorageAccount CreateStorageAccountFromConnectionString(string storageConnectionString)
        {
            CloudStorageAccount storageAccount;
            try
            {
                storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the application.");
                throw;
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the sample.");
                Console.ReadLine();
                throw;
            }

            return storageAccount;
        }

        public static async Task<CloudTable> CreateTableAsync(string tableName)
        {
            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

            CloudStorageAccount storageAccount = CreateStorageAccountFromConnectionString(storageConnectionString);

            CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());

            Console.WriteLine("Create a Table");

            CloudTable table = tableClient.GetTableReference(tableName);
            if (await table.CreateIfNotExistsAsync())
            {
                Console.WriteLine("Created Table named: {0}", tableName);
            }
            else
            {
                Console.WriteLine("Table {0} already exists", tableName);
            }

            Console.WriteLine();
            return table;
        }

        public static async Task<RepoStats> InsertOrMergeEntityAsync(CloudTable table, RepoStats entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }
            try
            {
                TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(entity);

                TableResult result = await table.ExecuteAsync(insertOrMergeOperation);
                RepoStats insertedCustomer = result.Result as RepoStats;

                if (result.RequestCharge.HasValue)
                {
                    Console.WriteLine("Request Charge of InsertOrMerge Operation: " + result.RequestCharge);
                }

                return insertedCustomer;
            }
            catch (StorageException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                throw;
            }
        }

        public static async Task<RepoStats> RetrieveEntityUsingPointQueryAsync(CloudTable table, string partitionKey, string rowKey)
        {
            try
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<RepoStats>(partitionKey, rowKey);
                TableResult result = await table.ExecuteAsync(retrieveOperation);
                RepoStats customer = result.Result as RepoStats;
                if (customer != null)
                {
                    Console.WriteLine("\t{0}\t{1}\t{2}", customer.PartitionKey, customer.RowKey,customer.RepoName);
                }

                if (result.RequestCharge.HasValue)
                {
                    Console.WriteLine("Request Charge of Retrieve Operation: " + result.RequestCharge);
                }

                return customer;
            }
            catch (StorageException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                throw;
            }
        }
    }
}
