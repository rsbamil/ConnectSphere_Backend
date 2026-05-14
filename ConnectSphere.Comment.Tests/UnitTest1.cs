using NUnit.Framework;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ConnectSphere.Comment.Services;
using ConnectSphere.Comment.Data;
using ConnectSphere.Comment.DTOs;
using Microsoft.Data.Sqlite;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ConnectSphere.Comment.Tests
{
    [TestFixture]
    public class CommentServiceTests
    {
        private CommentDbContext _db;
        private Mock<IHttpClientFactory> _httpFactoryMock;
        private Mock<ILogger<CommentService>> _loggerMock;
        private CommentService _service;

        private SqliteConnection _connection;

        [SetUp]
        public void Setup()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<CommentDbContext>()
                .UseSqlite(_connection)
                .Options;

            _db = new CommentDbContext(options);
            _db.Database.EnsureCreated();

            _httpFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<CommentService>>();

            _httpFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            _service = new CommentService(
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

        // ==========================================
        // Add Comment Tests
        // ==========================================

        [Test]
        public async Task AddCommentAsync_ShouldCreateTopLevelComment()
        {
            var req = new AddCommentRequest
            {
                PostId = 1,
                Content = " Hello World "
            };

            var result = await _service.AddCommentAsync(5, req);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.PostId, Is.EqualTo(1));
            Assert.That(result.UserId, Is.EqualTo(5));
            Assert.That(result.Content, Is.EqualTo("Hello World"));
        }

        [Test]
        public async Task AddCommentAsync_ShouldCreateReply_AndIncrementReplyCount()
        {
            var parent = new Models.Comment
            {
                PostId = 1,
                UserId = 1,
                Content = "Parent",
                CreatedAt = DateTime.UtcNow
            };

            _db.Comments.Add(parent);
            await _db.SaveChangesAsync();

            var req = new AddCommentRequest
            {
                PostId = 1,
                ParentCommentId = parent.CommentId,
                Content = "Reply"
            };

            await _service.AddCommentAsync(2, req);

            _db.ChangeTracker.Clear();

            var updatedParent = await _db.Comments
                .AsNoTracking()
                .FirstAsync(x => x.CommentId == parent.CommentId);

            Assert.That(updatedParent.ReplyCount, Is.EqualTo(1));
        }

        // ==========================================
        // Get By Id
        // ==========================================

        [Test]
        public async Task GetCommentByIdAsync_ShouldReturnComment()
        {
            var comment = new Models.Comment
            {
                PostId = 1,
                UserId = 1,
                Content = "Test"
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            var result = await _service.GetCommentByIdAsync(comment.CommentId);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Content, Is.EqualTo("Test"));
        }

        [Test]
        public async Task GetCommentByIdAsync_ShouldReturnNull_WhenNotFound()
        {
            var result = await _service.GetCommentByIdAsync(999);

            Assert.That(result, Is.Null);
        }

        // ==========================================
        // Edit Comment
        // ==========================================

        [Test]
        public async Task EditCommentAsync_ShouldEditComment()
        {
            var comment = new Models.Comment
            {
                PostId = 1,
                UserId = 7,
                Content = "Old Text"
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            var req = new EditCommentRequest
            {
                Content = "New Text"
            };

            var result = await _service.EditCommentAsync(comment.CommentId, 7, req);

            Assert.That(result.Content, Is.EqualTo("New Text"));
            Assert.That(result.IsEdited, Is.True);
        }

        [Test]
        public void EditCommentAsync_ShouldThrow_WhenWrongUser()
        {
            var comment = new Models.Comment
            {
                PostId = 1,
                UserId = 7,
                Content = "Old"
            };

            _db.Comments.Add(comment);
            _db.SaveChanges();

            var req = new EditCommentRequest
            {
                Content = "Hack"
            };

            Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _service.EditCommentAsync(comment.CommentId, 99, req));
        }

        // ==========================================
        // Delete Comment
        // ==========================================

        [Test]
        public async Task DeleteCommentAsync_ShouldSoftDelete()
        {
            var comment = new Models.Comment
            {
                PostId = 1,
                UserId = 5,
                Content = "Delete Me"
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            await _service.DeleteCommentAsync(comment.CommentId, 5);

            _db.ChangeTracker.Clear();

            var deleted = await _db.Comments
                .AsNoTracking()
                .FirstAsync(x => x.CommentId == comment.CommentId);

            Assert.That(deleted.IsDeleted, Is.True);
        }

        [Test]
        public async Task DeleteCommentAsync_ShouldAllowAdmin()
        {
            var comment = new Models.Comment
            {
                PostId = 1,
                UserId = 5,
                Content = "Delete"
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            await _service.DeleteCommentAsync(comment.CommentId, 99, true);

            _db.ChangeTracker.Clear();

            var deleted = await _db.Comments
                .AsNoTracking()
                .FirstAsync(x => x.CommentId == comment.CommentId);

            Assert.That(deleted.IsDeleted, Is.True);
        }

        [Test]
        public void DeleteCommentAsync_ShouldThrow_WhenWrongUser()
        {
            var comment = new Models.Comment
            {
                PostId = 1,
                UserId = 5,
                Content = "Delete"
            };

            _db.Comments.Add(comment);
            _db.SaveChanges();

            Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _service.DeleteCommentAsync(comment.CommentId, 99));
        }

        // ==========================================
        // Count Tests
        // ==========================================

        [Test]
        public async Task GetCommentCountAsync_ShouldCountOnlyActive()
        {
            _db.Comments.Add(new Models.Comment
            {
                PostId = 1,
                UserId = 1,
                Content = "A",
                IsDeleted = false
            });

            _db.Comments.Add(new Models.Comment
            {
                PostId = 1,
                UserId = 1,
                Content = "B",
                IsDeleted = true
            });

            await _db.SaveChangesAsync();

            var count = await _service.GetCommentCountAsync(1);

            Assert.That(count, Is.EqualTo(1));
        }

        // ==========================================
        // Like Count
        // ==========================================

        [Test]
        public async Task IncrementLikeCountAsync_ShouldIncreaseLikes()
        {
            var comment = new Models.Comment
            {
                PostId = 1,
                UserId = 1,
                Content = "Like Me",
                LikeCount = 0
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            await _service.IncrementLikeCountAsync(comment.CommentId);

            _db.ChangeTracker.Clear();

            var updated = await _db.Comments
                .AsNoTracking()
                .FirstAsync(x => x.CommentId == comment.CommentId);

            Assert.That(updated.LikeCount, Is.EqualTo(1));
        }

        // ==========================================
        // Deleted Placeholder
        // ==========================================

        [Test]
        public async Task GetCommentByIdAsync_ShouldReturnDeletedPlaceholder()
        {
            var comment = new Models.Comment
            {
                PostId = 1,
                UserId = 1,
                Content = "Old",
                IsDeleted = true
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            var result = await _service.GetCommentByIdAsync(comment.CommentId);

            Assert.That(result.Content, Is.EqualTo("This comment was deleted."));
        }
    }
}