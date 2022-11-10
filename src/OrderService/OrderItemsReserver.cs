using System;
using System.IO;
using System.Text;
using Azure.Storage.Blobs;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace OrderService
{
    public class OrderItemsReserver
    {
        [FunctionName("OrderItemsReserver")]
        public void Run([ServiceBusTrigger("order", Connection = "SBConnection")]string myQueueItem, ILogger log, ExecutionContext context)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {myQueueItem}");

            var config = new ConfigurationBuilder()
                        .SetBasePath(context.FunctionAppDirectory)
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();

            var connectionString = config["AzureWebJobsStorage"];

            string containerName = "order";
            string blobName = Guid.NewGuid().ToString() + ".json";

            var options = new BlobClientOptions();
            options.Retry.MaxRetries = 3;
            options.Retry.Mode = Azure.Core.RetryMode.Fixed;
            BlobContainerClient container = new BlobContainerClient(connectionString, containerName, options);
            container.CreateIfNotExists();

            BlobClient blob = container.GetBlobClient(blobName);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(myQueueItem ?? ""), writable: false))
            {
                blob.Upload(stream);
            }


            log.LogInformation("Completed Uploading: " + blobName);
        }
    }
}
