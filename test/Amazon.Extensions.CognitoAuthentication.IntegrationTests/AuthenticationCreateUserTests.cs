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

using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;

using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Extensions.CognitoAuthentication.Util;

namespace Amazon.Extensions.CognitoAuthentication.IntegrationTests
{
    public class AuthenticationCreateUserTests : BaseAuthenticationTestClass
    {
        public AuthenticationCreateUserTests() : base()
        {
            var createUserRequest = new AdminCreateUserRequest()
            {
                TemporaryPassword = "PassWord1!",
                Username = "User5",
                UserAttributes = new List<AttributeType>()
                {
                    new AttributeType() {Name=CognitoConstants.UserAttrEmail, Value="xxx@yyy.zzz"},
                },
                ValidationData = new List<AttributeType>()
                {
                    new AttributeType() {Name=CognitoConstants.UserAttrEmail, Value="xxx@yyy.zzz"}
                },
                UserPoolId = pool.PoolID
            };

            var createReponse = provider.AdminCreateUserAsync(createUserRequest).Result;

            user = new CognitoUser("User5", pool.ClientID, pool, provider);
        }

        // Tests the sessionauthentication process with a NEW_PASSWORD_REQURIED flow
        [Fact]
        public async Task TestNewPasswordRequiredFlow()
        {
            var password = "PassWord1!";

            var context =
                await user.StartWithSrpAuthAsync(new InitiateSrpAuthRequest()
                {
                    Password = password
                }).ConfigureAwait(false);

            Assert.Equal(context.ChallengeName, ChallengeNameType.NEW_PASSWORD_REQUIRED);

            context = await user.RespondToNewPasswordRequiredAsync(new RespondToNewPasswordRequiredRequest()
            {
                SessionID = context.SessionID,
                NewPassword = "NewPassword1!"
            });

            Assert.True(user.SessionTokens.IsValid());
        }
    }
}
