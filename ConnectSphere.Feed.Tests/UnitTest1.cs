using NUnit.Framework;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ConnectSphere.Feed.Services;
using ConnectSphere.Feed.Data;
using ConnectSphere.Feed.Models;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace ConnectSphere.Feed.Tests
{
    [TestFixture]
    public class FeedServiceTests
    {
        private FeedDbContext _db;
        private IDistributedCache _cache;
        private Mock<IHttpClientFactory> _httpFactoryMock;
        private Mock<ILogger<FeedService>> _loggerMock;
        private FeedService _service;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<FeedDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _db = new FeedDbContext(options);

            _cache = new MemoryDistributedCache(
                Options.Create(new MemoryDistributedCacheOptions()));

            _httpFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<FeedService>>();

            var client = new HttpClient(new FakeHttpMessageHandler())
            {
                BaseAddress = new Uri("http://localhost")
            };

            _httpFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(client);

            _service = new FeedService(
                _db,
                _cache,
                _httpFactoryMock.Object,
                _loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _db.Database.EnsureDeleted();
            _db.Dispose();
        }

        

        

        // ==================================================
        // Explore Feed
        // ==================================================

        [Test]
        public async Task GetExploreFeedAsync_ShouldReturnFeed()
        {
            var result = await _service.GetExploreFeedAsync(1);

            Assert.That(result, Is.Not.Null);
        }

        // ==================================================
        // User Timeline
        // ==================================================

        [Test]
        public async Task GetUserTimelineAsync_ShouldReturnTimeline()
        {
            var result = await _service.GetUserTimelineAsync(1);

            Assert.That(result, Is.Not.Null);
        }

        // ==================================================
        // AddPostToFollowerFeedsAsync
        // ==================================================

        [Test]
        public async Task AddPostToFollowerFeedsAsync_ShouldInsertFeedItems()
        {
            await _service.AddPostToFollowerFeedsAsync(10, 1);

            var count = await _db.FeedItems.CountAsync();

            Assert.That(count, Is.EqualTo(2));
        }

        // ==================================================
        // Cache Invalidate
        // ==================================================

        [Test]
        public async Task InvalidateFeedCacheAsync_ShouldRemoveCache()
        {
            await _cache.SetStringAsync("feed:1:1:20", "abc");

            await _service.InvalidateFeedCacheAsync(1);

            var result = await _cache.GetStringAsync("feed:1:1:20");

            Assert.That(result, Is.Null);
        }

        // ==================================================
        // Trending Hashtags
        // ==================================================

        [Test]
        public async Task GetTrendingHashtagsAsync_ShouldReturnList()
        {
            var result = await _service.GetTrendingHashtagsAsync();

            Assert.That(result, Is.Not.Null);
        }

        // ==================================================
        // Suggested Users
        // ==================================================

        [Test]
        public async Task GetSuggestedUsersAsync_ShouldReturnList()
        {
            var result = await _service.GetSuggestedUsersAsync(1);

            Assert.That(result, Is.Not.Null);
        }
    }

    // ======================================================
    // Fake HTTP Handler
    // ======================================================

    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.ToLower();

            string json = "[]";

            if (path.Contains("following-ids"))
            {
                json = "[2,3]";
            }
            else if (path.Contains("followers"))
            {
                json = """
                [
                  { "followerId": 2, "followeeId": 1, "status": "Accepted" },
                  { "followerId": 3, "followeeId": 1, "status": "Accepted" }
                ]
                """;
            }
            else if (path.Contains("public"))
            {
                json = """
                [
                  { "postId": 1, "userId": 2, "likeCount": 10, "commentCount": 5 },
                  { "postId": 2, "userId": 3, "likeCount": 4, "commentCount": 1 }
                ]
                """;
            }
            else if (path.Contains("timeline"))
            {
                json = """
                [
                  { "postId": 11, "userId": 1 }
                ]
                """;
            }
            else if (path.Contains("trending-hashtags"))
            {
                json = """
                [
                  { "tag": "#dotnet", "count": 10 }
                ]
                """;
            }
            else if (path.Contains("suggested"))
            {
                json = """
                [
                  { "userId": 5, "userName": "rohit" }
                ]
                """;
            }

            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}