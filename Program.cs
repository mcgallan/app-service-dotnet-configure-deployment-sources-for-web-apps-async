// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.CosmosDB;
using Azure.ResourceManager.CosmosDB.Models;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Samples.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.ResourceManager.Resources.Models;
using System.Drawing;
using Azure.ResourceManager.AppService.Models;
using System.Xml;

namespace ManageWebAppSourceControlAsync
{
    public class Program
    {
        private const string Suffix = ".azurewebsites.net";

        /**
         * Azure App Service basic sample for managing web apps.
         * Note: you need to have the Git command line available on your PATH. The sample makes a direct call to 'git'.
         *  - Create 4 web apps under the same new app service plan:
         *    - Deploy to 1 using FTP
         *    - Deploy to 2 using local Git repository
         *    - Deploy to 3 using a publicly available Git repository
         *    - Deploy to 4 using a GitHub repository with continuous integration
         */
        public async static Task RunSampleAsync(ArmClient client)
        {
            AzureLocation region = AzureLocation.EastUS;
            string app1Name = Utilities.CreateRandomName("webapp1");
            string app2Name = Utilities.CreateRandomName("webapp1");
            string app3Name = Utilities.CreateRandomName("webapp1");
            string app4Name = Utilities.CreateRandomName("webapp1");
            string app1Url = app1Name + Suffix;
            string app2Url = app2Name + Suffix;
            string app3Url = app3Name + Suffix;
            string app4Url = app4Name + Suffix;
            string rgName = Utilities.CreateRandomName("rg1NEMV_");
            var lro = client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdate(Azure.WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
            var resourceGroup = lro.Value;

            try
            {
                //============================================================
                // Create a web app with a new app service plan

                Utilities.Log("Creating web app " + app1Name + " in resource group " + rgName + "...");

                var webSiteCollection = resourceGroup.GetWebSites();
                var webSiteData = new WebSiteData(region)
                {
                    SiteConfig = new Azure.ResourceManager.AppService.Models.SiteConfigProperties()
                    {
                        WindowsFxVersion = "PricingTier.StandardS1",
                        NetFrameworkVersion = "NetFrameworkVersion.V4_6",
                        JavaContainerVersion = "1.8.0_60",
                        JavaContainer = "Tomcat 8.0.23"
                    }
                };
                var webSite_lro = webSiteCollection.CreateOrUpdate(Azure.WaitUntil.Completed, app1Name, webSiteData);
                var webSite = webSite_lro.Value;

                Utilities.Log("Created web app " + webSite.Data.Name);
                Utilities.Print(webSite);

                //============================================================
                // Deploy to app 1 through FTP

                Utilities.Log("Deploying helloworld.War to " + app1Name + " through FTP...");

                var publishingprofile = (await webSite.GetPublishingProfileXmlWithSecretsAsync(new CsmPublishingProfile()
                {
                    Format = PublishingProfileFormat.Ftp
                })).Value;
                Utilities.UploadFileToWebApp(publishingprofile,
                    Path.Combine(Utilities.ProjectPath, "Asset", "helloworld.war"));

                Utilities.Log("Deployment helloworld.War to web app " + webSite.Data.Name + " completed");
                Utilities.Print(webSite);

                // warm up
                Utilities.Log("Warming up " + app1Url + "/helloworld...");
                Utilities.CheckAddress("http://" + app1Url + "/helloworld");
                Thread.Sleep(5000);
                Utilities.Log("CURLing " + app1Url + "/helloworld...");
                Utilities.Log(Utilities.CheckAddress("http://" + app1Url + "/helloworld"));

                //============================================================
                // Create a second web app with local git source control

                Utilities.Log("Creating another web app " + app2Name + " in resource group " + rgName + "...");
                var plan = webSite.Data.AppServicePlanId;
                var webSite2Collection = resourceGroup.GetWebSites();
                var webSite2Data = new WebSiteData(region)
                {
                    SiteConfig = new Azure.ResourceManager.AppService.Models.SiteConfigProperties()
                    {
                        WindowsFxVersion = "PricingTier.StandardS1",
                        NetFrameworkVersion = "NetFrameworkVersion.V4_6",
                        JavaContainerVersion = "1.8.0_60",
                        JavaContainer = "Tomcat 8.0.23",
                    },
                    AppServicePlanId = plan,
                };
                var webSite2_lro = webSiteCollection.CreateOrUpdate(Azure.WaitUntil.Completed, app1Name, webSiteData);
                var webSite2 = webSite_lro.Value;

                Utilities.Log("Created web app " + webSite2.Data.Name);
                Utilities.Print(webSite2);

                //============================================================
                // Deploy to app 2 through local Git

                Utilities.Log("Deploying a local Tomcat source to " + app2Name + " through Git...");

                var reader = new StreamReader(publishingprofile);
                var content = reader.ReadToEnd();
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(content);
                XmlNodeList gitUrl = xmlDoc.GetElementsByTagName("publishUrl");
                string gitUrlString = gitUrl[0].InnerText;
                XmlNodeList userName = xmlDoc.GetElementsByTagName("userName");
                string userNameString = userName[0].InnerText;
                XmlNodeList password = xmlDoc.GetElementsByTagName("userPWD");
                string passwordString = password[0].InnerText;
                Utilities.DeployByGit(userNameString, passwordString, gitUrlString, "azure-samples-appservice-helloworld");

                Utilities.Log("Deployment to web app " + webSite2.Data.Name + " completed");
                Utilities.Print(webSite2);

                // warm up
                Utilities.Log("Warming up " + app2Url + "/helloworld...");
                Utilities.CheckAddress("http://" + app2Url + "/helloworld");
                Thread.Sleep(5000);
                Utilities.Log("CURLing " + app2Url + "/helloworld...");
                Utilities.Log(Utilities.CheckAddress("http://" + app2Url + "/helloworld"));

                //============================================================
                // Create a 3rd web app with a public GitHub repo in Azure-Samples

                Utilities.Log("Creating another web app " + app3Name + "...");;
                var webSite3Collection = resourceGroup.GetWebSites();
                var webSite3Data = new WebSiteData(region)
                {
                    SiteConfig = new Azure.ResourceManager.AppService.Models.SiteConfigProperties()
                    {
                        WindowsFxVersion = "PricingTier.StandardS1",
                        NetFrameworkVersion = "NetFrameworkVersion.V4_6",
                        JavaContainerVersion = "1.8.0_60",
                        JavaContainer = "Tomcat 8.0.23",
                    },
                    AppServicePlanId = plan,
                };
                var webSite3_lro = webSiteCollection.CreateOrUpdate(Azure.WaitUntil.Completed, app1Name, webSiteData);
                var webSite3 = webSite_lro.Value;

                Utilities.Log("Created web app " + webSite3.Data.Name);
                Utilities.Print(webSite3);

                // warm up
                Utilities.Log("Warming up " + app3Url + "...");
                Utilities.CheckAddress("http://" + app3Url);
                Thread.Sleep(5000);
                Utilities.Log("CURLing " + app3Url + "...");
                Utilities.Log(Utilities.CheckAddress("http://" + app3Url));

                //============================================================
                // Create a 4th web app with a personal GitHub repo and turn on continuous integration

                Utilities.Log("Creating another web app " + app4Name + "...");
                var webSite4Collection = resourceGroup.GetWebSites();
                var webSite4Data = new WebSiteData(region)
                {
                    AppServicePlanId = plan,
                };
                var webSite4_lro = webSiteCollection.CreateOrUpdate(Azure.WaitUntil.Completed, app1Name, webSiteData);
                var webSite4 = webSite_lro.Value;

                Utilities.Log("Created web app " + webSite4.Data.Name);
                Utilities.Print(webSite4);

                // warm up
                Utilities.Log("Warming up " + app4Url + "...");
                Utilities.CheckAddress("http://" + app4Url);
                Thread.Sleep(5000);
                Utilities.Log("CURLing " + app4Url + "...");
                Utilities.Log(Utilities.CheckAddress("http://" + app4Url));
            }
            catch (FileNotFoundException)
            {
                Utilities.Log("Cannot find 'git' command line. Make sure Git is installed and the directory of git.exe is included in your PATH environment variable.");
            }
            finally
            {
                try
                {
                    Utilities.Log("Deleting Resource Group: " + rgName);
                    resourceGroup.Delete(WaitUntil.Completed);
                    Utilities.Log("Deleted Resource Group: " + rgName);
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Utilities.Log(g);
                }
            }
        }

        public static void Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                // Print selected subscription
                Utilities.Log("Selected subscription: " + client.GetSubscriptions().Id);

                RunSampleAsync(client).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}