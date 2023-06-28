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

        public TestSettings(string testName)
        {
            _testName = testName;
        }
        public IGlobalConfiguration HangfireConfig => null;

        public DbContextOptionsBuilder WaitsDbConfig =>
            new DbContextOptionsBuilder()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_testName};");

        public string CurrentServiceUrl => null;

        public string[] DllsToScan => null;

        public bool ForceRescan { get; set; } = true;
        public string CurrentDbName { get; set; }
        public int CurrentServiceId { get; set; } = -1;

        public IDistributedLockProvider DistributedLockProvider =>
            new WaitHandleDistributedSynchronizationProvider();
        //public IDistributedLockProvider DistributedLockProvider =>
        //    new SqlDistributedSynchronizationProvider("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True");
    }
}