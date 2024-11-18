using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace KrantenJongen.Functions;

public static class FunctionHelper
{
    public static async Task HandleAsync(HttpContext context, Func<CancellationToken, Task> execute)
    {
        var cancellationToken = context.RequestAborted;
        context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.Headers.Append("Access-Control-Allow-Methods", "GET");
            context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");
            context.Response.Headers.Append("Access-Control-Max-Age", "3600");
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        else if (HttpMethods.IsPost(context.Request.Method))
        {
            await execute(cancellationToken);

            context.Response.StatusCode = (int)HttpStatusCode.OK;
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
        }
    }

    public static async Task HandleAsync<T>(HttpContext context, Func<T, CancellationToken, Task> execute)
    {
        await HandleAsync(context, async cancellationToken =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<T>(body);

            await execute(data, cancellationToken);
        });
    }
}
