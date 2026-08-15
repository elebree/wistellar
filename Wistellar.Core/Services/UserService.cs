using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using Wistellar.Core.Entities;

namespace Wistellar.Core.Services
{
    public class UserService(WiGleBackupContext context, ILogger<UserService> logger) : IUserService
    {
        private readonly WiGleBackupContext _context = context;
        private readonly ILogger<UserService> _logger = logger;

        /// <summary>
        /// Hashes a password using SHA256 with random salt
        /// </summary>
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            // Generate a random salt for each password
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var combinedBytes = new byte[salt.Length + passwordBytes.Length];

            Buffer.BlockCopy(salt, 0, combinedBytes, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, combinedBytes, salt.Length, passwordBytes.Length);

            var hashBytes = sha256.ComputeHash(combinedBytes);
            // Store both salt and hash together, separated by a delimiter
            var saltBase64 = Convert.ToBase64String(salt);
            var hashBase64 = Convert.ToBase64String(hashBytes);
            return $"{saltBase64}:{hashBase64}";
        }
        /// <summary>
        /// Verifies a password against a stored hash
        /// </summary>
        private static bool VerifyPassword(string password, string storedHash)
        {
            // Split the stored hash to extract salt and actual hash
            var parts = storedHash.Split(':');
            if (parts.Length != 2)
                return false;

            var salt = Convert.FromBase64String(parts[0]);
            var storedHashBytes = Convert.FromBase64String(parts[1]);

            // Recompute hash with the extracted salt
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var combinedBytes = new byte[salt.Length + passwordBytes.Length];

            Buffer.BlockCopy(salt, 0, combinedBytes, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, combinedBytes, salt.Length, passwordBytes.Length);

            var computedHashBytes = SHA256.HashData(combinedBytes);
            return CryptographicOperations.FixedTimeEquals(computedHashBytes, storedHashBytes);
        }

        public async Task<WsUser> AddUserAsync(string username, string password, UserRole role = UserRole.Member)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty", nameof(username));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            // Check if username already exists
            if (await UsernameExistsAsync(username))
                throw new ArgumentException("Username already exists", nameof(username));

            var hashedPassword = HashPassword(password);

            var user = new WsUser
            {
                Username = username.Trim(),
                Secret = hashedPassword,
                Role = role,
                Active = true,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {Username} created successfully with role {Role}", username, role);
            return user;
        }

        public async Task<bool> CheckCredentialsAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null || !user.Active)
                return false;

            return VerifyPassword(password, user.Secret);
        }

        public async Task<WsUser?> GetUserByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<WsUser?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        public async Task<WsUser> UpdateUserAsync(int userId, string? username = null, string? password = null,
                                               UserRole? role = null, bool? active = null)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found");

            bool changesMade = false;

            if (!string.IsNullOrWhiteSpace(username) && username != user.Username)
            {
                // Check if new username already exists
                if (await _context.Users.AnyAsync(u => u.Username == username && u.Id != userId))
                    throw new ArgumentException("Username already exists", nameof(username));

                user.Username = username;
                changesMade = true;
            }

            if (!string.IsNullOrWhiteSpace(password))
            {
                user.Secret = HashPassword(password);
                changesMade = true;
            }

            if (role.HasValue && role.Value != user.Role)
            {
                user.Role = role.Value;
                changesMade = true;
            }

            if (active.HasValue && active.Value != user.Active)
            {
                user.Active = active.Value;
                changesMade = true;
            }

            if (changesMade)
            {
                user.Updated = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("User {Username} (ID: {UserId}) updated", user.Username, userId);
            }

            return user;
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {Username} (ID: {UserId}) deleted", user.Username, userId);
            return true;
        }

        public async Task<bool> DeleteUserByUsernameAsync(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
                return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {Username} (ID: {UserId}) deleted", username, user.Id);
            return true;
        }

        public async Task<List<WsUser>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            return await _context.Users.AnyAsync(u => u.Username == username);
        }
    }
}