﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Common.Messages;
using Common.TableModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using NSubstitute;
using WebHook.Model;

namespace Test
{
    [TestClass]
    public class WebHookTests
    {
        [TestMethod]
        public async Task GivenCommitToOtherBranch_ShouldReturnOkDoNothing()
        {
            var result = await ExecuteHookAsync(
                githubEvent: "push",
                payload: "data/hooks/commit-otherbranch.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("Commit to non default branch", response.Result);

            // No messages sent to Router
            await routerMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No messages sent to OpenPr
            await openPrMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No calls to InstallationTable
            await installationsTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());

            // No calls to MarketplaceTable
            await marketplaceTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());
        }

        [TestMethod]
        public async Task GivenCommitToDefaultBranchNoImages_ShouldReturnOkDoNothing()
        {
            var result = await ExecuteHookAsync(
                githubEvent: "push",
                payload: "data/hooks/commit-defaultbranch-noimages.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("No image files touched", response.Result);

            // No messages sent to Router
            await routerMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No messages sent to OpenPr
            await openPrMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No calls to InstallationTable
            await installationsTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());

            // No calls to MarketplaceTable
            await marketplaceTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());
        }

        [TestMethod]
        public async Task GivenCommitToImgBotBranchByImgbot_ShouldReturnOkQueueToOpenPr()
        {
            var result = await ExecuteHookAsync(
                githubEvent: "push",
                payload: "data/hooks/commit-imgbotbranch-byimgbot.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("imgbot push", response.Result);

            // No messages sent to Router
            await routerMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // One message sent to OpenPr
            await openPrMessages.Received(1).AddMessageAsync(Arg.Is<CloudQueueMessage>(x =>
                       JsonConvert.DeserializeObject<OpenPrMessage>(x.AsString).InstallationId == 23199 &&
                       JsonConvert.DeserializeObject<OpenPrMessage>(x.AsString).RepoName == "test" &&
                       JsonConvert.DeserializeObject<OpenPrMessage>(x.AsString).CloneUrl == "https://github.com/dabutvin/test"));

            // No calls to InstallationTable
            await installationsTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());

            // No calls to MarketplaceTable
            await marketplaceTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());
        }

        [TestMethod]
        public async Task GivenCommitToImgBotBranchByOthers_ShouldReturnOkDoNothing()
        {
            var result = await ExecuteHookAsync(
                githubEvent: "push",
                payload: "data/hooks/commit-imgbotbranch-byothers.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("Commit to non default branch", response.Result);

            // No messages sent to Router
            await routerMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No messages sent to OpenPr
            await openPrMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No calls to InstallationTable
            await installationsTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());

            // No calls to MarketplaceTable
            await marketplaceTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());
        }

        [TestMethod]
        public async Task GivenCommitToDefaultBranchWithImages_ShouldReturnOkQueueToRouter()
        {
            var result = await ExecuteHookAsync(
                githubEvent: "push",
                payload: "data/hooks/commit-defaultbranch-images.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("truth", response.Result);

            // 1 message sent to Router
            await routerMessages.Received(1).AddMessageAsync(Arg.Is<CloudQueueMessage>(x =>
                JsonConvert.DeserializeObject<RouterMessage>(x.AsString).InstallationId == 23199 &&
                JsonConvert.DeserializeObject<RouterMessage>(x.AsString).Owner == "dabutvin" &&
                JsonConvert.DeserializeObject<RouterMessage>(x.AsString).RepoName == "test" &&
                JsonConvert.DeserializeObject<RouterMessage>(x.AsString).CloneUrl == "https://github.com/dabutvin/test"));

            // No messages sent to OpenPr
            await openPrMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No calls to InstallationTable
            await installationsTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());

            // No calls to MarketplaceTable
            await marketplaceTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());
        }

        [TestMethod]
        public async Task GivenCommitToDefaultBranchWithImagesUppercaseExtensions_ShouldReturnOkQueueToRouter()
        {
            var result = await ExecuteHookAsync(
                githubEvent: "push",
                payload: "cased-data/hooks/commit-defaultbranch-images-uppercase-extensions.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("truth", response.Result);

            // 1 message sent to Router
            await routerMessages.Received(1).AddMessageAsync(Arg.Is<CloudQueueMessage>(x =>
                JsonConvert.DeserializeObject<RouterMessage>(x.AsString).InstallationId == 23199 &&
                JsonConvert.DeserializeObject<RouterMessage>(x.AsString).Owner == "dabutvin" &&
                JsonConvert.DeserializeObject<RouterMessage>(x.AsString).RepoName == "test" &&
                JsonConvert.DeserializeObject<RouterMessage>(x.AsString).CloneUrl == "https://github.com/dabutvin/test"));

            // No messages sent to OpenPr
            await openPrMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No calls to InstallationTable
            await installationsTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());

            // No calls to MarketplaceTable
            await marketplaceTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());
        }

        [TestMethod]
        public async Task GivenNewInstallationAdded_ShouldReturnOkQueueRouter()
        {
            var result = await ExecuteHookAsync(
                githubEvent: "installation_repositories",
                payload: "data/hooks/installation_repositories_added.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("truth", response.Result);

            // 1 message sent to Router
            await routerMessages.Received(1).AddMessageAsync(Arg.Is<CloudQueueMessage>(x =>
                         JsonConvert.DeserializeObject<RouterMessage>(x.AsString).InstallationId == 554 &&
                         JsonConvert.DeserializeObject<RouterMessage>(x.AsString).CloneUrl == "https://github.com/dabutvin/testing" &&
                         JsonConvert.DeserializeObject<RouterMessage>(x.AsString).Owner == "dabutvin" &&
                         JsonConvert.DeserializeObject<RouterMessage>(x.AsString).RepoName == "testing"));

            // No messages sent to OpenPr
            await openPrMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No calls to InstallationTable
            await installationsTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());

            // No calls to MarketplaceTable to insert
            await marketplaceTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());
        }

        [TestMethod]
        public async Task GivenNewInstallationCreated_ShouldReturnOkQueueRouter()
        {
            var result = await ExecuteHookAsync(
                githubEvent: "installation",
                payload: "data/hooks/installation_repositories_created.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("truth", response.Result);

            // 1 message sent to Router
            await routerMessages.Received(1).AddMessageAsync(Arg.Is<CloudQueueMessage>(x =>
                          JsonConvert.DeserializeObject<RouterMessage>(x.AsString).InstallationId == 541 &&
                          JsonConvert.DeserializeObject<RouterMessage>(x.AsString).CloneUrl == "https://github.com/dabutvin/myrepo" &&
                          JsonConvert.DeserializeObject<RouterMessage>(x.AsString).Owner == "dabutvin" &&
                          JsonConvert.DeserializeObject<RouterMessage>(x.AsString).RepoName == "myrepo"));

            // No messages sent to OpenPr
            await openPrMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No calls to InstallationTable
            await installationsTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());

            // No calls to MarketplaceTable to insert
            await marketplaceTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());
        }

        [TestMethod]
        public async Task GivenInstallationRemoved_ShouldReturnOkDropRow()
        {
            void ExtraSetup(
                CloudQueue extraRouterMessages,
                CloudQueue extraOpenPrMessages,
                CloudTable extraInstallationsTable,
                CloudTable extraMarketplaceTable) =>
            extraInstallationsTable
                .ExecuteAsync(Arg.Is<TableOperation>(x => x.OperationType == TableOperationType.Retrieve))
                .Returns(Task.FromResult(new TableResult
                {
                    Result = new Marketplace
                    {
                        ETag = "*",
                        RowKey = "1",
                        PartitionKey = "testing"
                    },
                }));

            var result = await ExecuteHookAsync(
                githubEvent: "installation_repositories",
                payload: "data/hooks/installation_repositories_removed.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable,
                ExtraSetup);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("truth", response.Result);

            // 0 messages sent to Router
            await routerMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No messages sent to OpenPr
            await openPrMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // 1 call to InstallationTable
            await installationsTable.Received(1).ExecuteAsync(Arg.Is<TableOperation>(x => x.OperationType == TableOperationType.Delete));

            // No calls to MarketplaceTable to insert
            await marketplaceTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());
        }

        [TestMethod]
        public async Task GivenInstallationDeleted_ShouldReturnOkDropPartition()
        {
            var mockIntallations = new[]
            {
                new DynamicTableEntity("654321", "repo1") { ETag = "*" },
                new DynamicTableEntity("654321", "repo2") { ETag = "*" },
            }.ToList();

            var tableQuerySegment = (TableQuerySegment)typeof(TableQuerySegment)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(x => x.GetParameters().Count() == 1)
                .Invoke(new object[] { mockIntallations });

            void ExtraSetup(
                CloudQueue extraRouterMessages,
                CloudQueue extraOpenPrMessages,
                CloudTable extraInstallationsTable,
                CloudTable extraMarketplaceTable) =>
            extraInstallationsTable
                .ExecuteQuerySegmentedAsync(Arg.Any<TableQuery>(), Arg.Any<TableContinuationToken>())
                .Returns(Task.FromResult(tableQuerySegment));

            var result = await ExecuteHookAsync(
                githubEvent: "installation",
                payload: "data/hooks/installation_deleted.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable,
                ExtraSetup);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("truth", response.Result);

            // 0 message sent to Router
            await routerMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No messages sent to OpenPr
            await openPrMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // 2 call to InstallationTable to delete
            await installationsTable.Received(2).ExecuteAsync(Arg.Is<TableOperation>(x => x.OperationType == TableOperationType.Delete));

            // No calls to MarketplaceTable to insert
            await marketplaceTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());
        }

        [TestMethod]
        public async Task GivenMarketplacePurchase_ShouldReturnOkWriteRow()
        {
            var result = await ExecuteHookAsync(
                githubEvent: "marketplace_purchase",
                payload: "data/hooks/marketplacepurchase.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("purchased", response.Result);

            // No messages sent to Router
            await routerMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No messages sent to OpenPr
            await openPrMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No calls to InstallationTable
            await installationsTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());

            // 1 call to MarketplaceTable to insert
            await marketplaceTable.Received(1).ExecuteAsync(
                Arg.Is<TableOperation>(x => x.OperationType == TableOperationType.InsertOrMerge));
        }

        [TestMethod]
        public async Task GivenMarketplaceCancellation_ShouldReturnOkDeleteRow()
        {
            void ExtraSetup(
                CloudQueue extraRouterMessages,
                CloudQueue extraOpenPrMessages,
                CloudTable extraInstallationsTable,
                CloudTable extraMarketplaceTable) =>
            extraMarketplaceTable
                .ExecuteAsync(Arg.Is<TableOperation>(x => x.OperationType == TableOperationType.Retrieve))
                .Returns(Task.FromResult(new TableResult
                {
                    Result = new Marketplace
                    {
                        ETag = "*",
                        RowKey = "1",
                        PartitionKey = "test"
                    },
                }));

            var result = await ExecuteHookAsync(
                githubEvent: "marketplace_purchase",
                payload: "data/hooks/marketplacecancellation.json",
                out var routerMessages,
                out var openPrMessages,
                out var installationsTable,
                out var marketplaceTable,
                ExtraSetup);

            // Assert OKObjectResult and Value
            var response = (HookResponse)((OkObjectResult)result).Value;
            Assert.AreEqual("cancelled", response.Result);

            // No messages sent to Router
            await routerMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No messages sent to OpenPr
            await openPrMessages.DidNotReceive().AddMessageAsync(Arg.Any<CloudQueueMessage>());

            // No calls to InstallationTable
            await installationsTable.DidNotReceive().ExecuteAsync(Arg.Any<TableOperation>());

            // 1 call to MarketplaceTable to delete
            await marketplaceTable.Received(1).ExecuteAsync(
                Arg.Is<TableOperation>(x => x.OperationType == TableOperationType.Delete));
        }

        private Task<IActionResult> ExecuteHookAsync(
            string githubEvent,
            string payload,
            out CloudQueue routerMessages,
            out CloudQueue openPrMessages,
            out CloudTable installationsTable,
            out CloudTable marketplaceTable,
            Action<CloudQueue, CloudQueue, CloudTable, CloudTable> extraSetup = null)
        {
            var request = Substitute.For<HttpRequestMessage>();
            routerMessages = Substitute.For<CloudQueue>(new Uri("https://myaccount.queue.core.windows.net/Queue/routermessage"));
            openPrMessages = Substitute.For<CloudQueue>(new Uri("https://myaccount.queue.core.windows.net/Queue/openprmessage"));
            installationsTable = Substitute.For<CloudTable>(new Uri("https://myaccount.table.core.windows.net/Tables/installation"));
            marketplaceTable = Substitute.For<CloudTable>(new Uri("https://myaccount.table.core.windows.net/Tables/marketplace"));
            var logger = Substitute.For<ILogger>();

            request.Headers.Add("X-GitHub-Event", new[] { githubEvent });
            request.Content = new StringContent(File.ReadAllText(payload));

            extraSetup?.Invoke(routerMessages, openPrMessages, installationsTable, marketplaceTable);

            return WebHook.WebHookFunction.Run(
                request, routerMessages, openPrMessages, installationsTable, marketplaceTable, logger);
        }
    }
}
