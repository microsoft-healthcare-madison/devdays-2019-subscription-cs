using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>Identifier for the patient (once created).</summary>
        private static string _patientId;

        /// <summary>Identifier for the subscription (once created).</summary>
        private static string _subscriptionId;

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

        ///-------------------------------------------------------------------------------------------------
        /// <summary>   Starts a processing. </summary>
        ///
        /// <remarks>   Gino Canessa, 11/18/2019. </remarks>
        ///-------------------------------------------------------------------------------------------------

        public static async void StartProcessing()
        {
            // **** get a list of topics ****

            List<fhir.SubscriptionTopic> topics = await GetTopics();

            if ((topics == null) ||
                (topics.Count == 0))
            {
                Console.WriteLine("No SubscriptionTopics found!");
                System.Environment.Exit(1);
            }

            // **** list topics in the console ****

            Console.WriteLine("Found SubscriptionTopics:");
            foreach (fhir.SubscriptionTopic topic in topics)
            {
                Console.WriteLine($" SubscriptionTopic/{topic.Id}:");
                Console.WriteLine(JsonConvert.SerializeObject(
                    topic,
                    Formatting.Indented,
                    new JsonSerializerSettings()
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        ContractResolver = _contractResolver,
                    }));
            }

            // **** make sure our patient exists ****

            if (!(await CreatePatientIfRequired()))
            {
                Console.WriteLine($"Failed to verify patient: Patient/{_patientId}");
                System.Environment.Exit(1);
            }

            // **** create our subscription ****

            if (!(await CreateSubscription(topics[0])))
            {
                Console.WriteLine("Failed to create subscription!");
                Environment.Exit(1);
            }

            await Task.Delay(500);

            // **** post an encounter ****

            if (!(await PostEncounter()))
            {
                Console.WriteLine("Failed to post encounter");
                Environment.Exit(1);
            }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>   Posts the encounter. </summary>
        ///
        /// <remarks>   Gino Canessa, 11/18/2019. </remarks>
        ///
        /// <returns>   An asynchronous result that yields true if it succeeds, false if it fails. </returns>
        ///-------------------------------------------------------------------------------------------------

        public static async Task<bool> PostEncounter()
        {
            try
            {
                fhir.Encounter encounter = new Encounter()
                {
                    Class = v3_ActEncounterCode.VR,
                    Status = EncounterStatusCodes.IN_PROGRESS,
                    Subject = new Reference()
                    {
                        reference = $"Patient/{_patientId}"
                    }
                };

                string serialzed = JsonConvert.SerializeObject(
                    encounter,
                    new JsonSerializerSettings()
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        ContractResolver = _contractResolver,
                    });

                // **** build our request ****

                HttpRequestMessage request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Post,
                    RequestUri = GetFhirUri("Encounter"),
                    Headers =
                    {
                        Accept =
                        {
                            new MediaTypeWithQualityHeaderValue("application/fhir+json")
                        },
                    },
                    Content = new StringContent(
                        serialzed,
                        Encoding.UTF8,
                        "application/fhir+json"
                        ),
                };
                request.Headers.Add("Prefer", "return=representation");


                // **** make our request ****

                HttpResponseMessage response = await _restClient.SendAsync(request);

                // **** check the status code ****

                if ((response.StatusCode != System.Net.HttpStatusCode.OK) &&
                    (response.StatusCode != System.Net.HttpStatusCode.Created) &&
                    (response.StatusCode != System.Net.HttpStatusCode.Accepted) &&
                    (response.StatusCode != System.Net.HttpStatusCode.NoContent))
                {
                    Console.WriteLine($" Could not POST Encounter: {request.RequestUri.ToString()} returned: {response.StatusCode}");
                    return false;
                }

                // **** good here ****

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to Create Encounter: {ex.Message}");
                return false;
            }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>   Creates a subscription. </summary>
        ///
        /// <remarks>   Gino Canessa, 11/18/2019. </remarks>
        ///
        /// <param name="topic">    The topic. </param>
        ///
        /// <returns>   An asynchronous result that yields true if it succeeds, false if it fails. </returns>
        ///-------------------------------------------------------------------------------------------------

        public static async Task<bool> CreateSubscription(fhir.SubscriptionTopic topic)
        {
            try
            {
                string url = string.IsNullOrEmpty(Configuration["Basic_Public_Url"])
                    ? Configuration["Basic_Internal_Url"]
                    : Configuration["Basic_Public_Url"];

                fhir.Subscription subscription = new Subscription() 
                {
                    Channel = new SubscriptionChannel()
                    {
                        Endpoint = url,
                        HeartbeatPeriod = 60,
                        Payload = new SubscriptionChannelPayload()
                        {
                            Content = SubscriptionChannelPayloadContentCodes.ID_ONLY,
                            ContentType = "application/fhir+json",
                        },
                        Type = new CodeableConcept()
                        {
                            Coding = new Coding[]
                            {
                                SubscriptionChannelTypeCodes.rest_hook
                            },
                            Text = "REST Hook"
                        }
                    },
                    FilterBy = new SubscriptionFilterBy[]
                    {
                        new SubscriptionFilterBy()
                        {
                            MatchType = SubscriptionFilterByMatchTypeCodes.EQUALS,
                            Name = "patient",
                            Value = $"Patient/{_patientId}"
                        },
                    },
                    Topic = new Reference()
                    {
                        reference = topic.Url
                    },
                    Reason = "DevDays Example - C#",
                    Status = SubscriptionStatusCodes.REQUESTED,
                };

                string serialzed = JsonConvert.SerializeObject(
                    subscription,
                    new JsonSerializerSettings()
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        ContractResolver = _contractResolver,
                    });

                // **** build our request ****

                HttpRequestMessage request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Post,
                    RequestUri = GetFhirUri("Subscription"),
                    Headers =
                    {
                        Accept =
                        {
                            new MediaTypeWithQualityHeaderValue("application/fhir+json")
                        },
                    },
                    Content = new StringContent(
                        serialzed,
                        Encoding.UTF8,
                        "application/fhir+json"
                        ),
                };
                request.Headers.Add("Prefer", "return=representation");

                // **** make our request ****

                HttpResponseMessage response = await _restClient.SendAsync(request);

                // **** check the status code ****

                if ((response.StatusCode != System.Net.HttpStatusCode.OK) &&
                    (response.StatusCode != System.Net.HttpStatusCode.Created) &&
                    (response.StatusCode != System.Net.HttpStatusCode.Accepted) &&
                    (response.StatusCode != System.Net.HttpStatusCode.NoContent))
                {
                    Console.WriteLine($" Could not Post Subscription: {request.RequestUri.ToString()} returned: {response.StatusCode}");
                    return false;
                }

                // **** parse the return ****

                string body = await response.Content.ReadAsStringAsync();

                fhir.Subscription sub = JsonConvert.DeserializeObject<fhir.Subscription>(body);

                // **** grab the subscription id so we can clean up ****

                _subscriptionId = sub.Id;

                // **** good here ****

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to Create Subscription: {ex.Message}");
                return false;
            }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>Deletes the subscription.</summary>
        ///
        /// <remarks>Gino Canessa, 11/19/2019.</remarks>
        ///
        /// <returns>An asynchronous result that yields true if it succeeds, false if it fails.</returns>
        ///-------------------------------------------------------------------------------------------------

        public static async Task<bool> DeleteSubscription()
        {
            try
            {
                // **** build our request ****

                HttpRequestMessage request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Delete,
                    RequestUri = GetFhirUri($"Subscription/{_subscriptionId}"),
                    Headers =
                    {
                        Accept =
                        {
                            new MediaTypeWithQualityHeaderValue("application/fhir+json")
                        },
                    },
                };

                // **** make our request ****

                HttpResponseMessage response = await _restClient.SendAsync(request);

                // **** check the status code ****

                if ((response.StatusCode != System.Net.HttpStatusCode.OK) &&
                    (response.StatusCode != System.Net.HttpStatusCode.Accepted) &&
                    (response.StatusCode != System.Net.HttpStatusCode.NoContent))
                {
                    Console.WriteLine($" Could not Delete Subscription: {request.RequestUri.ToString()} returned: {response.StatusCode}");
                    return false;
                }

                Console.WriteLine($"Deleted Subscription: Subscription/{_subscriptionId}");

                // **** good here ****

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to Delete Subscription: {ex.Message}");
                return false;
            }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>   Creates patient if required. </summary>
        ///
        /// <remarks>   Gino Canessa, 11/18/2019. </remarks>
        ///
        /// <returns>   An asynchronous result that yields true if it succeeds, false if it fails. </returns>
        ///-------------------------------------------------------------------------------------------------

        public static async Task<bool> CreatePatientIfRequired()
        {
            // **** try to get a our patient ****

            try
            {
                // **** create a patient id ****

                _patientId = Guid.NewGuid().ToString();

                // **** build our request ****

                HttpRequestMessage request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = GetFhirUri($"Patient/{_patientId}"),
                    Headers =
                    {
                        Accept =
                        {
                            new MediaTypeWithQualityHeaderValue("application/fhir+json")
                        },
                    }
                };

                // **** make our request ****

                HttpResponseMessage response = await _restClient.SendAsync(request);

                // **** check the status code ****

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return (await CreatePatient());
                }

                string content = await response.Content.ReadAsStringAsync();

                // **** deserialize ****

                fhir.Bundle bundle = JsonConvert.DeserializeObject<fhir.Bundle>(content);

                // **** check for values ****

                if ((bundle.Entry == null) ||
                    (bundle.Entry.Length == 0))
                {
                    return (await CreatePatient());
                }

                // **** good here ****

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to Get Patient/{_patientId}: {ex.Message}");
                return (await CreatePatient());
            }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>   Creates the patient. </summary>
        ///
        /// <remarks>   Gino Canessa, 11/18/2019. </remarks>
        ///
        /// <returns>   An asynchronous result that yields true if it succeeds, false if it fails. </returns>
        ///-------------------------------------------------------------------------------------------------

        public static async Task<bool> CreatePatient()
        {
            // **** try to get a our patient ****

            try
            {
                fhir.Patient patient = new Patient()
                {
                    Id = _patientId,
                    Name = new fhir.HumanName[]
                    {
                        new fhir.HumanName()
                        {
                            Family = "Patient",
                            Given = new string[]{"DevDays"},
                            Use = HumanNameUseCodes.OFFICIAL,
                        }
                    },
                    Gender = PatientGenderCodes.UNKNOWN,
                    BirthDate = "2019-11-20"
                };

                string serialzed = JsonConvert.SerializeObject(
                    patient,
                    new JsonSerializerSettings()
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        ContractResolver = _contractResolver,
                    });

                // **** build our request ****

                HttpRequestMessage request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Put,
                    RequestUri = GetFhirUri($"Patient/{_patientId}"),
                    Headers =
                    {
                        Accept =
                        {
                            new MediaTypeWithQualityHeaderValue("application/fhir+json")
                        },
                    },
                    Content = new StringContent(
                        serialzed,
                        Encoding.UTF8,
                        "application/fhir+json"
                        ),
                };

                // **** make our request ****

                HttpResponseMessage response = await _restClient.SendAsync(request);

                // **** check the status code ****

                if ((response.StatusCode != System.Net.HttpStatusCode.OK) &&
                    (response.StatusCode != System.Net.HttpStatusCode.Created) &&
                    (response.StatusCode != System.Net.HttpStatusCode.Accepted) &&
                    (response.StatusCode != System.Net.HttpStatusCode.NoContent))
                {
                    Console.WriteLine($" Could not PUT Patient: {request.RequestUri.ToString()} returned: {response.StatusCode}");
                    return false;
                }
                
                // **** good here ****

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to Create Patient/{_patientId}: {ex.Message}");
                return false;
            }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>   Gets the topics. </summary>
        ///
        /// <remarks>   Gino Canessa, 11/18/2019. </remarks>
        ///
        /// <returns>   The topics. </returns>
        ///-------------------------------------------------------------------------------------------------

        public static async Task<List<fhir.SubscriptionTopic>> GetTopics()
        {
            List<fhir.SubscriptionTopic> topics = new List<fhir.SubscriptionTopic>();

            // **** try to get a list of topics ****

            try
            {
                // **** build our request ****

                HttpRequestMessage request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = GetFhirUri("SubscriptionTopic"),
                    Headers =
                    {
                        Accept =
                        {
                            new MediaTypeWithQualityHeaderValue("application/fhir+json")
                        },
                    }
                };

                // **** make our request ****

                HttpResponseMessage response = await _restClient.SendAsync(request);

                // **** check the status code ****

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($" Could not get SubscriptionTopics: {request.RequestUri.ToString()} returned: {response.StatusCode}");
                    return topics;
                }

                string content = await response.Content.ReadAsStringAsync();

                // **** deserialize ****

                fhir.Bundle bundle = JsonConvert.DeserializeObject<fhir.Bundle>(content, new fhir.ResourceConverter());

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

                    // **** add this topic (should error check here) ****
                    
                    topics.Add((fhir.SubscriptionTopic)entry.Resource);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to Get SubscriptionTopics: {ex.Message}");
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
