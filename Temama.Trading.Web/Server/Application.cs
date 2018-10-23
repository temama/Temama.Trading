using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Temama.Trading.Web.Server.Data;

namespace Temama.Trading.Web.Server
{
    public class Application
    {
        public static void Start()
        {
            InitDirectories();

            InitDatabase();
        }

        private static void InitDirectories()
        {
            if (!Directory.Exists("Data"))
                Directory.CreateDirectory("Data");
        }

        private static void InitDatabase()
        {
            using (var db = new WebContext())
            {
                db.Database.EnsureCreated();
            }
        }
    }
}
