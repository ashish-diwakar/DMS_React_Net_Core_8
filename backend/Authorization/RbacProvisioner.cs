using System.Globalization;
using Diwakar.Cms.Api.Authentication;
using MySqlConnector;

namespace Diwakar.Cms.Api.Authorization;

public sealed class RbacProvisioner(MySqlDataSource source,PasswordHasher hasher)
{
    public async Task<ulong> ProvisionAsync(string? email,string? name,string? password,string? confirmation,CancellationToken ct)
    {
        email=email?.Trim().ToLowerInvariant();
        if(!AuthValidation.Email(email))throw new ArgumentException("Enter a valid email address.");
        if(password is not null && (!AuthValidation.NewPassword(password,confirmation)||string.IsNullOrWhiteSpace(name)||name.Trim().Length>255))throw new ArgumentException("Provide a valid name and matching password of at least eight characters.");
        await using var c=await source.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
        await using(var lockRole=c.CreateCommand()){lockRole.Transaction=tx;lockRole.CommandText="SELECT id FROM roles WHERE name='Super Admin' AND guard_name='web' FOR UPDATE";if(await lockRole.ExecuteScalarAsync(ct) is null)throw new InvalidOperationException("The existing web Super Admin role is missing; no catalogue was seeded.");}
        ulong id;string? existingHash=null;
        await using(var find=c.CreateCommand()){find.Transaction=tx;find.CommandText="SELECT id,password FROM users WHERE email=@email LIMIT 1 FOR UPDATE";find.Parameters.AddWithValue("@email",email);await using var rd=await find.ExecuteReaderAsync(ct);if(await rd.ReadAsync(ct)){id=rd.GetUInt64(0);existingHash=rd.GetString(1);}else id=0;}
        if(id==0){if(password is null)throw new ArgumentException("A new user requires an explicit name and hidden password entry.");await using var add=c.CreateCommand();add.Transaction=tx;add.CommandText="INSERT INTO users(name,email,password,created_at,updated_at) VALUES(@name,@email,@hash,UTC_TIMESTAMP(),UTC_TIMESTAMP())";add.Parameters.AddWithValue("@name",name!.Trim());add.Parameters.AddWithValue("@email",email);add.Parameters.AddWithValue("@hash",hasher.Hash(password));await add.ExecuteNonQueryAsync(ct);id=Convert.ToUInt64(add.LastInsertedId,CultureInfo.InvariantCulture);}
        else if(password is not null && existingHash is null)throw new InvalidOperationException("Existing account data could not be read safely.");
        await using(var assign=c.CreateCommand()){assign.Transaction=tx;assign.CommandText="INSERT IGNORE INTO model_has_roles(role_id,model_type,model_id) SELECT id,@model,@user FROM roles WHERE name='Super Admin' AND guard_name='web'";assign.Parameters.AddWithValue("@model","App\\Models\\User");assign.Parameters.AddWithValue("@user",id);if(await assign.ExecuteNonQueryAsync(ct)!=1){await using var verify=c.CreateCommand();verify.Transaction=tx;verify.CommandText="SELECT COUNT(*) FROM model_has_roles mr JOIN roles r ON r.id=mr.role_id WHERE mr.model_type=@model AND mr.model_id=@id AND r.name='Super Admin' AND r.guard_name='web'";verify.Parameters.AddWithValue("@model","App\\Models\\User");verify.Parameters.AddWithValue("@id",id);if(Convert.ToInt32(await verify.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)!=1)throw new InvalidOperationException("Could not assign the Super Admin role.");}}
        await using(var cache=c.CreateCommand()){cache.Transaction=tx;cache.CommandText="DELETE FROM cache WHERE `key`='laravel-cache-spatie.permission.cache'";await cache.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);return id;
    }
}

public static class RbacProvisioningCommand
{
    public static async Task RunAsync(string[] args)
    {
        if(args.Length>2)throw new ArgumentException("Usage: provision-super-admin [email]");
        var email=args.Length==2?args[1]:PromptLine("Email: ");
        string? name=null,password=null,confirmation=null;
        
        if(!string.IsNullOrWhiteSpace(email))
        {
            name=PromptLine("Name (leave blank only if the account already exists): ");
            if(!string.IsNullOrWhiteSpace(name)){password=ReadHidden("New password: ");confirmation=ReadHidden("Confirm password: ");}
        }
        var builder=WebApplication.CreateBuilder(Array.Empty<string>());var directory=new DirectoryInfo(AppContext.BaseDirectory);while(directory is not null && !File.Exists(Path.Combine(directory.FullName,"appsettings.json")))directory=directory.Parent;if(directory is null)throw new InvalidOperationException("Backend configuration was not found.");builder.Configuration.AddJsonFile(Path.Combine(directory.FullName,"appsettings.json"),optional:true).AddJsonFile(Path.Combine(directory.FullName,"appsettings.Local.json"),optional:true).AddEnvironmentVariables();
        var db=builder.Configuration.GetSection("Database");var cs=new MySqlConnectionStringBuilder{Server=db["Server"]??"127.0.0.1",Port=db.GetValue<uint?>("Port")??3306,Database=db["Name"]??throw new InvalidOperationException("Server-side database configuration is required."),UserID=db["User"]??"",Password=db["Password"]??"",ConnectionTimeout=5};
        await using var source=new MySqlDataSourceBuilder(cs.ConnectionString).Build();var id=await new RbacProvisioner(source,new PasswordHasher()).ProvisionAsync(email,name,password,confirmation,CancellationToken.None);Console.WriteLine($"Super Admin role assigned to user id {id}.");
    }
    private static string ReadHidden(string prompt){Console.Write(prompt);var value=new System.Text.StringBuilder();while(true){var key=Console.ReadKey(true);if(key.Key==ConsoleKey.Enter)break;if(key.Key==ConsoleKey.Backspace&&value.Length>0)value.Length--;else if(!char.IsControl(key.KeyChar))value.Append(key.KeyChar);}Console.WriteLine();return value.ToString();}
    private static string PromptLine(string prompt){Console.Write(prompt);return Console.ReadLine()??"";}
}