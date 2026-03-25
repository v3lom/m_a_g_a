using System;
using System.Collections.Generic;
using System.IO;

namespace M_A_G_A.Helpers
{
    /// <summary>
    /// Migrates chat history between JSON files (legacy) and SQLite (default).
    /// </summary>
    public static class MigrationHelper
    {
        /// <summary>
        /// Copies all JSON history files into the SQLite database.
        /// Existing SQLite records are NOT overwritten (safe to run multiple times).
        /// </summary>
        public static (int contacts, int messages) MigrateJsonToSqlite()
        {
            int contacts = 0, messages = 0;
            try
            {
                foreach (var contactId in HistoryHelper.GetSavedContactIds())
                {
                    var history = HistoryHelper.LoadHistory(contactId);
                    if (history.Count == 0) continue;
                    foreach (var msg in history)
                        SqliteHistoryHelper.AppendMessage(contactId, msg);
                    contacts++;
                    messages += history.Count;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("MigrationHelper.MigrateJsonToSqlite", ex);
            }
            return (contacts, messages);
        }

        /// <summary>
        /// Copies all SQLite history into JSON files.
        /// Existing JSON content is merged (deduplicated by message ID).
        /// </summary>
        public static (int contacts, int messages) MigrateSqliteToJson()
        {
            int contacts = 0, messages = 0;
            try
            {
                foreach (var contactId in SqliteHistoryHelper.GetSavedContactIds())
                {
                    var history = SqliteHistoryHelper.LoadHistory(contactId);
                    if (history.Count == 0) continue;
                    HistoryHelper.SaveHistory(contactId, history);
                    contacts++;
                    messages += history.Count;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("MigrationHelper.MigrateSqliteToJson", ex);
            }
            return (contacts, messages);
        }
    }
}
