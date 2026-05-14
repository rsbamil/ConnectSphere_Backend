using NUnit.Framework;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ConnectSphere.Like.Services;
using ConnectSphere.Like.Data;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ConnectSphere.Like.Tests
{
    [TestFixture]
    public class LikeServiceTests
    {
        private LikeDbContext _db;
        private Mock<IHttpClientFactory> _httpFactoryMock;
        private Mock<ILogger<LikeService>> _loggerMock;
        private IConfiguration _config;
        private LikeService _service;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<LikeDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _db = new LikeDbContext(options);

            _httpFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<LikeService>>();

            var settings = new Dictionary<string, string>();

            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            var client = new HttpClient(new FakeHttpMessageHandler())
            {
                BaseAddress = new Uri("http://localhost")
            };

            _httpFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(client);

            _service = new LikeService(
                _db,
                _httpFactoryMock.Object,
                _config,
                _loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _db.Database.EnsureDeleted();
            _db.Dispose();
        }

        // ===================================================
        // ToggleLikeAsync
        // ===================================================

        [Test]
        public async Task ToggleLikeAsync_ShouldAddLike_WhenNotExists()
        {
            var result = await _service.ToggleLikeAsync(1, 10, "POST");

            Assert.That(result, Is.True);
            Assert.That(await _db.Likes.CountAsync(), Is.EqualTo(1));
        }

        [Test]
        public async Task ToggleLikeAsync_ShouldRemoveLike_WhenExists()
        {
            _db.Likes.Add(new Models.Like
            {
                UserId = 1,
                TargetId = 10,
                TargetType = "POST"
            });

            await _db.SaveChangesAsync();

            var result = await _service.ToggleLikeAsync(1, 10, "POST");

            Assert.That(result, Is.False);
            Assert.That(await _db.Likes.CountAsync(), Is.EqualTo(0));
        }

        [Test]
        public async Task ToggleLikeAsync_ShouldNormalizeTargetType()
        {
            await _service.ToggleLikeAsync(1, 10, "post");

            var like = await _db.Likes.FirstAsync();

            Assert.That(like.TargetType, Is.EqualTo("POST"));
        }

        // ===================================================
        // HasUserLikedAsync
        // ===================================================

        [Test]
        public async Task HasUserLikedAsync_ShouldReturnTrue()
        {
            _db.Likes.Add(new Models.Like
            {
                UserId = 1,
                TargetId = 10,
                TargetType = "POST"
            });

            await _db.SaveChangesAsync();

            var result = await _service.HasUserLikedAsync(1, 10, "POST");

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task HasUserLikedAsync_ShouldReturnFalse()
        {
            var result = await _service.HasUserLikedAsync(1, 10, "POST");

            Assert.That(result, Is.False);
        }

        // ===================================================
        // GetLikeCountAsync
        // ===================================================

        [Test]
        public async Task GetLikeCountAsync_ShouldReturnCount()
        {
            _db.Likes.AddRange(
                new Models.Like { UserId = 1, TargetId = 10, TargetType = "POST" },
                new Models.Like { UserId = 2, TargetId = 10, TargetType = "POST" }
            );

            await _db.SaveChangesAsync();

            var result = await _service.GetLikeCountAsync(10, "POST");

            Assert.That(result, Is.EqualTo(2));
        }

        // ===================================================
        // GetLikersForPostAsync
        // ===================================================

        [Test]
        public async Task GetLikersForPostAsync_ShouldReturnUsers()
        {
            _db.Likes.AddRange(
                new Models.Like { UserId = 1, TargetId = 10, TargetType = "POST" },
                new Models.Like { UserId = 2, TargetId = 10, TargetType = "POST" }
            );

            await _db.SaveChangesAsync();

            var result = await _service.GetLikersForPostAsync(10);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.Contains(1), Is.True);
        }

        // ===================================================
        // GetLikedPostsByUserAsync
        // ===================================================

        [Test]
        public async Task GetLikedPostsByUserAsync_ShouldReturnPosts()
        {
            _db.Likes.AddRange(
                new Models.Like
                {
                    UserId = 1,
                    TargetId = 100,
                    TargetType = "POST",
                    CreatedAt = DateTime.UtcNow
                },
                new Models.Like
                {
                    UserId = 1,
                    TargetId = 200,
                    TargetType = "POST",
                    CreatedAt = DateTime.UtcNow.AddMinutes(1)
                }
            );

            await _db.SaveChangesAsync();

            var result = await _service.GetLikedPostsByUserAsync(1);

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.First(), Is.EqualTo(200));
        }
    }

    // ===================================================
    // Fake HTTP Handler
    // ===================================================

    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }
}