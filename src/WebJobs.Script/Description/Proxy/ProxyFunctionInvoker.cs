// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.AppService.AdvancedRouting.Gateway.Client;

namespace Microsoft.Azure.WebJobs.Script.Description
{
    internal class ProxyFunctionInvoker : FunctionInvokerBase
    {
        private IProxyClient _proxyClient;

        public ProxyFunctionInvoker(ScriptHost host, FunctionMetadata functionMetadata, IProxyClient proxyClient) : base(host, functionMetadata)
        {
            _proxyClient = proxyClient;
        }

        protected override async Task InvokeCore(object[] parameters, FunctionInvocationContext context)
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();

            // TODO: temp
            arguments.Add(ScriptConstants.AzureFunctionsProxyHttpRequestKey, parameters[0]);

            await _proxyClient.CallAsync(arguments, null, context.Logger);
        }
    }
}
