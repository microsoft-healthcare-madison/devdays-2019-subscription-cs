// <copyright file="NotificationController.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace devdays_2019_subscription_cs.Controllers
{
    /// <summary>
    /// A controller for handling Subscription notifications Responds to: POST:   /notification.
    /// </summary>
    [Produces("application/json")]

    public class NotificationController : Controller
    {
        private static int _notificationCount;

        /// <summary>The configuration. </summary>
        private readonly IConfiguration _config;

        /// <summary>Static constructor.</summary>
        static NotificationController()
        {
            _notificationCount = 0;
        }

        /// <summary>Constructor.</summary>
        /// <param name="iConfiguration">Reference to the injected configuration object.</param>
        public NotificationController(IConfiguration iConfiguration)
        {
            // grab a reference to our application configuration
            _config = iConfiguration;
        }

        /// <summary>(An Action that handles HTTP POST requests) posts to a notification endpoint</summary>
        /// <returns>An IActionResult.</returns>

        [HttpPost]
        [Route("/notification")]
        [Consumes("application/fhir+json", new[] { "application/json" })]
        public virtual IActionResult PostEventToEndpointByUid()
        {
            // notify user
            Console.WriteLine($"Received notification:");

            try
            {
                // read our content
                using (StreamReader reader = new StreamReader(Request.Body))
                {
                    string content = reader.ReadToEnd();

                    // parse the content into a bundle
                    fhir.Bundle bundle = JsonConvert.DeserializeObject<fhir.Bundle>(content, new fhir.ResourceConverter());

                    if (bundle.Type != fhir.BundleTypeCodes.SUBSCRIPTION_NOTIFICATION)
                    {
                        Console.WriteLine($"Invalid notification bundle.type: {bundle.Type}");
                        return StatusCode(500);
                    }

                    if ((bundle.Entry == null) || (bundle.Entry.Length == 0))
                    {
                        Console.WriteLine("Invalid notification MUST contain bundle.entry");
                        return StatusCode(500);
                    }

                    if (bundle.Entry[0].Resource == null)
                    {
                        Console.WriteLine("Invalid notification, bundle.entry[0].resource MUST be a SubscriptionStatus.");
                        return StatusCode(500);
                    }

                    fhir.SubscriptionStatus subscriptionStatus = (fhir.SubscriptionStatus)bundle.Entry[0].Resource;

                    // check for being a handshake
                    if (subscriptionStatus.NotificationType == fhir.SubscriptionStatusNotificationTypeCodes.HANDSHAKE)
                    {
                        Console.WriteLine($"Handshake:\n" +
                            $"\tSubscription:      {subscriptionStatus.Subscription.reference}\n" +
                            $"\tSubscriptionTopic: {subscriptionStatus.Topic.reference}\n" +
                            $"\tStatus:            {subscriptionStatus.Status}"
                            );
                    }
                    else
                    {
                        Console.WriteLine($"Notification {subscriptionStatus.EventsSinceSubscriptionStart}:\n" +
                            $"\tSubscription:      {subscriptionStatus.Subscription.reference}\n" +
                            $"\tSubscriptionTopic: {subscriptionStatus.Topic.reference}\n" +
                            $"\tType:              {subscriptionStatus.NotificationType}\n" +
                            $"\tStatus:            {subscriptionStatus.Status}\n" +
                            $"\tTotal Events:      {subscriptionStatus.EventsSinceSubscriptionStart}\n" +
                            $"\tBundle Events:     {subscriptionStatus.EventsInNotification}"
                            );
                    }

                    // increment our received notification count
                    _notificationCount++;

                    // check for being done
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
    }
}
