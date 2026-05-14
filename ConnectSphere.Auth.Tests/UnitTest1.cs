using NUnit.Framework;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ConnectSphere.Auth.Services;
using ConnectSphere.Auth.Interfaces;
using ConnectSphere.Auth.Models;
using ConnectSphere.Auth.DTOs;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace ConnectSphere.Auth.Tests
{
    [TestFixture]
    public class UserServiceTests
    {
        private Mock<IUserRepository> _repoMock;
        private Mock<ILogger<UserService>> _loggerMock;
        private IConfiguration _config;
        private UserService _service;

        [SetUp]
        public void Setup()
        {
            _repoMock = new Mock<IUserRepository>();
            _loggerMock = new Mock<ILogger<UserService>>();

            var settings = new Dictionary<string, string>
            {
                { "Jwt:Secret", "ThisIsMySuperSecretKey123456789123456789" },
                { "Jwt:Issuer", "ConnectSphere" },
                { "Jwt:Audience", "ConnectSphere" }
            };

            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            _service = new UserService(
                _repoMock.Object,
                _config,
                _loggerMock.Object
            );
        }

        // =====================================
        // RegisterAsync Tests
        // =====================================

        [Test]
        public async Task RegisterAsync_ShouldRegisterUser_WhenValid()
        {
            var req = new RegisterRequest
            {
                UserName = "rohit",
                FullName = "Rohit Kumar",
                Email = "rohit@gmail.com",
                Password = "123456"
            };

            _repoMock.Setup(x => x.ExistsByUserNameAsync(req.UserName))
                .ReturnsAsync(false);

            _repoMock.Setup(x => x.ExistsByEmailAsync(req.Email))
                .ReturnsAsync(false);

            _repoMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.UserId = 1;
                    return u;
                });

            var result = await _service.RegisterAsync(req);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Token, Is.Not.Null.And.Not.Empty);
            Assert.That(result.User.UserName, Is.EqualTo("rohit"));
        }

        [Test]
        public void RegisterAsync_ShouldThrow_WhenUsernameExists()
        {
            var req = new RegisterRequest
            {
                UserName = "rohit",
                Email = "rohit@gmail.com"
            };

            _repoMock.Setup(x => x.ExistsByUserNameAsync(req.UserName))
                .ReturnsAsync(true);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.RegisterAsync(req));
        }

        [Test]
        public void RegisterAsync_ShouldThrow_WhenEmailExists()
        {
            var req = new RegisterRequest
            {
                UserName = "rohit",
                Email = "rohit@gmail.com"
            };

            _repoMock.Setup(x => x.ExistsByUserNameAsync(req.UserName))
                .ReturnsAsync(false);

            _repoMock.Setup(x => x.ExistsByEmailAsync(req.Email))
                .ReturnsAsync(true);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.RegisterAsync(req));
        }

        // =====================================
        // LoginAsync Tests
        // =====================================

        [Test]
        public async Task LoginAsync_ShouldReturnToken_WhenValid()
        {
            var user = new User
            {
                UserId = 1,
                UserName = "rohit",
                Email = "rohit@gmail.com",
                IsActive = true
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "123456");

            _repoMock.Setup(x => x.FindByUserNameAsync("rohit"))
                .ReturnsAsync(user);

            var req = new LoginRequest
            {
                UserNameOrEmail = "rohit",
                Password = "123456"
            };

            var result = await _service.LoginAsync(req);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Token, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void LoginAsync_ShouldThrow_WhenWrongPassword()
        {
            var user = new User
            {
                UserId = 1,
                UserName = "rohit",
                Email = "rohit@gmail.com",
                IsActive = true
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "123456");

            _repoMock.Setup(x => x.FindByUserNameAsync("rohit"))
                .ReturnsAsync(user);

            var req = new LoginRequest
            {
                UserNameOrEmail = "rohit",
                Password = "wrongpass"
            };

            Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _service.LoginAsync(req));
        }

        [Test]
        public void LoginAsync_ShouldThrow_WhenInactiveUser()
        {
            var user = new User
            {
                UserId = 1,
                UserName = "rohit",
                Email = "rohit@gmail.com",
                IsActive = false
            };

            _repoMock.Setup(x => x.FindByUserNameAsync("rohit"))
                .ReturnsAsync(user);

            var req = new LoginRequest
            {
                UserNameOrEmail = "rohit",
                Password = "123456"
            };

            Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _service.LoginAsync(req));
        }

        // =====================================
        // Get User Tests
        // =====================================

        [Test]
        public async Task GetUserByIdAsync_ShouldReturnUser()
        {
            var user = new User
            {
                UserId = 1,
                UserName = "rohit",
                Email = "rohit@gmail.com"
            };

            _repoMock.Setup(x => x.FindByUserIdAsync(1))
                .ReturnsAsync(user);

            var result = await _service.GetUserByIdAsync(1);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.UserName, Is.EqualTo("rohit"));
        }

        [Test]
        public async Task GetUserByIdAsync_ShouldReturnNull_WhenNotFound()
        {
            _repoMock.Setup(x => x.FindByUserIdAsync(1))
                .ReturnsAsync((User)null);

            var result = await _service.GetUserByIdAsync(1);

            Assert.That(result, Is.Null);
        }

        // =====================================
        // Toggle Privacy
        // =====================================

        [Test]
        public async Task TogglePrivacyAsync_ShouldToggleToTrue()
        {
            var user = new User
            {
                UserId = 1,
                IsPrivate = false
            };

            _repoMock.Setup(x => x.FindByUserIdAsync(1))
                .ReturnsAsync(user);

            _repoMock.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(user);

            var result = await _service.TogglePrivacyAsync(1);

            Assert.That(result, Is.True);
        }

        // =====================================
        // Delete Account
        // =====================================

        [Test]
        public async Task DeleteAccountAsync_ShouldCallDelete()
        {
            await _service.DeleteAccountAsync(1, 99);

            _repoMock.Verify(x => x.DeleteAsync(1), Times.Once);
        }

        // =====================================
        // Validate Token
        // =====================================

        [Test]
        public async Task ValidateTokenAsync_ShouldReturnFalse_WhenInvalid()
        {
            var result = await _service.ValidateTokenAsync("invalidtoken");

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ValidateTokenAsync_ShouldReturnTrue_WhenValid()
        {
            var req = new RegisterRequest
            {
                UserName = "rohit",
                FullName = "Rohit",
                Email = "rohit@gmail.com",
                Password = "123456"
            };

            _repoMock.Setup(x => x.ExistsByUserNameAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            _repoMock.Setup(x => x.ExistsByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            _repoMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.UserId = 1;
                    return u;
                });

            var registerResult = await _service.RegisterAsync(req);

            var result = await _service.ValidateTokenAsync(registerResult.Token);

            Assert.That(result, Is.True);
        }
    }
}