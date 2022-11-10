using System;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OrderItemsReserverServiceBus
{
    public class Function1
    {
        [FunctionName("Function1")]
        public void Run([ServiceBusTrigger("order", Connection = "ServiceBusConnection")]string myQueueItem, ILogger log, ExecutionContext context)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {myQueueItem}");

            var config = new ConfigurationBuilder()
                        .SetBasePath(context.FunctionAppDirectory)
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();

            var connectionString = config["AzureWebJobsStorage"];

            string containerName = "france-msgs";
            string blobName = Guid.NewGuid().ToString() + ".json";


            BlobContainerClient container = new BlobContainerClient(connectionString, containerName);
            container.CreateIfNotExists();

            BlobClient blob = container.GetBlobClient(blobName);

            blob.Upload(myQueueItem);

            log.LogInformation("Completed Uploading: " + blobName);

        }
    }
}
