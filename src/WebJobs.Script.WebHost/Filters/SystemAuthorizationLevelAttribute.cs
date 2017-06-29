// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public sealed class SystemAuthorizationLevelAttribute : AuthorizationLevelAttribute
    {
        public SystemAuthorizationLevelAttribute(string keyName)
            : base(AuthorizationLevel.System)
        {
            KeyName = keyName;
        }

        public string KeyName { get; }

        protected override string EvaluateKeyMatch(IDictionary<string, string> secrets, string keyValue)
            => GetKeyMatchOrNull(secrets, keyValue, KeyName);

        internal static string GetKeyMatchOrNull(IDictionary<string, string> secrets, string keyValue, string keyName)
        {
            if (secrets != null)
            {
                return secrets.Where(kvp => (keyName == null || string.Equals(kvp.Key, keyName, StringComparison.OrdinalIgnoreCase)) && Key.SecretValueEquals(kvp.Value, keyValue)).Select(p => p.Key).FirstOrDefault();
            }
            return null;
        }
    }
}