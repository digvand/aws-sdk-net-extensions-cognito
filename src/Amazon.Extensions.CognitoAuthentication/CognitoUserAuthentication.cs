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
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Extensions.CognitoAuthentication.Util;

namespace Amazon.Extensions.CognitoAuthentication
{
    partial class CognitoUser
    {
        /// <summary>
        /// Initiates the asynchronous SRP authentication flow
        /// </summary>
        /// <param name="srpRequest">InitiateSrpAuthRequest object containing the necessary parameters to
        /// create an InitiateAuthAsync API call for SRP authentication</param>
        /// <returns>Returns the AuthFlowResponse object that can be used to respond to the next challenge, 
        /// if one exists</returns>
        public async Task<AuthFlowResponse> StartWithSrpAuthAsync(InitiateSrpAuthRequest srpRequest)
        {
            if (srpRequest == null || string.IsNullOrEmpty(srpRequest.Password))
            {
                throw new ArgumentNullException("Password required for authentication.", "srpRequest");
            }

            var tupleAa = AuthenticationHelper.CreateAaTuple();
            var initiateRequest = CreateSrpAuthRequest(tupleAa);

            var initiateResponse = await Provider.InitiateAuthAsync(initiateRequest).ConfigureAwait(false);
            UpdateUsernameAndSecretHash(initiateResponse.ChallengeParameters);

            var challengeRequest =
                CreateSrpPasswordVerifierAuthRequest(initiateResponse, srpRequest.Password, tupleAa);

            var challengeResponsesValid = challengeRequest != null && challengeRequest.ChallengeResponses != null;
            var deviceKeyValid = Device != null && !string.IsNullOrEmpty(Device.DeviceKey);

            if (challengeResponsesValid && deviceKeyValid)
            {
                challengeRequest.ChallengeResponses.Add(CognitoConstants.ChlgParamDeviceKey, Device.DeviceKey);
            }

            var verifierResponse =
                await Provider.RespondToAuthChallengeAsync(challengeRequest).ConfigureAwait(false);

            UpdateSessionIfAuthenticationComplete(verifierResponse.ChallengeName, verifierResponse.AuthenticationResult);

            return new AuthFlowResponse(verifierResponse.Session,
                verifierResponse.AuthenticationResult,
                verifierResponse.ChallengeName,
                verifierResponse.ChallengeParameters,
                new Dictionary<string, string>(verifierResponse.ResponseMetadata.Metadata));
        }

        /// <summary>
        /// Initiates the asynchronous custom authentication flow
        /// </summary>
        /// <param name="customRequest">InitiateCustomAuthRequest object containing the necessary parameters to
        /// create an InitiateAuthAsync API call for custom authentication</param>
        /// <returns>Returns the AuthFlowResponse object that can be used to respond to the next challenge, 
        /// if one exists</returns>
        public async Task<AuthFlowResponse> StartWithCustomAuthAsync(InitiateCustomAuthRequest customRequest)
        {
            var authRequest = new InitiateAuthRequest()
            {
                AuthFlow = AuthFlowType.CUSTOM_AUTH,
                AuthParameters = new Dictionary<string, string>(customRequest.AuthParameters),
                ClientId = ClientID,
                ClientMetadata = new Dictionary<string, string>(customRequest.ClientMetadata)
            };

            var initiateResponse = await Provider.InitiateAuthAsync(authRequest).ConfigureAwait(false);
            UpdateUsernameAndSecretHash(initiateResponse.ChallengeParameters);

            UpdateSessionIfAuthenticationComplete(initiateResponse.ChallengeName, initiateResponse.AuthenticationResult);

            return new AuthFlowResponse(initiateResponse.Session,
                initiateResponse.AuthenticationResult,
                initiateResponse.ChallengeName,
                initiateResponse.ChallengeParameters,
                new Dictionary<string, string>(initiateResponse.ResponseMetadata.Metadata));
        }

        /// <summary>
        /// Uses the properties of the RespondToCustomChallengeRequest object to respond to the current 
        /// custom authentication challenge using an asynchronous call
        /// </summary>
        /// <param name="customRequest">RespondToCustomChallengeRequest object containing the necessary parameters to
        /// respond to the current custom authentication challenge</param>
        /// <returns>Returns the AuthFlowResponse object that can be used to respond to the next challenge, 
        /// if one exists</returns>
        public async Task<AuthFlowResponse> RespondToCustomAuthAsync(RespondToCustomChallengeRequest customRequest)
        {
            var request = new RespondToAuthChallengeRequest()
            {
                ChallengeName = ChallengeNameType.CUSTOM_CHALLENGE,
                ClientId = ClientID,
                ChallengeResponses = new Dictionary<string, string>(customRequest.ChallengeParameters),
                Session = customRequest.SessionID
            };

            var authResponse =
                await Provider.RespondToAuthChallengeAsync(request).ConfigureAwait(false);

            UpdateSessionIfAuthenticationComplete(authResponse.ChallengeName, authResponse.AuthenticationResult);

            return new AuthFlowResponse(authResponse.Session,
                authResponse.AuthenticationResult,
                authResponse.ChallengeName,
                authResponse.ChallengeParameters,
                new Dictionary<string, string>(authResponse.ResponseMetadata.Metadata));
        }

        /// <summary>
        /// Uses the properties of the RespondToSmsMfaRequest object to respond to the current MFA 
        /// authentication challenge using an asynchronous call
        /// </summary>
        /// <param name="smsMfaRequest">RespondToSmsMfaRequest object containing the necessary parameters to
        /// respond to the current SMS MFA authentication challenge</param>
        /// <returns>Returns the AuthFlowResponse object that can be used to respond to the next challenge, 
        /// if one exists</returns>
        public async Task<AuthFlowResponse> RespondToSmsMfaAuthAsync(RespondToSmsMfaRequest smsMfaRequest)
        {
            var challengeRequest = new RespondToAuthChallengeRequest
            {
                ChallengeResponses = new Dictionary<string, string>
                    {
                        { CognitoConstants.ChlgParamSmsMfaCode, smsMfaRequest.MfaCode},
                        { CognitoConstants.ChlgParamUsername, Username }
                    },
                Session = smsMfaRequest.SessionID,
                ClientId = ClientID,
                ChallengeName = ChallengeNameType.SMS_MFA
            };

            if (!string.IsNullOrEmpty(SecretHash))
            {
                challengeRequest.ChallengeResponses.Add(CognitoConstants.ChlgParamSecretHash, SecretHash);
            }

            var challengeResponse =
                await Provider.RespondToAuthChallengeAsync(challengeRequest).ConfigureAwait(false);

            UpdateSessionIfAuthenticationComplete(challengeResponse.ChallengeName, challengeResponse.AuthenticationResult);

            return new AuthFlowResponse(challengeResponse.Session,
                challengeResponse.AuthenticationResult,
                challengeResponse.ChallengeName,
                challengeResponse.ChallengeParameters,
                new Dictionary<string, string>(challengeResponse.ResponseMetadata.Metadata));
        }

        /// <summary>
        /// Uses the properties of the RespondToNewPasswordRequiredRequest object to respond to the current new 
        /// password required authentication challenge using an asynchronous call
        /// </summary>
        /// <param name="newPasswordRequest">RespondToNewPasswordRequiredRequest object containing the necessary 
        /// parameters to respond to the current SMS MFA authentication challenge</param>
        /// <returns>Returns the AuthFlowResponse object that can be used to respond to the next challenge, 
        /// if one exists</returns>
        public Task<AuthFlowResponse> RespondToNewPasswordRequiredAsync(RespondToNewPasswordRequiredRequest newPasswordRequest)
        {
            return RespondToNewPasswordRequiredAsync(newPasswordRequest, null);
        }

        /// <summary>
        /// Uses the properties of the RespondToNewPasswordRequiredRequest object to respond to the current new 
        /// password required authentication challenge using an asynchronous call
        /// </summary>
        /// <param name="newPasswordRequest">RespondToNewPasswordRequiredRequest object containing the necessary 
        /// <param name="requiredAttributes">Optional dictionnary of attributes that may be required by the user pool
        /// Each attribute key must be prefixed by "userAttributes."
        /// parameters to respond to the current SMS MFA authentication challenge</param>
        /// <returns>Returns the AuthFlowResponse object that can be used to respond to the next challenge, 
        /// if one exists</returns>
        public async Task<AuthFlowResponse> RespondToNewPasswordRequiredAsync(RespondToNewPasswordRequiredRequest newPasswordRequest, Dictionary<string, string> requiredAttributes)
        {
            var challengeResponses = new Dictionary<string, string>()
            {
                { CognitoConstants.ChlgParamNewPassword, newPasswordRequest.NewPassword},
                { CognitoConstants.ChlgParamUsername, Username }
            };

            if (requiredAttributes != null)
            {
                foreach (var attribute in requiredAttributes)
                {
                    challengeResponses.Add(attribute.Key, attribute.Value);
                }
            }

            var challengeRequest = new RespondToAuthChallengeRequest
            {
                
                ChallengeResponses = challengeResponses,
                Session = newPasswordRequest.SessionID,
                ClientId = ClientID,
                ChallengeName = ChallengeNameType.NEW_PASSWORD_REQUIRED
            };

            if (!string.IsNullOrEmpty(SecretHash))
            {
                challengeRequest.ChallengeResponses.Add(CognitoConstants.ChlgParamSecretHash, SecretHash);
            }

            var challengeResponse =
                await Provider.RespondToAuthChallengeAsync(challengeRequest).ConfigureAwait(false);

            UpdateSessionIfAuthenticationComplete(challengeResponse.ChallengeName, challengeResponse.AuthenticationResult);

            return new AuthFlowResponse(challengeResponse.Session,
                challengeResponse.AuthenticationResult,
                challengeResponse.ChallengeName,
                challengeResponse.ChallengeParameters,
                new Dictionary<string, string>(challengeResponse.ResponseMetadata.Metadata));
        }

        /// <summary>
        /// Initiates the asynchronous refresh token authentication flow
        /// </summary>
        /// <param name="refreshTokenRequest">InitiateRefreshTokenAuthRequest object containing the necessary 
        /// parameters to initiate the refresh token authentication flow</param>
        /// <returns>Returns the AuthFlowResponse object that can be used to respond to the next challenge, 
        /// if one exists</returns>
        public async Task<AuthFlowResponse> StartWithRefreshTokenAuthAsync(InitiateRefreshTokenAuthRequest refreshTokenRequest)
        {
            var initiateAuthRequest = CreateRefreshTokenAuthRequest(refreshTokenRequest.AuthFlowType);

            var initiateResponse =
                await Provider.InitiateAuthAsync(initiateAuthRequest).ConfigureAwait(false);

            UpdateSessionIfAuthenticationComplete(initiateResponse.ChallengeName, initiateResponse.AuthenticationResult);

            return new AuthFlowResponse(initiateResponse.Session,
                initiateResponse.AuthenticationResult,
                initiateResponse.ChallengeName,
                initiateResponse.ChallengeParameters,
                new Dictionary<string, string>(initiateResponse.ResponseMetadata.Metadata));
        }

        /// <summary>
        /// Initiates the asynchronous ADMIN_NO_SRP_AUTH authentication flow
        /// </summary>
        /// <param name="adminAuthRequest">InitiateAdminNoSrpAuthRequest object containing the necessary 
        /// parameters to initiate the ADMIN_NO_SRP_AUTH authentication flow</param>
        /// <returns>Returns the AuthFlowResponse object that can be used to respond to the next challenge, 
        /// if one exists</returns>
        public async Task<AuthFlowResponse> StartWithAdminNoSrpAuthAsync(InitiateAdminNoSrpAuthRequest adminAuthRequest)
        {
            var initiateAuthRequest = CreateAdminAuthRequest(adminAuthRequest);

            var initiateResponse =
                await Provider.AdminInitiateAuthAsync(initiateAuthRequest).ConfigureAwait(false);

            UpdateSessionIfAuthenticationComplete(initiateResponse.ChallengeName, initiateResponse.AuthenticationResult);

            return new AuthFlowResponse(initiateResponse.Session,
                initiateResponse.AuthenticationResult,
                initiateResponse.ChallengeName,
                initiateResponse.ChallengeParameters,
                new Dictionary<string, string>(initiateResponse.ResponseMetadata.Metadata));
        }

        /// <summary>
        /// Internal method for updating the CognitoUser SessionTokens property if properly authenticated
        /// </summary>
        private void UpdateSessionIfAuthenticationComplete(ChallengeNameType challengeName, AuthenticationResultType authResult)
        {
            if (string.IsNullOrEmpty(challengeName))
            {
                var cognitoUserSession = GetCognitoUserSession(authResult);
                this.SessionTokens = cognitoUserSession;
            }
        }

        /// <summary>
        /// Interal method which creates the InitiateAuthRequest for an SRP authentication flow
        /// </summary>
        /// <param name="tupleAa">Tuple containing the A,a pair for SRP authentication</param>
        /// <returns>Returns the InitiateAuthRequest for an SRP authentication flow</returns>
        private InitiateAuthRequest CreateSrpAuthRequest(Tuple<BigInteger, BigInteger> tupleAa)
        {
            var initiateAuthRequest = new InitiateAuthRequest()
            {
                AuthFlow = AuthFlowType.USER_SRP_AUTH,
                ClientId = ClientID,
                AuthParameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { CognitoConstants.ChlgParamUsername, Username },
                    { CognitoConstants.ChlgParamSrpA, tupleAa.Item1.ToString("X") }
                }
            };

            if (!string.IsNullOrEmpty(ClientSecret))
            {
                initiateAuthRequest.AuthParameters.Add(CognitoConstants.ChlgParamSecretHash,
                                                    CognitoAuthHelper.GetUserPoolSecretHash(Username, ClientID, ClientSecret));
            }

            if (Device != null && !string.IsNullOrEmpty(Device.DeviceKey))
            {
                initiateAuthRequest.AuthParameters.Add(CognitoConstants.ChlgParamDeviceKey, Device.DeviceKey);
            }

            return initiateAuthRequest;
        }

        /// <summary>
        /// Internal mehtod which updates CognitoUser's username, secret hash, and device key from challege parameters
        /// </summary>
        /// <param name="challengeParameters">Dictionary containing the key-value pairs for challenge parameters</param>
        private void UpdateUsernameAndSecretHash(IDictionary<string, string> challengeParameters)
        {
            var canSetUsername = string.IsNullOrEmpty(Username) || string.Equals(UserID, Username, StringComparison.Ordinal);
            var challengeParamIsUsername = challengeParameters != null && challengeParameters.ContainsKey(CognitoConstants.ChlgParamUsername);
            var shouldUpdate = canSetUsername || challengeParamIsUsername;

            if (!shouldUpdate)
            {
                return;
            }

            if (challengeParameters.ContainsKey(CognitoConstants.ChlgParamUsername))
            {
                Username = challengeParameters[CognitoConstants.ChlgParamUsername];
            }

            if (!string.IsNullOrEmpty(ClientSecret))
            {
                SecretHash = CognitoAuthHelper.GetUserPoolSecretHash(Username, ClientID, ClientSecret);
            }
        }

        private AdminInitiateAuthRequest CreateAdminAuthRequest(InitiateAdminNoSrpAuthRequest adminRequest)
        {
            var returnRequest = new AdminInitiateAuthRequest()
            {
                AuthFlow = AuthFlowType.ADMIN_NO_SRP_AUTH,
                ClientId = ClientID,
                UserPoolId = UserPool.PoolID,
                AuthParameters = new Dictionary<string, string>()
                {
                    { CognitoConstants.ChlgParamUsername, Username },
                    {CognitoConstants.ChlgParamPassword, adminRequest.Password }
                }
            };

            if (Device != null && !string.IsNullOrEmpty(Device.DeviceKey))
            {
                returnRequest.AuthParameters.Add(CognitoConstants.ChlgParamDeviceKey, Device.DeviceKey);
            }

            if (!string.IsNullOrEmpty(SecretHash))
            {
                returnRequest.AuthParameters.Add(CognitoConstants.ChlgParamSecretHash, SecretHash);
            }

            if (adminRequest.ClientMetadata != null)
            {
                returnRequest.ClientMetadata = new Dictionary<string, string>(adminRequest.ClientMetadata);
            }

            return returnRequest;
        }

        private InitiateAuthRequest CreateRefreshTokenAuthRequest(AuthFlowType authFlowType)
        {
            EnsureUserAuthenticated();

            if (authFlowType != AuthFlowType.REFRESH_TOKEN && authFlowType != AuthFlowType.REFRESH_TOKEN_AUTH)
            {
                throw new ArgumentException("authFlowType must be either \"REFRESH_TOKEN\" or \"REFRESH_TOKEN_AUTH\"", "authFlowType");
            }

            var initiateAuthRequest = new InitiateAuthRequest()
            {
                AuthFlow = authFlowType,
                ClientId = ClientID,
                AuthParameters = new Dictionary<string, string>()
                {
                    {CognitoConstants.ChlgParamUsername, Username },
                    {CognitoConstants.ChlgParamRefreshToken, SessionTokens.RefreshToken }
                }
            };

            if (Device != null && !string.IsNullOrEmpty(Device.DeviceKey))
            {
                initiateAuthRequest.AuthParameters.Add(CognitoConstants.ChlgParamDeviceKey, Device.DeviceKey);
            }

            if (!string.IsNullOrEmpty(SecretHash))
            {
                initiateAuthRequest.AuthParameters.Add(CognitoConstants.ChlgParamSecretHash, SecretHash);
            }

            return initiateAuthRequest;
        }

        /// <summary>
        /// Internal method which responds to the PASSWORD_VERIFIER challenge in SRP authentication
        /// </summary>
        /// <param name="challenge">Response from the InitiateAuth challenge</param>
        /// <param name="password">Password for the CognitoUser, needed for authentication</param>
        /// <param name="tupleAa">Tuple of BigIntegers containing the A,a pair for the SRP protocol flow</param>
        /// <returns>Returns the RespondToAuthChallengeRequest for an SRP authentication flow</returns>
        private RespondToAuthChallengeRequest CreateSrpPasswordVerifierAuthRequest(InitiateAuthResponse challenge,
                                                                                   string password,
                                                                                   Tuple<BigInteger, BigInteger> tupleAa)
        {
            var username = challenge.ChallengeParameters[CognitoConstants.ChlgParamUsername];
            var poolName = PoolName;
            var secretBlock = challenge.ChallengeParameters[CognitoConstants.ChlgParamSecretBlock];
            var salt = challenge.ChallengeParameters[CognitoConstants.ChlgParamSalt];
            var srpb = BigIntegerExtensions.FromUnsignedLittleEndianHex(challenge.ChallengeParameters[CognitoConstants.ChlgParamSrpB]);

            if ((srpb.TrueMod(AuthenticationHelper.N)).Equals(BigInteger.Zero))
            {
                throw new ArgumentException("SRP error, B mod N cannot be zero.", "challenge");
            }

            var timestamp = DateTime.UtcNow;
            var timeStr = timestamp.ToString("ddd MMM d HH:mm:ss \"UTC\" yyyy", CultureInfo.InvariantCulture);

            var claim = AuthenticationHelper.AuthenticateUser(username, password, poolName, tupleAa, salt,
                challenge.ChallengeParameters[CognitoConstants.ChlgParamSrpB], secretBlock, timeStr);
            var claimBase64 = Convert.ToBase64String(claim);

            var srpAuthResponses = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {CognitoConstants.ChlgParamPassSecretBlock, secretBlock},
                {CognitoConstants.ChlgParamPassSignature, claimBase64},
                {CognitoConstants.ChlgParamUsername, username },
                {CognitoConstants.ChlgParamTimestamp, timeStr },
            };

            if (!string.IsNullOrEmpty(ClientSecret))
            {
                SecretHash = CognitoAuthHelper.GetUserPoolSecretHash(Username, ClientID, ClientSecret);
                srpAuthResponses.Add(CognitoConstants.ChlgParamSecretHash, SecretHash);
            }

            if (Device != null && !string.IsNullOrEmpty(Device.DeviceKey))
            {
                srpAuthResponses.Add(CognitoConstants.ChlgParamDeviceKey, Device.DeviceKey);
            }

            var authChallengeRequest = new RespondToAuthChallengeRequest()
            {
                ChallengeName = challenge.ChallengeName,
                ClientId = ClientID,
                Session = challenge.Session,
                ChallengeResponses = srpAuthResponses
            };

            return authChallengeRequest;
        }

        /// <summary>
        /// Creates the CognitoAWSCredentials for accessing AWS resources. Should only be called with an authenticated user.
        /// </summary>
        /// <param name="identityPoolID">The poolID of the identity pool the user belongs to</param>
        /// <param name="identityPoolRegion">The region of the identity pool the user belongs to</param>
        /// <returns>Returns the CognitoAWSCredentials for the user to be able to access AWS resources</returns>
        public CognitoAWSCredentials GetCognitoAWSCredentials(string identityPoolID, RegionEndpoint identityPoolRegion)
        {
            EnsureUserAuthenticated();

            var poolRegion = UserPool.PoolID.Substring(0, UserPool.PoolID.IndexOf("_"));
            var providerName = "cognito-idp." + poolRegion + ".amazonaws.com/" + UserPool.PoolID;

            var credentials = new CognitoAWSCredentials(identityPoolID, identityPoolRegion);
            credentials.AddLogin(providerName, SessionTokens.IdToken);

            return credentials;
        }
    }
}
