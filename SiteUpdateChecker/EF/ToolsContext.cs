using Microsoft.EntityFrameworkCore;
using System;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace SiteUpdateChecker.EF
{
    public class ToolsContext:DbContext
    {

        public virtual DbSet<CheckSite> CheckSites { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
            var connectionString = config.GetConnectionString("DefaultConnection");
            optionsBuilder.UseMySql(connectionString,
                    mySqlOptions =>
                    {
                        mySqlOptions.ServerVersion(new Version(10, 3), ServerType.MariaDb);
                    }
            );
        }

    }
}
