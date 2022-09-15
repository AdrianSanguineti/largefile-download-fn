using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.IO.Compression;
using Microsoft.AspNetCore.Http.Features;
using System.Threading;
using System.Collections.Generic;

namespace ZipArchiveFn
{
    /*
     * Problems encountered:
     * 
     * 1. Isolated Process function requires entire file to be in memory before being sent to the client
     * 2. ZipArchiveEntry class uses the BinaryWriter class which does not support Async IO streams (https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Compression/src/System/IO/Compression/ZipArchiveEntry.cs and https://github.com/dotnet/aspnetcore/issues/7644)
     * 3. Synchronous IO streams are hard to enable (if not impossible) in Azure Fns during start up. Every request needs access to the HttpContext to enable the feature via IHttpBodyControlFeature.
     * 4. Dynamically streaming zip means that no checksums can be implemented.
     */

    public static class StreamZipFunction
    {
        [FunctionName("StreamZip")]
        public static IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]  HttpRequest req, ILogger log, CancellationToken cancellationToken = default)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var syncIOFeature = req.HttpContext.Features.Get<IHttpBodyControlFeature>();
            if (syncIOFeature != null)
            {
                syncIOFeature.AllowSynchronousIO = true;
            }


            var files = new Dictionary<string, string>
            {
                ["File1.jpg"] = @"C:\File1.jpg",
                ["File2.jpg"] = @"C:\File1.jpg",
                ["File3.jpg"] = @"C:\File1.jpg",
                ["File4.jpg"] = @"C:\File1.jpg",

            };

            return new FileCallbackResult(
                new MediaTypeHeaderValue("application/octet-stream"),
                async (outputStream, _) =>
                {
                    using var zipArchive = new ZipArchive(outputStream, ZipArchiveMode.Create);
                    

                    foreach (var file in files)
                    {
                        var zipEntry = zipArchive.CreateEntry(file.Key);
                        using var stream = File.OpenRead(file.Value);
                        using var zipStream = zipEntry.Open();
                        await stream.CopyToAsync(zipStream, cancellationToken);
                    }
                }
            )
            {
                FileDownloadName = "File.zip",
                EnableRangeProcessing = true
            };
        }
    }

    /// <summary>
    /// Stephen Cleary's FileCallbackResult class: https://blog.stephencleary.com/2016/11/streaming-zip-on-aspnet-core.html
    /// </summary>
    public class FileCallbackResult : FileResult
    {
        private Func<Stream, ActionContext, Task> callback;

        public FileCallbackResult(MediaTypeHeaderValue contentType, Func<Stream, ActionContext, Task> callback)
            : base(contentType?.ToString())
        {
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public override Task ExecuteResultAsync(ActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var executor = new FileCallbackResultExecutor(context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>());
            return executor.ExecuteAsync(context, this);
        }

        private sealed class FileCallbackResultExecutor : FileResultExecutorBase
        {
            public FileCallbackResultExecutor(ILoggerFactory loggerFactory)
                : base(CreateLogger<FileCallbackResultExecutor>(loggerFactory))
            {
            }

            public Task ExecuteAsync(ActionContext context, FileCallbackResult result)
            {
                SetHeadersAndLog(context, result, null, result.EnableRangeProcessing);
                return result.callback(context.HttpContext.Response.Body, context);
            }
        }
    }
}
