﻿using IdentityModel.Client;
using IdentityModel.OidcClient;
using Serilog;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ConformanceTests
{
    public class Helper
    {
        string _discoEndpoint = "https://rp.certification.openid.net:8080/{0}/{1}/.well-known/openid-configuration";

        public string RpId { get; private set; }
        public string TestName { get; private set; }

        public Helper(string rpId, string testName)
        {
            Console.WriteLine($"Starting test '{testName}'");

            RpId = rpId;
            TestName = testName;
        }

        public string GetLogUrl()
        {
            return $"https://rp.certification.openid.net:8080/log/{RpId}/{TestName}";
        }

        public async Task<string> GetLog()
        {
            var client = new HttpClient();
            var log = await client.GetStringAsync(GetLogUrl());

            return log;
        }

        public void ShowResult(LoginResult result)
        {
            if (result.IsError)
            {
                Console.WriteLine("Error!");
                Console.WriteLine(result.Error);
            }
            else
            {
                Console.WriteLine("Success!\n");
                foreach (var claim in result.User.Claims)
                {
                    Console.WriteLine($"{claim.Type}: {claim.Value}");
                }

                if (!string.IsNullOrEmpty(result.IdentityToken))
                {
                    Console.WriteLine($"\nIdentity token:\n{result.IdentityToken}");
                }
                if (!string.IsNullOrEmpty(result.AccessToken))
                {
                    Console.WriteLine($"\nAccess token:\n{result.AccessToken}");
                }
                if (!string.IsNullOrEmpty(result.RefreshToken))
                {
                    Console.WriteLine($"\nRefresh token:\n{result.RefreshToken}");
                }
            }
        }

        public async Task<OidcClientOptions> Register()
        {
            var disco = await GetDiscoveryDocument();
            var registration = await RegisterClient(disco.RegistrationEndpoint, "http://localhost:7890");

            var serilog = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.LiterateConsole(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}")
                .CreateLogger();

            var options = new OidcClientOptions
            {
                Authority = disco.Issuer,
                RedirectUri = "http://localhost:7890",
                ClientId = registration.ClientId,
                ClientSecret = registration.ClientSecret,
                TokenClientAuthenticationStyle = AuthenticationStyle.BasicAuthentication,
                Browser = new SystemBrowser(port: 7890),
                FilterClaims = false
            };

            options.LoggerFactory.AddSerilog(serilog);

            options.Policy.RequireAccessTokenHash = false;
            options.Policy.Discovery.ValidateEndpoints = false;

            return options;
        }

        public async Task<DiscoveryResponse> GetDiscoveryDocument()
        {
            var discoUrl = string.Format(_discoEndpoint, RpId, TestName);

            var client = new DiscoveryClient(discoUrl)
            {
                Policy =
                {
                    ValidateEndpoints = false
                }
            };

            var disco = await client.GetAsync();
            if (disco.IsError) throw new Exception(disco.Error);

            return disco;
        }

        public async Task<RegistrationResponse> RegisterClient(string address, string redirectUri)
        {
            var client = new DynamicRegistrationClient(address);

            var request = new RegistrationRequest
            {
                RedirectUris = { redirectUri },
                ApplicationType = "native"
            };

            var response = await client.RegisterAsync(request);
            if (response.IsError) throw new Exception(response.ErrorDescription);

            return response;
        }
    }
}