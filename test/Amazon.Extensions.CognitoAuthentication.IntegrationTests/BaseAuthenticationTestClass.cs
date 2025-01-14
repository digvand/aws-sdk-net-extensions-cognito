﻿/*
 * Copyright 2018 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 * 
 *  http://aws.amazon.com/apache2.0
 * 
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using System;
using System.Collections.Generic;

using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Extensions.CognitoAuthentication.Util;

namespace Amazon.Extensions.CognitoAuthentication.IntegrationTests
{
    /// <summary>
    /// Base class to be used for authentication integrations tests
    /// Allows for child classes to create, sign up, or confirm users
    /// </summary>
    public partial class BaseAuthenticationTestClass : IDisposable
    {
        protected AmazonCognitoIdentityProviderClient provider;
        protected CognitoUserPool pool;
        protected CognitoUser user;

        static BaseAuthenticationTestClass()
        {
            AWSConfigs.RegionEndpoint = RegionEndpoint.USEast1;
        }

        public BaseAuthenticationTestClass()
        {
            var passwordPolicy = new UserPoolPolicyType();
            var requiredAttributes = new List<SchemaAttributeType>();
            var verifiedAttributes = new List<string>();

            provider = new AmazonCognitoIdentityProviderClient();

            var adminCreateUser = new AdminCreateUserConfigType()
            {
                UnusedAccountValidityDays = 8,
                AllowAdminCreateUserOnly = false
            };

            passwordPolicy.PasswordPolicy = new PasswordPolicyType()
            {
                MinimumLength = 8,
                RequireNumbers = true,
                RequireSymbols = true,
                RequireUppercase = true,
                RequireLowercase = true
            };

            var tempSchema = new SchemaAttributeType()
            {
                Required = true,
                Name = CognitoConstants.UserAttrEmail,
                AttributeDataType = AttributeDataType.String
            };
            requiredAttributes.Add(tempSchema);
            verifiedAttributes.Add(CognitoConstants.UserAttrEmail);

            var createPoolRequest = new CreateUserPoolRequest
            {
                PoolName = "testPool_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                Policies = passwordPolicy,
                Schema = requiredAttributes,
                AdminCreateUserConfig = adminCreateUser,
                MfaConfiguration = "OFF",
                AutoVerifiedAttributes = verifiedAttributes,
                DeviceConfiguration = new DeviceConfigurationType()
                {
                    ChallengeRequiredOnNewDevice = false,
                    DeviceOnlyRememberedOnUserPrompt = false
                }
            };
            var createPoolResponse = provider.CreateUserPoolAsync(createPoolRequest).Result;
            var userPoolCreated = createPoolResponse.UserPool;

            var clientRequest = new CreateUserPoolClientRequest()
            {
                ClientName = "App_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                UserPoolId = userPoolCreated.Id,
                GenerateSecret = false,

            };
            var clientResponse = provider.CreateUserPoolClientAsync(clientRequest).Result;
            var clientCreated = clientResponse.UserPoolClient;

            pool = new CognitoUserPool(userPoolCreated.Id, clientCreated.ClientId, provider, "");
        }

        /// <summary>
        /// Internal method that cleans up the created user pool (along with associated client/user) 
        /// for testing
        /// </summary>
        public virtual void Dispose()
        {
            try
            {
                provider.DeleteUserPoolAsync(new DeleteUserPoolRequest()
                {
                    UserPoolId = pool.PoolID
                }).Wait();

                provider.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}
