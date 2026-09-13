using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Pame.Core;

public sealed class Database : IDisposable
{
    readonly SqliteConnection connection;
    readonly object sync = new();
    public Database(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        connection.Open();
        Execute("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; CREATE TABLE IF NOT EXISTS migrations(version INTEGER PRIMARY KEY); CREATE TABLE IF NOT EXISTS games(id TEXT PRIMARY KEY, json TEXT NOT NULL); CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY,json TEXT NOT NULL); CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY,game_id TEXT NOT NULL,started TEXT NOT NULL,heartbeat TEXT NOT NULL,seconds INTEGER NOT NULL DEFAULT 0,ended TEXT); INSERT OR IGNORE INTO migrations VALUES(1);");
        // Recover only observed playtime through the last heartbeat, never count downtime.
        lock (sync)
        {
            using var cmd = connection.CreateCommand(); cmd.CommandText = "SELECT id,game_id,seconds FROM sessions WHERE ended IS NULL";
            using var reader = cmd.ExecuteReader(); var recover = new List<(string,string,long)>();
            while(reader.Read()) recover.Add((reader.GetString(0),reader.GetString(1),reader.GetInt64(2)));
            reader.Close();
            foreach(var (id,game,seconds) in recover) FinishSession(id,game,seconds);
        }
    }
    void Execute(string sql, params (string, object?)[] args)
    {
        using var cmd = connection.CreateCommand(); cmd.CommandText = sql;
        foreach(var (name,value) in args) cmd.Parameters.AddWithValue(name,value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
    public List<Game> LoadGames()
    {
        lock(sync) { using var cmd = connection.CreateCommand(); cmd.CommandText = "SELECT json FROM games"; using var r=cmd.ExecuteReader(); var games = new List<Game>(); while(r.Read()) { try { var g=JsonSerializer.Deserialize<Game>(r.GetString(0)); if(g!=null) games.Add(g); } catch(JsonException) {} } return games; }
    }
    public void SaveGames(IEnumerable<Game> games)
    {
        lock(sync) { using var tx = connection.BeginTransaction(); foreach(var g in games) SaveGameInternal(g); tx.Commit(); }
    }
    void SaveGameInternal(Game game)
    {
        // A rescan or metadata request can finish after a session heartbeat/exit.
        // Never let an older in-memory record roll play history backwards.
        using var query=connection.CreateCommand();query.CommandText="SELECT json FROM games WHERE id=$id";query.Parameters.AddWithValue("$id",game.Id);
        if(query.ExecuteScalar() is string existing)
        {
            var prior=JsonSerializer.Deserialize<Game>(existing);
            if(prior!=null){game.LocalPlaySeconds=Math.Max(game.LocalPlaySeconds,prior.LocalPlaySeconds);game.ImportedPlaySeconds=Math.Max(game.ImportedPlaySeconds,prior.ImportedPlaySeconds);if(game.LastPlayed==null||prior.LastPlayed>game.LastPlayed)game.LastPlayed=prior.LastPlayed;}
        }
        Execute("INSERT INTO games VALUES($id,$json) ON CONFLICT(id) DO UPDATE SET json=excluded.json",("$id",game.Id),("$json",JsonSerializer.Serialize(game)));
    }
    public void SaveGame(Game game) { lock(sync) SaveGameInternal(game); }
    public T Get<T>(string key, T fallback)
    {
        lock(sync) { using var cmd=connection.CreateCommand();cmd.CommandText="SELECT json FROM settings WHERE key=$key";cmd.Parameters.AddWithValue("$key",key); var v=cmd.ExecuteScalar() as string;try { return v==null ? fallback : JsonSerializer.Deserialize<T>(v) ?? fallback; } catch(JsonException) { return fallback; } }
    }
    public void Set<T>(string key,T value) { lock(sync) Execute("INSERT INTO settings VALUES($key,$json) ON CONFLICT(key) DO UPDATE SET json=excluded.json",("$key",key),("$json",JsonSerializer.Serialize(value))); }
    public string BeginSession(string gameId)
    {
        var id=Guid.NewGuid().ToString("N"); lock(sync) Execute("INSERT INTO sessions(id,game_id,started,heartbeat) VALUES($id,$game,$now,$now)",("$id",id),("$game",gameId),("$now",DateTimeOffset.UtcNow.ToString("O"))); return id;
    }
    public void Heartbeat(string id,long seconds) { lock(sync) Execute("UPDATE sessions SET heartbeat=$now,seconds=$s WHERE id=$id AND ended IS NULL",("$id",id),("$s",seconds),("$now",DateTimeOffset.UtcNow.ToString("O"))); }
    public void FinishSession(string id,string gameId,long seconds)
    {
        lock(sync)
        {
            using var tx=connection.BeginTransaction();
            using var cmd=connection.CreateCommand();cmd.CommandText="SELECT ended FROM sessions WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);var ended=cmd.ExecuteScalar();
            if(ended==DBNull.Value)
            {
                var g=LoadGames().FirstOrDefault(x=>x.Id==gameId);
                if(g!=null) { g.LocalPlaySeconds+=Math.Max(0,seconds);g.LastPlayed=DateTimeOffset.UtcNow;SaveGameInternal(g); }
                Execute("UPDATE sessions SET ended=$now,seconds=$s WHERE id=$id",("$id",id),("$s",seconds),("$now",DateTimeOffset.UtcNow.ToString("O")));
            }
            tx.Commit();
        }
    }
    public void Dispose() => connection.Dispose();
}

public static class Log
{
    private static readonly object Sync = new();
    public static string DirectoryPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Pame","logs");
    public static void Write(string name, object? detail = null)
    {
        try { lock(Sync) { Directory.CreateDirectory(DirectoryPath); File.AppendAllText(Path.Combine(DirectoryPath,$"pame-{DateTime.Today:yyyyMMdd}.jsonl"),JsonSerializer.Serialize(new { at=DateTimeOffset.UtcNow, name, detail })+Environment.NewLine); } } catch(IOException) {} catch(UnauthorizedAccessException) {}
    }
    public static void Error(string area,Exception ex) => Write(area,new { error=ex.GetType().Name,ex.Message });
}
