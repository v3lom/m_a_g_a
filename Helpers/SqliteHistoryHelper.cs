using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using M_A_G_A.Models;
using Microsoft.Data.Sqlite;

namespace M_A_G_A.Helpers
{
    /// <summary>
    /// Stores chat history in a single SQLite database file.
    /// This is the recommended (default) storage back-end: it is faster and more
    /// robust than the JSON-file approach, especially for large histories.
    ///
    /// Schema:
    ///   messages(
    ///     id          TEXT PRIMARY KEY,
    ///     contact_id  TEXT NOT NULL,
    ///     sender_id   TEXT,
    ///     sender_name TEXT,
    ///     type        TEXT,
    ///     content     TEXT,
    ///     image_b64   TEXT,
    ///     file_b64    TEXT,
    ///     file_name   TEXT,
    ///     timestamp   TEXT,
    ///     is_sent_by_me INTEGER,
    ///     is_delivered  INTEGER,
    ///     is_read       INTEGER,
    ///     is_edited     INTEGER
    ///   )
    /// </summary>
    public static class SqliteHistoryHelper
    {
        private static readonly string DbPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MAGA", "history.db");

        private static readonly string ConnectionString;

        static SqliteHistoryHelper()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath));
            ConnectionString = $"Data Source={DbPath};Version=3;Journal Mode=WAL;";
            EnsureSchema();
        }

        // ── Connection factory ────────────────────────────────────────────

        private static SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        private static void EnsureSchema()
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS messages (
                        id            TEXT PRIMARY KEY,
                        contact_id    TEXT NOT NULL,
                        sender_id     TEXT,
                        sender_name   TEXT,
                        type          TEXT,
                        content       TEXT,
                        image_b64     TEXT,
                        file_b64      TEXT,
                        file_name     TEXT,
                        timestamp     TEXT,
                        is_sent_by_me INTEGER DEFAULT 0,
                        is_delivered  INTEGER DEFAULT 0,
                        is_read       INTEGER DEFAULT 0,
                        is_edited     INTEGER DEFAULT 0
                    );
                    CREATE INDEX IF NOT EXISTS idx_contact ON messages(contact_id);";
                cmd.ExecuteNonQuery();
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        public static List<ChatMessage> LoadHistory(string contactId)
        {
            var result = new List<ChatMessage>();
            try
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT id, sender_id, sender_name, type, content,
                               image_b64, file_b64, file_name, timestamp,
                               is_sent_by_me, is_delivered, is_read, is_edited
                        FROM messages
                        WHERE contact_id = @cid
                        ORDER BY timestamp ASC";
                    cmd.Parameters.AddWithValue("@cid", contactId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            result.Add(ReadRow(r));
                    }
                }
            }
            catch (Exception ex) { AppLogger.Error("SqliteHistoryHelper.LoadHistory", ex); }
            return result;
        }

        public static void AppendMessage(string contactId, ChatMessage m)
        {
            try
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO messages
                        (id, contact_id, sender_id, sender_name, type, content,
                         image_b64, file_b64, file_name, timestamp,
                         is_sent_by_me, is_delivered, is_read, is_edited)
                        VALUES
                        (@id, @cid, @sid, @sname, @type, @content,
                         @imgb64, @fileb64, @fname, @ts,
                         @isbyme, @isdeliv, @isread, @isedit)";
                    cmd.Parameters.AddWithValue("@id",      m.Id);
                    cmd.Parameters.AddWithValue("@cid",     contactId);
                    cmd.Parameters.AddWithValue("@sid",     m.SenderId ?? "");
                    cmd.Parameters.AddWithValue("@sname",   m.SenderName ?? "");
                    cmd.Parameters.AddWithValue("@type",    m.Type.ToString());
                    cmd.Parameters.AddWithValue("@content", m.Content ?? "");
                    cmd.Parameters.AddWithValue("@imgb64",  m.ImageBytes != null ? Convert.ToBase64String(m.ImageBytes) : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@fileb64", m.FileBytes  != null ? Convert.ToBase64String(m.FileBytes)  : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@fname",   m.FileName ?? "");
                    cmd.Parameters.AddWithValue("@ts",      m.Timestamp.ToString("o"));
                    cmd.Parameters.AddWithValue("@isbyme",  m.IsSentByMe  ? 1 : 0);
                    cmd.Parameters.AddWithValue("@isdeliv", m.IsDelivered ? 1 : 0);
                    cmd.Parameters.AddWithValue("@isread",  m.IsRead      ? 1 : 0);
                    cmd.Parameters.AddWithValue("@isedit",  m.IsEdited    ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { AppLogger.Error("SqliteHistoryHelper.AppendMessage", ex); }
        }

        public static void UpdateMessageStatus(string messageId, bool isDelivered, bool isRead)
        {
            try
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE messages SET is_delivered=@d, is_read=@r WHERE id=@id";
                    cmd.Parameters.AddWithValue("@d",  isDelivered ? 1 : 0);
                    cmd.Parameters.AddWithValue("@r",  isRead ? 1 : 0);
                    cmd.Parameters.AddWithValue("@id", messageId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { AppLogger.Error("SqliteHistoryHelper.UpdateMessageStatus", ex); }
        }

        public static void UpdateMessageContent(string messageId, string newContent)
        {
            try
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE messages SET content=@c, is_edited=1 WHERE id=@id";
                    cmd.Parameters.AddWithValue("@c",  newContent);
                    cmd.Parameters.AddWithValue("@id", messageId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { AppLogger.Error("SqliteHistoryHelper.UpdateMessageContent", ex); }
        }

        public static void DeleteMessage(string messageId)
        {
            try
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM messages WHERE id=@id";
                    cmd.Parameters.AddWithValue("@id", messageId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { AppLogger.Error("SqliteHistoryHelper.DeleteMessage", ex); }
        }

        public static void SaveHistory(string contactId, IEnumerable<ChatMessage> messages)
        {
            try
            {
                using (var conn = OpenConnection())
                using (var tx = conn.BeginTransaction())
                {
                    // Delete existing
                    using (var del = conn.CreateCommand())
                    {
                        del.CommandText = "DELETE FROM messages WHERE contact_id=@cid";
                        del.Parameters.AddWithValue("@cid", contactId);
                        del.ExecuteNonQuery();
                    }
                    // Batch insert with prepared statement
                    using (var ins = conn.CreateCommand())
                    {
                        ins.CommandText = @"
                            INSERT OR REPLACE INTO messages
                            (id, contact_id, sender_id, sender_name, type, content,
                             image_b64, file_b64, file_name, timestamp,
                             is_sent_by_me, is_delivered, is_read, is_edited)
                            VALUES
                            (@id, @cid, @sid, @sname, @type, @content,
                             @imgb64, @fileb64, @fname, @ts,
                             @isbyme, @isdeliv, @isread, @isedit)";
                        var pId      = ins.Parameters.Add("@id", (SqliteType)DbType.String);
                        var pCid     = ins.Parameters.Add("@cid", (SqliteType)DbType.String);
                        var pSid     = ins.Parameters.Add("@sid", (SqliteType)DbType.String);
                        var pSname   = ins.Parameters.Add("@sname",   (SqliteType)DbType.String);
                        var pType    = ins.Parameters.Add("@type",    (SqliteType)DbType.String);
                        var pContent = ins.Parameters.Add("@content", (SqliteType)DbType.String);
                        var pImg     = ins.Parameters.Add("@imgb64",    (SqliteType)DbType.String);
                        var pFile    = ins.Parameters.Add("@fileb64", (SqliteType)DbType.String);
                        var pFname   = ins.Parameters.Add("@fname",   (SqliteType)DbType.String);
                        var pTs      = ins.Parameters.Add("@ts",      (SqliteType)DbType.String);
                        var pByMe    = ins.Parameters.Add("@isbyme",  (SqliteType)DbType.Int32);
                        var pDeliv   = ins.Parameters.Add("@isdeliv", (SqliteType)DbType.Int32);
                        var pRead    = ins.Parameters.Add("@isread",  (SqliteType)DbType.Int32);
                        var pEdit    = ins.Parameters.Add("@isedit",  (SqliteType)DbType.Int32);

                        foreach (var m in messages)
                        {
                            pId.Value      = m.Id;
                            pCid.Value     = contactId;
                            pSid.Value     = m.SenderId ?? "";
                            pSname.Value   = m.SenderName ?? "";
                            pType.Value    = m.Type.ToString();
                            pContent.Value = m.Content ?? "";
                            pImg.Value     = m.ImageBytes != null ? (object)Convert.ToBase64String(m.ImageBytes) : DBNull.Value;
                            pFile.Value    = m.FileBytes  != null ? (object)Convert.ToBase64String(m.FileBytes)  : DBNull.Value;
                            pFname.Value   = m.FileName ?? "";
                            pTs.Value      = m.Timestamp.ToString("o");
                            pByMe.Value    = m.IsSentByMe  ? 1 : 0;
                            pDeliv.Value   = m.IsDelivered ? 1 : 0;
                            pRead.Value    = m.IsRead      ? 1 : 0;
                            pEdit.Value    = m.IsEdited    ? 1 : 0;
                            ins.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("SqliteHistoryHelper.SaveHistory", ex);
                throw;
            }
        }

        public static IEnumerable<string> GetSavedContactIds()
        {
            var ids = new List<string>();
            try
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT DISTINCT contact_id FROM messages";
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) ids.Add(r.GetString(0));
                }
            }
            catch (Exception ex) { AppLogger.Error("SqliteHistoryHelper.GetSavedContactIds", ex); }
            return ids;
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static ChatMessage ReadRow(IDataReader r)
        {
            MessageType type = MessageType.Text;
            Enum.TryParse(r["type"]?.ToString(), out type);
            var imgB64  = r["image_b64"] as string;
            var fileB64 = r["file_b64"]  as string;
            return new ChatMessage
            {
                Id          = r["id"]?.ToString() ?? Guid.NewGuid().ToString(),
                SenderId    = r["sender_id"]?.ToString(),
                SenderName  = r["sender_name"]?.ToString(),
                Type        = type,
                Content     = r["content"]?.ToString(),
                ImageBytes  = !string.IsNullOrEmpty(imgB64)  ? TryFromBase64(imgB64)  : null,
                FileBytes   = !string.IsNullOrEmpty(fileB64) ? TryFromBase64(fileB64) : null,
                FileName    = r["file_name"]?.ToString(),
                Timestamp   = DateTime.TryParse(r["timestamp"]?.ToString(), null,
                                  System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                              ? dt : DateTime.Now,
                IsSentByMe  = Convert.ToInt32(r["is_sent_by_me"]) != 0,
                IsDelivered = Convert.ToInt32(r["is_delivered"])   != 0,
                IsRead      = Convert.ToInt32(r["is_read"])        != 0,
                IsEdited    = Convert.ToInt32(r["is_edited"])      != 0,
            };
        }

        private static byte[] TryFromBase64(string s)
        {
            try { return Convert.FromBase64String(s); } catch { return null; }
        }
    }
}
