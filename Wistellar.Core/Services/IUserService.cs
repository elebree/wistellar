using Wistellar.Core.Entities;

namespace Wistellar.Core.Services
{
    public interface IUserService
    {
        /// <summary>
        /// Adds a new user to the system
        /// </summary>
        /// <param name="username">Username for the new user</param>
        /// <param name="password">Password for the new user</param>
        /// <param name="role">User role (default: Member)</param>
        /// <returns>Created user entity</returns>
        Task<WsUser> AddUserAsync(string username, string password, UserRole role = UserRole.Member);

        /// <summary>
        /// Checks if user credentials are valid
        /// </summary>
        /// <param name="username">Username to check</param>
        /// <param name="password">Password to verify</param>
        /// <returns>True if credentials are valid, false otherwise</returns>
        Task<bool> CheckCredentialsAsync(string username, string password);

        /// <summary>
        /// Gets a user by username
        /// </summary>
        /// <param name="username">Username to find</param>
        /// <returns>User entity or null if not found</returns>
        Task<WsUser?> GetUserByUsernameAsync(string username);

        /// <summary>
        /// Gets a user by ID
        /// </summary>
        /// <param name="userId">User ID to find</param>
        /// <returns>User entity or null if not found</returns>
        Task<WsUser?> GetUserByIdAsync(int userId);

        /// <summary>
        /// Updates an existing user
        /// </summary>
        /// <param name="userId">ID of user to update</param>
        /// <param name="username">New username (optional)</param>
        /// <param name="password">New password (optional)</param>
        /// <param name="role">New role (optional)</param>
        /// <param name="active">New active status (optional)</param>
        /// <returns>Updated user entity</returns>
        Task<WsUser> UpdateUserAsync(int userId, string? username = null, string? password = null,
                                    UserRole? role = null, bool? active = null);

        /// <summary>
        /// Deletes a user by ID
        /// </summary>
        /// <param name="userId">ID of user to delete</param>
        /// <returns>True if deletion was successful</returns>
        Task<bool> DeleteUserAsync(int userId);

        /// <summary>
        /// Deletes a user by username
        /// </summary>
        /// <param name="username">Username of user to delete</param>
        /// <returns>True if deletion was successful</returns>
        Task<bool> DeleteUserByUsernameAsync(string username);

        /// <summary>
        /// Gets all users in the system
        /// </summary>
        /// <returns>List of all users</returns>
        Task<List<WsUser>> GetAllUsersAsync();

        /// <summary>
        /// Checks if a username already exists
        /// </summary>
        /// <param name="username">Username to check</param>
        /// <returns>True if username exists</returns>
        Task<bool> UsernameExistsAsync(string username);
    }
}