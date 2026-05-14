using NUnit.Framework;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ConnectSphere.Follow.Services;
using ConnectSphere.Follow.Data;
using ConnectSphere.Follow.DTOs;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ConnectSphere.Follow.Tests
{
    [TestFixture]
    public class FollowServiceTests
    {
        private FollowDbContext _db;
        private Mock<IHttpClientFactory> _httpFactoryMock;
        private Mock<ILogger<FollowService>> _loggerMock;
        private FollowService _service;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<FollowDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _db = new FollowDbContext(options);

            _httpFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<FollowService>>();

            var client = new HttpClient(new FakeHttpMessageHandler())
            {
                BaseAddress = new Uri("http://localhost")
            };

            _httpFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(client);

            _service = new FollowService(
                _db,
                _httpFactoryMock.Object,
                _loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _db.Database.EnsureDeleted();
            _db.Dispose();
        }

        // ======================================================
        // FollowUserAsync
        // ======================================================

        [Test]
        public async Task FollowUserAsync_ShouldCreateAcceptedFollow()
        {
            var result = await _service.FollowUserAsync(1, 2);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.FollowerId, Is.EqualTo(1));
            Assert.That(result.FolloweeId, Is.EqualTo(2));
            Assert.That(result.Status, Is.EqualTo("ACCEPTED"));
        }

        [Test]
        public void FollowUserAsync_ShouldThrow_WhenSelfFollow()
        {
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.FollowUserAsync(1, 1));
        }

        [Test]
        public void FollowUserAsync_ShouldThrow_WhenAlreadyExists()
        {
            _db.Follows.Add(new Models.Follow
            {
                FollowerId = 1,
                FolloweeId = 2,
                Status = "ACCEPTED"
            });

            _db.SaveChanges();

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.FollowUserAsync(1, 2));
        }

        // ======================================================
        // UnfollowUserAsync
        // ======================================================

        [Test]
        public async Task UnfollowUserAsync_ShouldDeleteFollow()
        {
            var follow = new Models.Follow
            {
                FollowerId = 1,
                FolloweeId = 2,
                Status = "ACCEPTED"
            };

            _db.Follows.Add(follow);
            await _db.SaveChangesAsync();

            await _service.UnfollowUserAsync(1, 2);

            Assert.That(await _db.Follows.CountAsync(), Is.EqualTo(0));
        }

        [Test]
        public void UnfollowUserAsync_ShouldThrow_WhenNotFound()
        {
            Assert.ThrowsAsync<KeyNotFoundException>(
                async () => await _service.UnfollowUserAsync(1, 2));
        }

        // ======================================================
        // Accept Follow Request
        // ======================================================

        [Test]
        public async Task AcceptFollowRequestAsync_ShouldAccept()
        {
            var follow = new Models.Follow
            {
                FollowerId = 1,
                FolloweeId = 2,
                Status = "PENDING"
            };

            _db.Follows.Add(follow);
            await _db.SaveChangesAsync();

            var result = await _service.AcceptFollowRequestAsync(follow.FollowId, 2);

            Assert.That(result.Status, Is.EqualTo("ACCEPTED"));
        }

        [Test]
        public void AcceptFollowRequestAsync_ShouldThrow_WhenNotFound()
        {
            Assert.ThrowsAsync<KeyNotFoundException>(
                async () => await _service.AcceptFollowRequestAsync(999, 2));
        }

        // ======================================================
        // Reject Follow Request
        // ======================================================

        [Test]
        public async Task RejectFollowRequestAsync_ShouldDeletePending()
        {
            var follow = new Models.Follow
            {
                FollowerId = 1,
                FolloweeId = 2,
                Status = "PENDING"
            };

            _db.Follows.Add(follow);
            await _db.SaveChangesAsync();

            await _service.RejectFollowRequestAsync(follow.FollowId, 2);

            Assert.That(await _db.Follows.CountAsync(), Is.EqualTo(0));
        }

        // ======================================================
        // GetFollowersAsync
        // ======================================================

        [Test]
        public async Task GetFollowersAsync_ShouldReturnFollowers()
        {
            _db.Follows.Add(new Models.Follow
            {
                FollowerId = 1,
                FolloweeId = 2,
                Status = "ACCEPTED"
            });

            await _db.SaveChangesAsync();

            var result = await _service.GetFollowersAsync(2);

            Assert.That(result.Count, Is.EqualTo(1));
        }

        // ======================================================
        // GetFollowingAsync
        // ======================================================

        [Test]
        public async Task GetFollowingAsync_ShouldReturnFollowing()
        {
            _db.Follows.Add(new Models.Follow
            {
                FollowerId = 1,
                FolloweeId = 2,
                Status = "ACCEPTED"
            });

            await _db.SaveChangesAsync();

            var result = await _service.GetFollowingAsync(1);

            Assert.That(result.Count, Is.EqualTo(1));
        }

        // ======================================================
        // Pending Requests
        // ======================================================

        [Test]
        public async Task GetPendingRequestsAsync_ShouldReturnPending()
        {
            _db.Follows.Add(new Models.Follow
            {
                FollowerId = 3,
                FolloweeId = 2,
                Status = "PENDING"
            });

            await _db.SaveChangesAsync();

            var result = await _service.GetPendingRequestsAsync(2);

            Assert.That(result.Count, Is.EqualTo(1));
        }

        // ======================================================
        // IsFollowingAsync
        // ======================================================

        [Test]
        public async Task IsFollowingAsync_ShouldReturnTrue()
        {
            _db.Follows.Add(new Models.Follow
            {
                FollowerId = 1,
                FolloweeId = 2,
                Status = "ACCEPTED"
            });

            await _db.SaveChangesAsync();

            var result = await _service.IsFollowingAsync(1, 2);

            Assert.That(result, Is.True);
        }

        // ======================================================
        // GetFollowingIdsAsync
        // ======================================================

        [Test]
        public async Task GetFollowingIdsAsync_ShouldReturnIds()
        {
            _db.Follows.Add(new Models.Follow
            {
                FollowerId = 1,
                FolloweeId = 2,
                Status = "ACCEPTED"
            });

            await _db.SaveChangesAsync();

            var result = await _service.GetFollowingIdsAsync(1);

            Assert.That(result.First(), Is.EqualTo(2));
        }

        // ======================================================
        // Mutual Followers
        // ======================================================

        [Test]
        public async Task GetMutualFollowersAsync_ShouldReturnCommonUsers()
        {
            _db.Follows.AddRange(
                new Models.Follow { FollowerId = 10, FolloweeId = 1, Status = "ACCEPTED" },
                new Models.Follow { FollowerId = 10, FolloweeId = 2, Status = "ACCEPTED" },
                new Models.Follow { FollowerId = 20, FolloweeId = 1, Status = "ACCEPTED" }
            );

            await _db.SaveChangesAsync();

            var result = await _service.GetMutualFollowersAsync(1, 2);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.First(), Is.EqualTo(10));
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
            string json = """{ "isPrivate": false }""";

            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}