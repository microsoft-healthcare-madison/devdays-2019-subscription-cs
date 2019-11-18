using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using fhir;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace devdays_2019_subscription_cs
{
    class Program
    {
        /// <summary>A Regex pattern to filter proper base URLs for WebHost.</summary>
        private const string _regexBaseUrlMatch = @"(http[s]*:\/\/[A-Za-z0-9\.]*(:\d+)*)";

        private static HttpClient _restClient;
        
        private static CamelCasePropertyNamesContractResolver _contractResolver;

        ///-------------------------------------------------------------------------------------------------
        /// <summary>Gets or sets the configuration.</summary>
        ///
        /// <value>The configuration.</value>
        ///-------------------------------------------------------------------------------------------------

        public static IConfiguration Configuration { get; set; }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>   Main entry-point for this application. </summary>
        ///
        /// <remarks>   Gino Canessa, 11/18/2019. </remarks>
        ///
        /// <param name="args"> The arguments. </param>
        ///-------------------------------------------------------------------------------------------------

        public static void Main(string[] args)
        {
            // **** setup our configuration (command line > environment > appsettings.json) ****

            Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build()
                ;

            // **** update configuration to make sure listen url is properly formatted ****

            Regex regex = new Regex(_regexBaseUrlMatch);
            Match match = regex.Match(Configuration["Basic_Internal_Url"]);
            Configuration["Basic_Internal_Url"] = match.ToString();

            // **** create our rest client ****

            _restClient = new HttpClient();

            // **** configure serialization ****

            _contractResolver = new CamelCasePropertyNamesContractResolver();

            // **** create our web host ****

            (new Thread(() =>
            {
                CreateWebHostBuilder(args).Build().Run();
            })).Start();
            

            // **** start our process ****

            StartProcessing();
        }

        public static async void StartProcessing()
        {
            // **** get a list of topics ****

            List<fhir.Topic> topics = GetTopics();

            if ((topics == null) ||
                (topics.Count == 0))
            {
                Console.WriteLine("No topics found!");
                System.Environment.Exit(1);
            }

            // **** list topics in the console ****

            Console.WriteLine("Found Topics:");
            foreach (Topic topic in topics)
            {
                Console.WriteLine($" Topic/{topic.Id}:");
                Console.WriteLine(JsonConvert.SerializeObject(
                    topic,
                    Formatting.Indented,
                    new JsonSerializerSettings()
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        ContractResolver = _contractResolver,
                    }));
            }
        }

        public static List<fhir.Topic> GetTopics()
        {
            List<fhir.Topic> topics = new List<Topic>();

            // **** try to get a list of topics ****

            try
            {
                // **** build our request ****

                HttpRequestMessage request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = GetFhirUri("Topic"),
                    Headers =
                    {
                        Accept =
                        {
                            new MediaTypeWithQualityHeaderValue("application/fhir+json")
                        },
                    }
                };

                // **** make our request ****

                HttpResponseMessage response = _restClient.SendAsync(request).Result;

                // **** check the status code ****

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($" Could not get Topics: {request.RequestUri.ToString()} returned: {response.StatusCode}");
                    return topics;
                }

                string content = response.Content.ReadAsStringAsync().Result;

                // **** deserialize ****

                fhir.Bundle bundle = JsonConvert.DeserializeObject<fhir.Bundle>(content);

                // **** check for values ****

                if ((bundle.Entry == null) ||
                    (bundle.Entry.Length == 0))
                {
                    return topics;
                }

                // **** traverse topics ****

                foreach (fhir.BundleEntry entry in bundle.Entry)
                {
                    if (entry.Resource == null)
                    {
                        continue;
                    }
                    
                    topics.Add(((JObject)entry.Resource).ToObject<fhir.Topic>());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to Get Topics: {ex.Message}");
                return topics;
            }

            // **** return our list ****

            return topics;
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>   Gets fhir URI. </summary>
        ///
        /// <remarks>   Gino Canessa, 11/18/2019. </remarks>
        ///
        /// <param name="resource"> (Optional) The resource. </param>
        ///
        /// <returns>   The fhir URI. </returns>
        ///-------------------------------------------------------------------------------------------------

        private static Uri GetFhirUri(string resource = "")
        {
            if (string.IsNullOrEmpty(resource))
            {
                return new Uri(Configuration["Basic_Fhir_Server_Url"]);
            }

            return new Uri(new Uri(Configuration["Basic_Fhir_Server_Url"]), resource);
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>   Creates web host builder. </summary>
        ///
        /// <remarks>   Gino Canessa, 11/18/2019. </remarks>
        ///
        /// <param name="args"> The arguments. </param>
        ///
        /// <returns>   The new web host builder. </returns>
        ///-------------------------------------------------------------------------------------------------

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseUrls(Configuration["Basic_Internal_Url"])
                .UseKestrel()
                .UseStartup<Startup>()
                ;
    }
}
