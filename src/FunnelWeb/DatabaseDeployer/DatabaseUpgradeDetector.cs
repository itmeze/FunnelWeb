﻿using System.Collections.Generic;
using System.Linq;
using FunnelWeb.Providers.Database;

namespace FunnelWeb.DatabaseDeployer
{
    public class DatabaseUpgradeDetector : IDatabaseUpgradeDetector
    {
        private readonly IConnectionStringSettings connectionStringSettings;
        private readonly IEnumerable<ScriptedExtension> extensions;
        private readonly IDatabaseProvider databaseProvider;
        private readonly IApplicationDatabase database;
        private bool? updateNeeded;
        private readonly object @lock = new object();

        public DatabaseUpgradeDetector(
            IConnectionStringSettings connectionStringSettings, 
            IEnumerable<ScriptedExtension> extensions, 
            IApplicationDatabase database,
            IDatabaseProvider databaseProvider)
        {
            this.connectionStringSettings = connectionStringSettings;
            this.extensions = extensions;
            this.database = database;
            this.databaseProvider = databaseProvider;
        }

        public bool UpdateNeeded()
        {
            if (updateNeeded != null)
                return updateNeeded.Value;

            lock (@lock)
            {
                if (updateNeeded != null)
                    return updateNeeded.Value;

                var connectionString = connectionStringSettings.ConnectionString;

                string error;
                if (databaseProvider.TryConnect(connectionString, out error))
                {
                    var currentScripts = database.GetCoreExecutedScripts(databaseProvider.GetConnectionFactory(connectionString));
                    var requiredScripts = database.GetCoreRequiredScripts();
                    var notRun = requiredScripts.Select(x => x.Trim().ToLowerInvariant())
                        .Except(currentScripts.Select(x => x.Trim().ToLowerInvariant()))
                        .ToList();

                    updateNeeded = notRun.Count > 0
                        || ExtensionsRequireUpdate(extensions, database, databaseProvider, connectionString);
                }
                else
                {
                    updateNeeded = true;
                }

                return updateNeeded.Value;
            }
        }

        public void Reset()
        {
            lock (@lock)
                updateNeeded = null;
        }

        private static bool ExtensionsRequireUpdate(IEnumerable<ScriptedExtension> extensions, IApplicationDatabase applicationDatabase, 
            IDatabaseProvider databaseProvider, string connectionString)
        {
            return (from x in extensions
                    let current = applicationDatabase.GetExtensionExecutedScripts(databaseProvider.GetConnectionFactory(connectionString), x)
                    let required = applicationDatabase.GetExtensionRequiredScripts(x)
                    let notRun = required.Select(z => z.Trim().ToLowerInvariant())
                        .Except(current.Select(z => z.Trim().ToLowerInvariant()))
                        .ToList()
                    where notRun.Count > 0
                    select current).Any();
        }
    }
}