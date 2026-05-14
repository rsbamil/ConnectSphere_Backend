using NUnit.Framework;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ConnectSphere.Notif.Services;
using ConnectSphere.Notif.Data;
using ConnectSphere.Notif.DTOs;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ConnectSphere.Notif.Tests
{
    [TestFixture]
    public class NotifServiceTests
    {
        private SqliteConnection _connection;
        private NotifDbContext _db;
        private Mock<IHttpClientFactory> _httpFactoryMock;
        private Mock<ILogger<NotifService>> _loggerMock;
        private NotifService _service;

        [SetUp]
        public void Setup()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<NotifDbContext>()
                .UseSqlite(_connection)
                .Options;

            _db = new NotifDbContext(options);
            _db.Database.EnsureCreated();

            _httpFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<NotifService>>();

            var client = new HttpClient(new FakeHttpHandler())
            {
                BaseAddress = new Uri("http://localhost")
            };

            _httpFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(client);

            _service = new NotifService(
                _db,
                _httpFactoryMock.Object,
                _loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _db.Dispose();
            _connection.Close();
        }

        // ============================================
        // Like Notification
        // ============================================

        [Test]
        public async Task SendLikeNotifAsync_ShouldCreateNotification()
        {
            await _service.SendLikeNotifAsync(new LikeNotifRequest
            {
                ActorId = 1,
                TargetId = 10,
                TargetType = "POST",
                Type = "LIKE_POST"
            });

            Assert.That(await _db.Notifications.CountAsync(), Is.EqualTo(1));
        }

        // ============================================
        // Comment Notification
        // ============================================

        [Test]
        public async Task SendCommentNotifAsync_ShouldCreateNotification()
        {
            await _service.SendCommentNotifAsync(new CommentNotifRequest
            {
                ActorId = 1,
                PostId = 10,
                Type = "NEW_COMMENT"
            });

            Assert.That(await _db.Notifications.CountAsync(), Is.EqualTo(1));
        }

        // ============================================
        // Follow Notification
        // ============================================

        [Test]
        public async Task SendFollowNotifAsync_ShouldCreateNotification()
        {
            await _service.SendFollowNotifAsync(new FollowNotifRequest
            {
                ActorId = 1,
                RecipientId = 2,
                Type = "NEW_FOLLOWER"
            });

            Assert.That(await _db.Notifications.CountAsync(), Is.EqualTo(1));
        }

        // ============================================
        // GetByRecipient
        // ============================================

        [Test]
        public async Task GetByRecipientAsync_ShouldReturnNotifications()
        {
            _db.Notifications.Add(new Models.Notification
            {
                RecipientId = 5,
                Type = "TEST",
                Message = "Hello"
            });

            await _db.SaveChangesAsync();

            var result = await _service.GetByRecipientAsync(5);

            Assert.That(result.Count, Is.EqualTo(1));
        }

        // ============================================
        // Unread
        // ============================================

        [Test]
        public async Task GetUnreadCountAsync_ShouldReturnCount()
        {
            _db.Notifications.Add(new Models.Notification
            {
                RecipientId = 1,
                IsRead = false,
                Type = "TEST"
            });

            await _db.SaveChangesAsync();

            var result = await _service.GetUnreadCountAsync(1);

            Assert.That(result, Is.EqualTo(1));
        }

        // ============================================
        // Mark Read
        // ============================================

        [Test]
        public async Task MarkAsReadAsync_ShouldUpdate()
        {
            var notif = new Models.Notification
            {
                RecipientId = 1,
                IsRead = false,
                Type = "TEST"
            };

            _db.Notifications.Add(notif);
            await _db.SaveChangesAsync();

            await _service.MarkAsReadAsync(notif.NotificationId, 1);

            _db.ChangeTracker.Clear();

            var updated = await _db.Notifications.FirstAsync();

            Assert.That(updated.IsRead, Is.True);
        }

        [Test]
        public async Task MarkAllReadAsync_ShouldUpdateAll()
        {
            _db.Notifications.AddRange(
                new Models.Notification { RecipientId = 1, IsRead = false, Type = "A" },
                new Models.Notification { RecipientId = 1, IsRead = false, Type = "B" }
            );

            await _db.SaveChangesAsync();

            await _service.MarkAllReadAsync(1);

            _db.ChangeTracker.Clear();

            var unread = await _db.Notifications.CountAsync(x => !x.IsRead);

            Assert.That(unread, Is.EqualTo(0));
        }

        // ============================================
        // Delete
        // ============================================

        [Test]
        public async Task DeleteNotifAsync_ShouldDelete()
        {
            var notif = new Models.Notification
            {
                RecipientId = 1,
                Type = "TEST"
            };

            _db.Notifications.Add(notif);
            await _db.SaveChangesAsync();

            await _service.DeleteNotifAsync(notif.NotificationId, 1);

            Assert.That(await _db.Notifications.CountAsync(), Is.EqualTo(0));
        }

        // ============================================
        // Broadcast
        // ============================================

        [Test]
        public async Task SendBulkAsync_ShouldInsertMany()
        {
            await _service.SendBulkAsync(new BroadcastRequest
            {
                Title = "System",
                Message = "Maintenance",
                UserIds = new List<int> { 1, 2, 3 }
            });

            Assert.That(await _db.Notifications.CountAsync(), Is.EqualTo(3));
        }
    }

    // ============================================
    // Fake HTTP
    // ============================================

    public class FakeHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var json = """{ "userId": 2 }""";

            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}