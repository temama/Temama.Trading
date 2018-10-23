using Microsoft.EntityFrameworkCore;
using System.IO;
using Temama.Trading.Web.Server.Data.Model;

namespace Temama.Trading.Web.Server.Data
{
    public class WebContext: DbContext
    {
        private static readonly string _connectionString = Path.Combine("Data", "WebDB.db");

        private DbSet<User> Users { get; set; }
        private DbSet<Setting> Settings { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=" + _connectionString);
        }
    }
}
