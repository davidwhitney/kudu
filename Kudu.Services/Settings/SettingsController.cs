﻿using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Settings;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Settings
{
    public class SettingsController : ApiController
    {
        private const string DeploymentSettingsSection = "deployment";
        private readonly IDeploymentSettingsManager _settingsManager;

        public SettingsController(IDeploymentSettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }

        /// <summary>
        /// Create or change some settings
        /// </summary>
        /// <param name="newSettings">The object containing the new settings</param>
        /// <returns></returns>
        public HttpResponseMessage Set(JObject newSettings)
        {
            if (newSettings == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            // We support two formats here:
            // 1. For backward compat, we support {key: 'someKey', value: 'someValue' }
            // 2. The preferred format is { someKey = 'someValue' }
            // Note that #2 allows multiple settings to be set, e.g. { someKey = 'someValue', someKey2 = 'someValue2' }


            JToken keyToken, valueToken;
            if (newSettings.Count == 2 && newSettings.TryGetValue("key", out keyToken) && newSettings.TryGetValue("value", out valueToken))
            {
                string key = keyToken.Value<string>();

                if (String.IsNullOrEmpty(key))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest);
                }

                _settingsManager.SetValue(key, valueToken.Value<string>());
            }
            else
            {
                foreach (var keyValuePair in newSettings)
                {
                    _settingsManager.SetValue(keyValuePair.Key, keyValuePair.Value.Value<string>());
                }
            }

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Delete a setting
        /// </summary>
        /// <param name="key">The key of the setting to delete</param>
        /// <returns></returns>
        public HttpResponseMessage Delete(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            _settingsManager.DeleteValue(key);

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Get the list of all settings
        /// </summary>
        /// <returns></returns>
        public HttpResponseMessage GetAll()
        {
            var values = _settingsManager.GetValues();

            return Request.CreateResponse(HttpStatusCode.OK, values);
        }

        /// <summary>
        /// Get the value of a setting
        /// </summary>
        /// <param name="key">The setting's key</param>
        /// <returns></returns>
        public HttpResponseMessage Get(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            string value = _settingsManager.GetValue(key);

            if (value == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, String.Format(Resources.SettingDoesNotExist, key));
            }

            return Request.CreateResponse(HttpStatusCode.OK, value);
        }
    }
}
