﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Script.WebHost.Security;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class AuthorizationLevelAttribute : AuthorizationFilterAttribute
    {
        public const string FunctionsKeyHeaderName = "x-functions-key";

        public AuthorizationLevelAttribute(AuthorizationLevel level)
        {
            Level = level;
        }

        public AuthorizationLevel Level { get; }

        public async override Task OnAuthorizationAsync(HttpActionContext actionContext, CancellationToken cancellationToken)
        {
            if (actionContext == null)
            {
                throw new ArgumentNullException("actionContext");
            }

            AuthorizationLevel requestAuthorizationLevel = actionContext.Request.GetAuthorizationLevel();

            // If the request has not yet been authenticated, authenticate it
            var request = actionContext.Request;
            if (requestAuthorizationLevel == AuthorizationLevel.Anonymous)
            {
                // determine the authorization level for the function and set it
                // as a request property
                var secretManager = actionContext.ControllerContext.Configuration.DependencyResolver.GetService<ISecretManager>();

                var result = await GetAuthorizationResultAsync(request, secretManager, EvaluateKeyMatch);
                requestAuthorizationLevel = result.AuthorizationLevel;
                request.SetAuthorizationLevel(result.AuthorizationLevel);
                request.SetProperty(ScriptConstants.AzureFunctionsHttpRequestKeyNameKey, result.KeyName);
            }

            if (request.IsAuthDisabled() ||
                SkipAuthorization(actionContext) ||
                Level == AuthorizationLevel.Anonymous)
            {
                return;
            }

            if (requestAuthorizationLevel < Level)
            {
                actionContext.Response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
        }

        protected virtual string EvaluateKeyMatch(IDictionary<string, string> secrets, string keyValue) => GetKeyMatchOrNull(secrets, keyValue);

        internal static Task<KeyAuthorizationResult> GetAuthorizationResultAsync(HttpRequestMessage request, ISecretManager secretManager, string functionName = null)
        {
            return GetAuthorizationResultAsync(request, secretManager, GetKeyMatchOrNull, functionName);
        }

        internal static async Task<KeyAuthorizationResult> GetAuthorizationResultAsync(HttpRequestMessage request, ISecretManager secretManager,
            Func<IDictionary<string, string>, string, string> matchEvaluator, string functionName = null)
        {
            // first see if a key value is specified via headers or query string (header takes precedence)
            IEnumerable<string> values;
            string keyValue = null;
            if (request.Headers.TryGetValues(FunctionsKeyHeaderName, out values))
            {
                keyValue = values.FirstOrDefault();
            }
            else
            {
                var queryParameters = request.GetQueryParameterDictionary();
                queryParameters.TryGetValue("code", out keyValue);
            }

            if (!string.IsNullOrEmpty(keyValue))
            {
                // see if the key specified is the master key
                HostSecretsInfo hostSecrets = await secretManager.GetHostSecretsAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(hostSecrets.MasterKey) &&
                    Key.SecretValueEquals(keyValue, hostSecrets.MasterKey))
                {
                    return new KeyAuthorizationResult(ScriptConstants.DefaultMasterKeyName, AuthorizationLevel.Admin);
                }

                string keyName = matchEvaluator(hostSecrets.SystemKeys, keyValue);
                if (keyName != null)
                {
                    new KeyAuthorizationResult(keyName, AuthorizationLevel.System);
                }

                // see if the key specified matches the host function key
                keyName = matchEvaluator(hostSecrets.SystemKeys, keyValue);
                if (keyName != null)
                {
                    return new KeyAuthorizationResult(keyName, AuthorizationLevel.Function);
                }

                // if there is a function specific key specified try to match against that
                if (functionName != null)
                {
                    IDictionary<string, string> functionSecrets = await secretManager.GetFunctionSecretsAsync(functionName);
                    keyName = matchEvaluator(functionSecrets, keyValue);
                    if (keyName != null)
                    {
                        return new KeyAuthorizationResult(keyName, AuthorizationLevel.Function);
                    }
                }
            }

            return new KeyAuthorizationResult(null, AuthorizationLevel.Anonymous);
        }

        private static string GetKeyMatchOrNull(IDictionary<string, string> secrets, string keyValue)
        {
            if (secrets != null)
            {
                foreach (var pair in secrets)
                {
                    if (Key.SecretValueEquals(pair.Value, keyValue))
                    {
                        return pair.Key;
                    }
                }
            }
            return null;
        }

        internal static bool SkipAuthorization(HttpActionContext actionContext)
        {
            return actionContext.ActionDescriptor.GetCustomAttributes<AllowAnonymousAttribute>().Count > 0
                || actionContext.ControllerContext.ControllerDescriptor.GetCustomAttributes<AllowAnonymousAttribute>().Count > 0;
        }
    }
}