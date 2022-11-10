using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace OrderItemsReserver
{
    public static class AddOrder
    {
        [FunctionName("add-order")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Blob("orders/{sys.utcnow}.json", FileAccess.Write, Connection = "AzureWebJobsStorage")] CloudBlockBlob outputBlob,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            await outputBlob.UploadTextAsync(requestBody);
            return new OkResult();
        }
    }
}
