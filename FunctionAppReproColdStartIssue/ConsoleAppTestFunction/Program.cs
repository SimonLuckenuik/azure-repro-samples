using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ConsoleAppTestFunction
{
    class Program
    {
        static void Main(string[] args)
        {
            var ev = new ManualResetEvent(false);
            //var host = "http://localhost:7071";
            var host = "http://[CHANGEME].azurewebsites.net";
            
            var ping = Task.Run(async () =>
            {
                var pingClient = new HttpClient();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                ev.WaitOne();
                do
                {
                    timer.Restart();
                    var pingResponse = await pingClient.GetAsync(host+ "/Monitoring/Ping");
                    Console.WriteLine($"{DateTime.Now:O}: PING: {timer.Elapsed:G} -- {JsonConvert.DeserializeObject<string>(await pingResponse.Content.ReadAsStringAsync())}");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
                while (true);
            });

            var callApiL1 = Task.Run(async () =>
            {
                var callApiClient = new HttpClient();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                ev.WaitOne();
                do
                {
                    timer.Restart();
                    var apiResponse = await callApiClient.GetAsync(host + "/api/Level1");
                    Console.WriteLine($"{DateTime.Now:O}: API-L1: {timer.Elapsed:G} -- {JsonConvert.DeserializeObject<string>(await apiResponse.Content.ReadAsStringAsync())}");
                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                }
                while (true);
            });


            var callApiL2 = Task.Run(async () =>
            {
                var callApiClient = new HttpClient();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                ev.WaitOne();
                do
                {
                    timer.Restart();
                    var apiResponse = await callApiClient.GetAsync(host + "/api/Level2");
                    Console.WriteLine($"{DateTime.Now:O}: API-L2: {timer.Elapsed:G} -- {JsonConvert.DeserializeObject<string>(await apiResponse.Content.ReadAsStringAsync())}");
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                while (true);
            });

            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            ev.Set();
            Task.WhenAll(ping, callApiL1, callApiL2).Wait();
        }
    }
}
