﻿using Hangfire;
using Medallion.Threading;
using Medallion.Threading.WaitHandles;
using Microsoft.EntityFrameworkCore;
using ResumableFunctions.Handler.InOuts;

namespace ResumableFunctions.Handler.Testing
{
    internal class TestSettings : IResumableFunctionsSettings
    {

        private readonly string _testName;
        private const string Server = "(localdb)\\MSSQLLocalDB";



        public TestSettings(string testName)
        {
            _testName = testName;
        }
        public IGlobalConfiguration HangfireConfig => null;

        public DbContextOptionsBuilder WaitsDbConfig =>
            new DbContextOptionsBuilder()
            .UseSqlServer($"Server={Server};Database={_testName};Trusted_Connection=True;TrustServerCertificate=True;");

        public string CurrentServiceUrl => null;

        public string[] DllsToScan => null;

        public bool ForceRescan { get; set; } = true;
        public string CurrentWaitsDbName { get; set; }
        public int CurrentServiceId { get; set; } = -1;

        public IDistributedLockProvider DistributedLockProvider =>
            new WaitHandleDistributedSynchronizationProvider();

        public CleanDatabaseSettings CleanDbSettings => new CleanDatabaseSettings();
    }
}