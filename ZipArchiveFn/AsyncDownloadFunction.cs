using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Files.Shares;
using System.Security.Cryptography.X509Certificates;

namespace ZipArchiveFn
{
    public static class AsyncDownloadFunction
    {
        [FunctionName("RequestDownload")]
        public static async Task<IActionResult> RequestDownload(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }

        [FunctionName("Status")]
        public static async Task<IActionResult> Status([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            // TODO: Read the request ID.
            var dirName = "";
            var fileName = "";


            var connectionString = Environment.GetEnvironmentVariable("WEBSITE_CONTENTAZUREFILECONNECTIONSTRING");
            var shareName = Environment.GetEnvironmentVariable("WEBSITE_CONTENTSHARE");
            
            ShareClient share = new ShareClient(connectionString, shareName);
            ShareDirectoryClient directory = share.GetDirectoryClient(dirName);
            
            
            var fileClient = directory.GetFileClient(fileName);
            if (await fileClient.ExistsAsync())
            {
                return new OkObjectResult("File exists");
            }

            var file = await fileClient.DownloadAsync();
            file.Value.Content

            return new OkObjectResult("H");
        }

        [FunctionName("Download")]
        public static async Task<IActionResult> Download([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            return new OkObjectResult("S");
        }
    }
}
