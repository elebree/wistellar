using Wistellar.Core.Services;

namespace Wistellar.Server.Services
{
    public static class CommandLineService
    {
        public static async Task HandleCommandLineArguments(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    PrintUsage();
                    return;
                }

                // Parse command line arguments
                string command = args[0].ToLower();
                string? username = null;
                string? password = null;
                string? role = null;

                for (int i = 1; i < args.Length; i++)
                {
                    switch (args[i].ToLower())
                    {
                        case "--username":
                            if (i + 1 < args.Length) username = args[++i];
                            break;
                        case "--password":
                            if (i + 1 < args.Length) password = args[++i];
                            break;
                        case "--role":
                            if (i + 1 < args.Length) role = args[++i];
                            break;
                    }
                }

                // Validate required parameters
                if (string.IsNullOrWhiteSpace(username))
                {
                    Console.WriteLine(Properties.Resources.ErrorUsernameRequired);
                    PrintUsage();
                    Environment.Exit(1);
                }

                // Build service provider using the same configuration as web service
                var builder = WebApplication.CreateBuilder(args);
                Wistellar.Server.Config.ServiceConfiguration.ConfigureServices(builder);

                using var serviceProvider = builder.Services.BuildServiceProvider();

                using var scope = serviceProvider.CreateScope();
                var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

                switch (command)
                {
                    case "--add-user":
                        if (string.IsNullOrWhiteSpace(password))
                        {
                            Console.WriteLine(Properties.Resources.ErrorPasswordRequired);
                            PrintUsage();
                            Environment.Exit(1);
                        }

                        // Parse role, default to Member
                        Wistellar.Core.Entities.UserRole userRole = Wistellar.Core.Entities.UserRole.Member;
                        if (!string.IsNullOrWhiteSpace(role))
                        {
                            if (Enum.TryParse<Wistellar.Core.Entities.UserRole>(role, true, out var parsedRole))
                            {
                                userRole = parsedRole;
                            }
                            else
                            {
                                Console.WriteLine(string.Format(Properties.Resources.WarningInvalidRole, role));
                            }
                        }

                        // Check if user already exists
                        var userExists = await userService.UsernameExistsAsync(username);
                        if (userExists)
                        {
                            Console.WriteLine(string.Format(Properties.Resources.ErrorUserAlreadyExists, username));
                            Environment.Exit(1);
                        }

                        // Create the user
                        await userService.AddUserAsync(username, password, userRole);
                        Console.WriteLine(string.Format(Properties.Resources.SuccessUserCreated, username, userRole));
                        break;

                    case "--delete-user":
                        var deleteResult = await userService.DeleteUserByUsernameAsync(username);
                        if (deleteResult)
                        {
                            Console.WriteLine(string.Format(Properties.Resources.SuccessUserDeleted, username));
                        }
                        else
                        {
                            Console.WriteLine(string.Format(Properties.Resources.ErrorUserNotFoundOrCouldNotDelete, username));
                            Environment.Exit(1);
                        }
                        break;

                    case "--update-user":
                        // Update user (password and/or role)
                        var user = await userService.GetUserByUsernameAsync(username);
                        if (user == null)
                        {
                            Console.WriteLine(string.Format(Properties.Resources.ErrorUserNotFound, username));
                            Environment.Exit(1);
                        }

                        Core.Entities.UserRole? updateRole = null;
                        if (!string.IsNullOrWhiteSpace(role))
                        {
                            if (Enum.TryParse<Core.Entities.UserRole>(role, true, out var parsedRole))
                            {
                                updateRole = parsedRole;
                            }
                            else
                            {
                                Console.WriteLine(string.Format(Properties.Resources.WarningInvalidRoleUpdate, role));
                            }
                        }

                        await userService.UpdateUserAsync(user.Id, password: string.IsNullOrWhiteSpace(password) ? null : password, role: updateRole);
                        Console.WriteLine(string.Format(Properties.Resources.SuccessUserUpdated, username));
                        break;

                    default:
                        Console.WriteLine(string.Format(Properties.Resources.ErrorUnknownCommand, command));
                        PrintUsage();
                        Environment.Exit(1);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format(Properties.Resources.ErrorGeneric, ex.Message));
                Environment.Exit(1);
            }
        }

        private static void PrintUsage()
        {
            var appName = Path.GetFileName(Environment.ProcessPath) ?? "Wistellar.Server";
            Console.WriteLine(string.Format(Properties.Resources.CompleteUsage, appName));
        }
    }
}