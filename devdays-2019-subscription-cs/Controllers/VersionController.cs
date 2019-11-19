using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace devdays_2019_subscription_cs.Controllers
{
    ///-------------------------------------------------------------------------------------------------
    /// <summary>A controller processing version requests.
    /// Responds to:
    ///     GET:    /api/
    ///     GET:    /api/version/
    /// </summary>
    ///
    /// <remarks>Gino Canessa, 11/18/2019.</remarks>
    ///-------------------------------------------------------------------------------------------------

    [Produces("application/json")]
    public class VersionController : Controller
    {
        #region Class Constants . . .

        private const string _configPrefix = "Basic_";

        #endregion Class Constants . . .

        #region Private Classes . . .

        private class RouteInfo
        {
            public string FunctionName { get; set; }
            public string ControllerName { get; set; }
            public string UriTemplate { get; set; }
        }

        #endregion Private Classes . . 

        #region Class Variables . . .

        #endregion Class Variables . . .

        #region Instance Variables . . .

        /// <summary>   The configuration. </summary>
        private readonly IConfiguration _config;

        private readonly IActionDescriptorCollectionProvider _provider;

        #endregion Instance Variables . . .

        #region Constructors . . .

        ///-------------------------------------------------------------------------------------------------
        /// <summary>Static constructor.</summary>
        ///
        /// <remarks>Gino Canessa, 11/18/2019.</remarks>
        ///-------------------------------------------------------------------------------------------------

        static VersionController()
        {
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>Constructor.</summary>
        ///
        /// <remarks>Gino Canessa, 11/18/2019.</remarks>
        ///
        /// <param name="iConfiguration">Reference to the injected configuration object</param>
        /// <param name="provider">      The provider.</param>
        ///-------------------------------------------------------------------------------------------------

        public VersionController(
                                IConfiguration iConfiguration,
                                IActionDescriptorCollectionProvider provider
                                )
        {
            // **** grab a reference to our application configuration ****

            _config = iConfiguration;
            _provider = provider;
        }

        #endregion Constructors . . .

        #region Class Interface . . .

        #endregion Class Interface . . .

        #region Instance Interface . . .

        ///-------------------------------------------------------------------------------------------------
        /// <summary>(An Action that handles HTTP GET requests) gets version information.</summary>
        ///
        /// <remarks>Gino Canessa, 11/18/2019.</remarks>
        ///
        /// <returns>The version information.</returns>
        ///-------------------------------------------------------------------------------------------------

        [HttpGet]
        [Route("/api/")]
        [Route("/api/version/")]
        public virtual IActionResult GetVersionInfo()
        {
            // **** create a basic tuple to return ****

            Dictionary<string, string> information = new Dictionary<string, string>();

            information.Add("Application", AppDomain.CurrentDomain.FriendlyName);
            information.Add("Runtime", Environment.Version.ToString());

            // **** get the file version of the assembly that launched us ****

            information.Add("Version", FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).FileVersion.ToString());

            // **** add the list of configuration keys and values ****

            IEnumerable<IConfigurationSection> configItems = _config.GetChildren();

            foreach (IConfigurationSection configItem in configItems)
            {
                if (configItem.Key.StartsWith(_configPrefix, StringComparison.Ordinal))
                {
                    information.Add(configItem.Key, configItem.Value);
                }
            }

            // **** try to get a list of routes ****

            try
            {
                List<RouteInfo> routes = _provider.ActionDescriptors.Items.Select(x => new RouteInfo()
                {
                    FunctionName = x.RouteValues["Action"],
                    ControllerName = x.RouteValues["Controller"],
                    UriTemplate = x.AttributeRouteInfo.Template
                })
                    .ToList();

                // *** add to our return list ****

                information.Add("Routes", JsonConvert.SerializeObject(routes));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            // **** return our information ****

            return StatusCode(200, information);
        }

        #endregion Instance Interface . . .

        #region Internal Functions . . .

        #endregion Internal Functions . . .


    }
}
