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

                    // 
                }
            }
            catch (Exception)
            {
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
