using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GuarinoApp.Infrastructure.Data
    {
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
        {
        public ApplicationDbContext CreateDbContext(string[] args)
            {
            var basePath = Directory.GetCurrentDirectory();
            var webProjectPath = Path.Combine(basePath, "..", "GuarinoApp.Web");

            if (Directory.Exists(webProjectPath))
                basePath = webProjectPath;

            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));

            return new ApplicationDbContext(optionsBuilder.Options);
            }
        }
    }
