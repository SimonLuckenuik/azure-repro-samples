using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ExecutionContext = Microsoft.Azure.WebJobs.ExecutionContext;

[assembly: AssemblyVersion("1.0.0.*")]

namespace FunctionAppReproColdStartIssue
{
    public static class Functions
    {
        private static readonly HttpClient Client;
        private static readonly TelemetryClient TelemetryClient;

        static Functions()
        {
            Client = new HttpClient();
            TelemetryClient = new TelemetryClient
            {
                InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY")
            };
            TelemetryClient.Context.Cloud.RoleInstance =
                string.IsNullOrWhiteSpace(TelemetryClient.Context.Cloud.RoleInstance)
                    ? $"{Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")}_{Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")}"
                    : $"{Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")}_{TelemetryClient.Context.Cloud.RoleInstance}";

            if (string.IsNullOrWhiteSpace(TelemetryClient.Context.Cloud.RoleName))
            {
                TelemetryClient.Context.Cloud.RoleName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            }

            TelemetryClient.Context.Component.Version = Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString();
            TelemetryClient.Context.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");

            TelemetryClient.TrackEvent("ColdStart", new Dictionary<string, string>
            {
                {"WEBSITE_INSTANCE_ID", Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")},
                {"WEBSITE_SITE_NAME", Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")},
                {"WEBSITE_SKU", Environment.GetEnvironmentVariable("WEBSITE_SKU")},
                {"WEBSITE_COMPUTE_MODE", Environment.GetEnvironmentVariable("WEBSITE_COMPUTE_MODE")},
            });

            TelemetryClient.TrackTrace($"ColdStart - {Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")}",
                SeverityLevel.Information,
                new Dictionary<string, string>
                {
                    {"WEBSITE_INSTANCE_ID", Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")},
                    {"WEBSITE_SITE_NAME", Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")},
                    {"WEBSITE_SKU", Environment.GetEnvironmentVariable("WEBSITE_SKU")},
                    {"WEBSITE_COMPUTE_MODE", Environment.GetEnvironmentVariable("WEBSITE_COMPUTE_MODE")},
                });
        }

        [FunctionName("Level1")]
        public static async Task<HttpResponseMessage> RunLevel1(
            CancellationToken ct,
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/Level1")] HttpRequestMessage req,
            ExecutionContext ctx,
            ILogger log)
        {
            TraceExecution(ctx, log);
            var level2Result = await Client.GetAsync(new Uri($"http://{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/api/Level2"), ct);
            var level2Content = JsonConvert.DeserializeObject<string>(await level2Result.Content.ReadAsStringAsync());
            return req.CreateResponse(HttpStatusCode.OK, $"L1_{Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")}__{level2Content}");
        }

        [FunctionName("Level2")]
        public static async Task<HttpResponseMessage> RunLevel2(
            CancellationToken ct,
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/Level2")] HttpRequestMessage req,
            ExecutionContext ctx,
            ILogger log)
        {
            TraceExecution(ctx, log);
            var level3Result = await Client.GetAsync(new Uri($"http://{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/api/Level3"), ct);
            var level3Content = JsonConvert.DeserializeObject<string>(await level3Result.Content.ReadAsStringAsync());
            return req.CreateResponse(HttpStatusCode.OK, $"L2_{Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")}__{level3Content}");
        }

        [FunctionName("Level3")]
        public static HttpResponseMessage RunLevel3(
            CancellationToken ct,
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/Level3")] HttpRequestMessage req,
            ExecutionContext ctx,
            ILogger log)
        {
            TraceExecution(ctx, log);
            return req.CreateResponse(HttpStatusCode.OK, $"L3_{Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")}");
        }

        [FunctionName("Ping")]
        public static HttpResponseMessage Ping(
            CancellationToken ct,
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Monitoring/Ping")] HttpRequestMessage req,
            ExecutionContext ctx,
            ILogger log)
        {
            TraceExecution(ctx, log);
            return req.CreateResponse(HttpStatusCode.OK, $"{Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")}");
        }


        [FunctionName("KeepAlive")]
        public static void RunKeepAlive([TimerTrigger("0 */1 * * * *")]TimerInfo timer, ExecutionContext ctx,  ILogger log)
        {
            TraceExecution(ctx, log);
        }

        private static void TraceExecution(ExecutionContext ctx, ILogger log)
        {
            var message = $"LOG_{ctx.FunctionName} - {Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")}";
            var trace = new TraceTelemetry(message)
            {
                SeverityLevel = SeverityLevel.Information,
                Properties =
                {
                    {"WEBSITE_INSTANCE_ID", Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? string.Empty},
                    {"WEBSITE_SITE_NAME", Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? string.Empty},
                    {"WEBSITE_SKU", Environment.GetEnvironmentVariable("WEBSITE_SKU") ?? string.Empty},
                    {"WEBSITE_COMPUTE_MODE", Environment.GetEnvironmentVariable("WEBSITE_COMPUTE_MODE") ?? string.Empty},
                    {"FUNCTION_NAME", ctx.FunctionName ?? string.Empty},
                },
            };
            trace.Context.Operation.Id = ctx.InvocationId.ToString();
            trace.Context.Operation.Name = ctx.FunctionName;
            trace.Context.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
            TelemetryClient.TrackTrace(trace);
            var newMessage = $"{message} -- {string.Join("--", trace.Properties.OrderBy(p => p.Key).Select(p => $"{p.Key}:{{{p.Key}}}").ToArray())}";
            log.LogInformation(newMessage, trace.Properties.OrderBy(p => p.Key).Select(p => p.Value).Cast<object>().ToArray());
        }
    }
}

