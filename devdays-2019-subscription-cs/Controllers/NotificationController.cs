using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace devdays_2019_subscription_cs.Controllers
{
    ///-------------------------------------------------------------------------------------------------
    /// <summary>A controller for handling subcription notifications
    /// Responds to:
    ///     POST:   /notification
    /// </summary>
    ///
    /// <remarks>Gino Canessa, 11/18/2019.</remarks>
    ///-------------------------------------------------------------------------------------------------
    [Produces("application/json")]

    public class NotificationController : Controller
    {
        #region Class Variables . . .

        private static int _notificationCount;

        #endregion Class Variables . . .

        #region Instance Variables . . .

        /// <summary>   The configuration. </summary>
        private readonly IConfiguration _config;

        #endregion Instance Variables . . .

        #region Constructors . . .

        ///-------------------------------------------------------------------------------------------------
        /// <summary>Static constructor.</summary>
        ///
        /// <remarks>Gino Canessa, 11/18/2019.</remarks>
        ///-------------------------------------------------------------------------------------------------

        static NotificationController()
        {
            _notificationCount = 0;
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>Constructor.</summary>
        ///
        /// <remarks>Gino Canessa, 11/18/2019.</remarks>
        ///
        /// <param name="iConfiguration">Reference to the injected configuration object</param>
        ///-------------------------------------------------------------------------------------------------

        public NotificationController(
                                        IConfiguration iConfiguration
                                        )
        {
            // **** grab a reference to our application configuration ****

            _config = iConfiguration;
        }

        #endregion Constructors . . .

        #region Class Interface . . .

        #endregion Class Interface . . .

        #region Instance Interface . . .
        
        ///-------------------------------------------------------------------------------------------------
        /// <summary>(An Action that handles HTTP POST requests) posts to a notification endpoint</summary>
        ///
        /// <remarks>Gino Canessa, 11/18/2019.</remarks>
        ///
        /// <returns>An IActionResult.</returns>
        ///-------------------------------------------------------------------------------------------------

        [HttpPost]
        [Route("/notification")]
        [Consumes("application/fhir+json", new[] { "application/json" })]
        public virtual IActionResult PostEventToEndpointByUid()
        {
            // **** notify user ****

            Console.WriteLine($"Received notification:");

            try
            {
                // **** read our content ****

                using (StreamReader reader = new StreamReader(Request.Body))
                {
                    string content = reader.ReadToEnd();

                    // **** parse the content into a bundle ****

                    fhir.Bundle bundle = JsonConvert.DeserializeObject<fhir.Bundle>(content);

                    // **** grab the fields we need out of the notification ****

                    long eventCount = -1;
                    int bundleEventCount = -1;
                    string status = "";
                    string topicUrl = "";
                    string subscriptionUrl = "";


                    if ((bundle != null) &&
                        (bundle.Meta != null) &&
                        (bundle.Meta.Extension != null))
                    {
                        // **** loop over extensions ****

                        foreach (fhir.Extension element in bundle.Meta.Extension)
                        {
                            if (element.Url.EndsWith("subscription-event-count"))
                            {
                                if (!Int64.TryParse(element.ValueInteger64, out eventCount))
                                {
                                    eventCount = -1;
                                }
                            }
                            else if (element.Url.EndsWith("bundle-event-count"))
                            {
                                bundleEventCount = (int)element.ValueUnsignedInt;
                            }
                            else if (element.Url.EndsWith("subscription-status"))
                            {
                                status = element.ValueString;
                            }
                            else if (element.Url.EndsWith("subscription-topic-url"))
                            {
                                topicUrl = element.ValueUrl;
                            }
                            else if (element.Url.EndsWith("subscription-url"))
                            {
                                subscriptionUrl = element.ValueUrl;
                            }
                        }
                    }

                    // **** check for being a handshake ****

                    if (eventCount == 0)
                    {
                        Console.WriteLine($"Handshake:\n" +
                            $"\tTopic:         {topicUrl}\n" +
                            $"\tSubscription:  {subscriptionUrl}\n" +
                            $"\tStatus:        {status}"
                            );
                    }
                    else
                    {
                        Console.WriteLine($"Notification {eventCount}:\n" +
                            $"\tTopic:         {topicUrl}\n" +
                            $"\tSubscription:  {subscriptionUrl}\n" +
                            $"\tStatus:        {status}\n" +
                            $"\tBundle Events: {bundleEventCount}\n" +
                            $"\tTotal Events:  {eventCount}"
                            );
                    }

                    // **** increment our received notification count ****

                    _notificationCount++;

                    // **** check for being done ****

                    if (_notificationCount == 2)
                    {
                        Program.DeleteSubscription().Wait(2000);
                        Environment.Exit(0);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing notification: {ex.Message}");
                return StatusCode(500);
            }

            // **** flag we accepted ****

            return StatusCode(204);
        }
        
        #endregion Instance Interface . . .

        #region Internal Functions . . .

        #endregion Internal Functions . . .

    }
}
