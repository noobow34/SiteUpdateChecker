using Microsoft.EntityFrameworkCore;
using System;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.Extensions.Configuration;
using System.IO;
using EnumStringValues;
using SiteUpdateChecker.Constants;

namespace SiteUpdateChecker.EF
{
    public class ToolsContext : DbContext
    {

        public virtual DbSet<CheckSite> CheckSites { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var config = new ConfigurationBuilder().SetBasePath(System.AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("appsettings.json").Build();
            var connectionString = config.GetConnectionString("DefaultConnection");
            optionsBuilder.UseMySql(connectionString,
                    mySqlOptions =>
                    {
                        mySqlOptions.ServerVersion(new Version(10, 3), ServerType.MariaDb);
                    }
            );
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CheckSite>()
               .Property(c => c.CheckType)
               .HasConversion(v => v.GetStringValue(), v => ((string)v).ParseToEnum<CheckTypeEnum>());
        }

    }
}
